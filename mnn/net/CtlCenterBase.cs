﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace mnn.net {
    public class CtlCenterBase {
        // session control
        protected SessCtl sessctl;
        // other request control
        protected DispatcherBase dispatcher;

        public CtlCenterBase()
        {
            // init sesscer
            sessctl = new SessCtl();
            sessctl.sess_parse += new SessCtl.SessParseDelegate(sess_parse);
            sessctl.sess_create += new SessCtl.SessCreateDelegate(sess_create);
            sessctl.sess_delete += new SessCtl.SessDeleteDelegate(sess_delete);

            // init dispatcher
            dispatcher = new DispatcherBase();
        }

        // Session Event ==================================================================================

        protected virtual void sess_parse(object sender, SockSess sess)
        {
            // init request & response
            SockRequest request = new SockRequest() { lep = sess.lep, rep = sess.rep };
            SockResponse response = new SockResponse() { data = null };
            byte[] data = sess.rdata.Take(sess.rdata_size).ToArray();

            // check request
            if (!request.CheckType(data)) {
                sess.RfifoSkip(sess.rdata_size);
                request.type = SockRequestType.unknown;
                request.length = -1;
                request.data = data;
            } else if (request.CheckLength(data))
                sess.RfifoSkip(request.ParseRawData(data));
            else
                return;

            // dispatch
            dispatcher.handle(request, response);
            if (response.data != null && response.data.Length != 0)
                sessctl.SendSession(sess, response.data);
        }

        protected virtual void sess_create(object sender, SockSess sess) { }

        protected virtual void sess_delete(object sender, SockSess sess) { }

        // Center Service =========================================================================

        protected virtual void default_service(SockRequest request, SockResponse response)
        {
            response.data = new byte[] { 0x34, 0x30, 0x34 };
        }

        protected virtual void sock_open_service(SockRequest request, SockResponse response)
        {
            // get param string & parse to dictionary
            string msg = Encoding.UTF8.GetString(request.data);
            if (!msg.Contains('?')) return;
            msg = msg.Substring(msg.IndexOf('?') + 1);
            IDictionary<string, string> dc = SockConvert.ParseHttpQueryParam(msg);

            // find session and open
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), int.Parse(dc["port"]));
            SockSess result = null;
            if (dc["type"] == SockType.listen.ToString() && sessctl.FindSession(SockType.listen, ep, null) == null)
                result = sessctl.MakeListen(ep);
            else if (dc["type"] == SockType.connect.ToString() && sessctl.FindSession(SockType.connect, null, ep) == null)
                result = sessctl.AddConnect(ep);
            else
                return;
        }

        protected virtual void sock_close_service(SockRequest request, SockResponse response)
        {
            // get param string & parse to dictionary
            string msg = Encoding.UTF8.GetString(request.data);
            if (!msg.Contains('?')) return;
            msg = msg.Substring(msg.IndexOf('?') + 1);
            IDictionary<string, string> dc = SockConvert.ParseHttpQueryParam(msg);

            // find session and close
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), int.Parse(dc["port"]));
            SockSess result = null;
            if (dc["type"] == SockType.listen.ToString())
                result = sessctl.FindSession(SockType.listen, ep, null);
            else if (dc["type"] == SockType.connect.ToString())
                result = sessctl.FindSession(SockType.connect, null, ep);
            else// if (dc["type"] == SockType.accept.ToString())
                result = sessctl.FindSession(SockType.accept, null, ep);

            if (result != null)
                sessctl.DelSession(result);
        }

        protected virtual void sock_send_service(SockRequest request, SockResponse response)
        {
            // retrieve param_list of url
            string param_list = Encoding.UTF8.GetString(request.data);
            if (!param_list.Contains('?')) return;
            param_list = param_list.Substring(param_list.IndexOf('?') + 1);

            // retrieve param_data
            int index_data = param_list.IndexOf("&data=");
            if (index_data == -1) return;
            string param_data = param_list.Substring(index_data + 6);
            param_list = param_list.Substring(0, index_data);

            // retrieve param to dictionary
            IDictionary<string, string> dc = SockConvert.ParseHttpQueryParam(param_list);

            // find session and send message
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), int.Parse(dc["port"]));
            SockSess result = null;
            if (dc["type"] == SockType.listen.ToString())
                result = sessctl.FindSession(SockType.listen, ep, null);
            else if (dc["type"] == SockType.connect.ToString())
                result = sessctl.FindSession(SockType.connect, null, ep);
            else// if (dc["type"] == SockType.accept.ToString())
                result = sessctl.FindSession(SockType.accept, null, ep);

            if (result != null)
                sessctl.SendSession(result, Encoding.UTF8.GetBytes(param_data));
        }
    }
}
