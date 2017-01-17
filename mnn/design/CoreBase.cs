using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using mnn.net;
using mnn.misc.service;

namespace mnn.design {
    public class CoreBase {
        // timeout
        protected TimeOutCtl timectl;
        // session control
        protected SessCtl sessctl;
        // other request control
        protected ServiceCoreBase dispatcher;

        public CoreBase()
        {
            // init timectl
            timectl = new TimeOutCtl();

            // init sessctl
            sessctl = new SessCtl();
            sessctl.sess_parse += new SessCtl.SessDelegate(SessParse);
            sessctl.sess_create += new SessCtl.SessDelegate(SessCreate);
            sessctl.sess_delete += new SessCtl.SessDelegate(SessDelete);

            // init dispatcher
            dispatcher = new ServiceCoreBase();
        }

        public virtual void Exec()
        {
            timectl.Exec();
            sessctl.Exec(1000);
        }

        // Session Event ==================================================================================

        protected virtual void SessParse(object sender, SockSess sess)
        {
            // init request & response
            ServiceRequest request = new ServiceRequest(sess.RfifoTake(), sess);
            ServiceResponse response = new ServiceResponse();

            // rfifo skip
            sess.RfifoSkip(request.length);

            // dispatch
            dispatcher.DoService(request, ref response);
            if (response.data != null && response.data.Length != 0)
                sessctl.SendSession(sess, response.data);
        }

        protected virtual void SessCreate(object sender, SockSess sess) { }

        protected virtual void SessDelete(object sender, SockSess sess) { }

        // Center Service =========================================================================

        protected virtual void DefaultService(ServiceRequest request, ref ServiceResponse response)
        {
            // write response
            response.data = Encoding.UTF8.GetBytes("Failure: unknown request\r\n");
        }

        protected virtual void SockOpenService(ServiceRequest request, ref ServiceResponse response)
        {
            // get param string & parse to dictionary
            string url = Encoding.UTF8.GetString(request.data);
            if (!url.Contains('?')) return;
            string param_list = url.Substring(url.IndexOf('?') + 1);
            IDictionary<string, string> dc = SockConvert.ParseUrlQueryParam(param_list);

            // find session and open
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), int.Parse(dc["port"]));
            SockSess result = null;
            if (dc["type"] == SockType.listen.ToString() && sessctl.FindSession(SockType.listen, ep, null) == null)
                result = sessctl.MakeListen(ep);
            else if (dc["type"] == SockType.connect.ToString())
                result = sessctl.AddConnect(ep);
            else
                result = null;

            // write response
            if (result != null)
                response.data = Encoding.UTF8.GetBytes("Success: " + dc["type"] + " " + ep.ToString() + "\r\n");
            else
                response.data = Encoding.UTF8.GetBytes("Failure: can't find " + ep.ToString() + "\r\n");
        }

        protected virtual void SockCloseService(ServiceRequest request, ref ServiceResponse response)
        {
            // get param string & parse to dictionary
            string url = Encoding.UTF8.GetString(request.data);
            if (!url.Contains('?')) return;
            string param_list = url.Substring(url.IndexOf('?') + 1);
            IDictionary<string, string> dc = SockConvert.ParseUrlQueryParam(param_list);

            // find session
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), int.Parse(dc["port"]));
            SockSess result = null;
            if (dc["type"] == SockType.listen.ToString())
                result = sessctl.FindSession(SockType.listen, ep, null);
            else if (dc["type"] == SockType.connect.ToString())
                result = sessctl.FindSession(SockType.connect, ep, null);
            else// if (dc["type"] == SockType.accept.ToString())
                result = sessctl.FindSession(SockType.accept, null, ep);

            // close session
            if (result != null)
                sessctl.DelSession(result);

            // write response
            if (result != null)
                response.data = Encoding.UTF8.GetBytes("Success: shutdown " + ep.ToString() + "\r\n");
            else
                response.data = Encoding.UTF8.GetBytes("Failure: can't find " + ep.ToString() + "\r\n");
        }

        protected virtual void SockSendService(ServiceRequest request, ref ServiceResponse response)
        {
            // retrieve param_list of url
            string url = Encoding.UTF8.GetString(request.data);
            if (!url.Contains('?')) return;
            string param_list = url.Substring(url.IndexOf('?') + 1);

            // retrieve param_data
            int index_data = param_list.IndexOf("&data=");
            if (index_data == -1) return;
            string param_data = param_list.Substring(index_data + 6);
            param_list = param_list.Substring(0, index_data);

            // retrieve param to dictionary
            IDictionary<string, string> dc = SockConvert.ParseUrlQueryParam(param_list);

            // find session
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), int.Parse(dc["port"]));
            SockSess result = null;
            if (dc["type"] == SockType.listen.ToString())
                result = sessctl.FindSession(SockType.listen, ep, null);
            else if (dc["type"] == SockType.connect.ToString())
                result = sessctl.FindSession(SockType.connect, ep, null);
            else// if (dc["type"] == SockType.accept.ToString())
                result = sessctl.FindSession(SockType.accept, null, ep);

            // send message
            if (result != null)
                sessctl.SendSession(result, Encoding.UTF8.GetBytes(param_data));

            // write response
            if (result != null)
                response.data = Encoding.UTF8.GetBytes("Success: send to " + ep.ToString() + "\r\n");
            else
                response.data = Encoding.UTF8.GetBytes("Failure: can't find " + ep.ToString() + "\r\n");
        }
    }
}
