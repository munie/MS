using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using mnn.net;
using mnn.misc.service;
using mnn.misc.module;

namespace mnn.design {
    public class CoreBase {
        // timeout
        protected TimeOutCtl timectl;
        // session control
        public SessCtl sessctl;
        // service control
        public ServiceCore servctl;
        // module control
        protected ModuleCtl modctl;

        public CoreBase()
        {
            // init timectl
            timectl = new TimeOutCtl();

            // init sessctl
            sessctl = new SessCtl();
            sessctl.sess_parse += new SessCtl.SessDelegate(OnSessParse);
            sessctl.sess_create += new SessCtl.SessDelegate(OnSessCreate);
            sessctl.sess_delete += new SessCtl.SessDelegate(OnSessDelete);

            // init servctl
            servctl = new ServiceCore();
            servctl.serv_before_do += new ServiceDoBeforeDelegate(OnServBeforeDo);
            servctl.serv_done += new ServiceDoneDelegate(OnServDone);

            // init modctl
            modctl = new ModuleCtl();
        }

        public virtual void Exec()
        {
            timectl.Exec();
            sessctl.Exec(1000);
        }

        // Session Event ==================================================================================

        protected virtual void OnSessParse(object sender, SockSess sess)
        {
            // init request & response
            ServiceRequest request = new ServiceRequest(sess.RfifoTake(), sess);
            ServiceResponse response = new ServiceResponse();

            // rfifo skip
            sess.RfifoSkip(request.length);

            // add request to service core
            servctl.AddRequest(request);
        }

        protected virtual void OnSessCreate(object sender, SockSess sess) { }

        protected virtual void OnSessDelete(object sender, SockSess sess) { }

        // Service Event ==================================================================================

        protected virtual void OnServBeforeDo(ref ServiceRequest request)
        {
            if (request.content_mode != ServiceRequestContentMode.none) {
                try {
                    byte[] result = Convert.FromBase64String(Encoding.UTF8.GetString(request.data));
                    result = EncryptSym.AESDecrypt(result);
                    if (result != null) request.SetData(result);
                } catch (Exception) { }
            }
        }

        protected virtual void OnServDone(ServiceRequest request, ServiceResponse response)
        {
            if (response.data != null && response.data.Length != 0) {
                sessctl.BeginInvoke(new Action(() =>
                {
                    SockSess result = sessctl.FindSession(SockType.accept, null, (request.sdata as SockSess).rep);
                    if (result != null)
                        sessctl.SendSession(result, response.data);
                }));
            }
        }

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
