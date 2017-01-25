using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using System.Diagnostics;
using System.Xml;
using System.Xml.Serialization;
using System.Net;
using mnn.misc.glue;
using mnn.net;
using mnn.misc.env;
using mnn.service;
using mnn.module;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EnvConsole.Backend {
    class Core : BaseLayer {
        public Core()
        {
            // start nodejs
            Process process = new Process();
            process.StartInfo.FileName = "node";
            process.StartInfo.Arguments = "js\\main.js";
            //process.StartInfo.CreateNoWindow = true;
            //process.StartInfo.UseShellExecute = false;
            try {
                process.Start();
            } catch (Exception ex) {
                log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType)
                    .Error("Start nodejs failed.", ex);
                //Thread.CurrentThread.Abort();
            }
            System.Windows.Application.Current.Exit += new System.Windows.ExitEventHandler((s, e) =>
            {
                try {
                    process.Kill();
                } catch (Exception) { }
            });

            // servctl register
            servctl.RegisterDefaultService("core.default", DefaultService);
            servctl.RegisterService("core.sessopen", SessOpenService);
            servctl.RegisterService("core.sessclose", SessCloseService);
            servctl.RegisterService("core.sesssend", SessSendService);
            servctl.RegisterService("core.clientlist", ClientListService);
            servctl.RegisterService("core.clentclose", ClientCloseService);
            servctl.RegisterService("core.clientsend", ClientSendService);
            servctl.RegisterService("core.clientsendbyccid", ClientSendByCcidService);
            servctl.RegisterService("core.clientupdate", ClientUpdateService);
        }

        // Session Event ==========================================================================

        protected override void OnSessCreate(object sender, SockSess sess)
        {
            if (sess.type == SockType.accept && sess.sdata == null) {
                sess.sdata = new SessData() {
                    Ccid = "",
                    Name = "",
                };
            }
        }

        // Center Service =========================================================================

        protected override void SessSendService(ServiceRequest request, ref ServiceResponse response)
        {
            base.SessSendService(request, ref response);

            string logmsg = "(" + (request.user_data as SockSess).rep.ToString()
                + " => " + "*.*.*.*" + ")" + Environment.NewLine;
            logmsg += "\tRequest: " + (string)request.data + Environment.NewLine;
            logmsg += "\tRespond: " + JsonConvert.SerializeObject(response);

            log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType).Info(logmsg);
        }

        private void ClientListService(ServiceRequest request, ref ServiceResponse response)
        {
            // check if admin
            SockSess sess = request.user_data as SockSess;
            SessData sdata = sess.sdata as SessData;
            if (sdata == null || !sdata.Admin) return;

            // parse to dictionary
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>((string)request.data);

            StringBuilder sb = new StringBuilder();
            foreach (var item in sessctl.GetSessionTable()) {
                if (item.type != SockType.accept) continue;
                SessData sd = item.sdata as SessData;
                if (String.IsNullOrEmpty(sd.Ccid)) continue;
                sb.Append("{"
                    + "\"dev\":\"" + item.lep.Port + "\","
                    + "\"ip\":\"" + item.rep.ToString() + "\","
                    + "\"time\":\"" + item.conntime + "\","
                    + "\"ccid\":\"" + sd.Ccid + "\","
                    + "\"name\":\"" + sd.Name + "\""
                    + "}");
            }
            sb.Insert(0, '[');
            sb.Append(']');
            sb.Replace("}{", "},{");
            sb.Append("\r\n");

            response.id = dc["id"];
            response.errcode = 0;
            response.errmsg = "";
            response.data = JObject.Parse(sb.ToString());
        }

        private void ClientCloseService(ServiceRequest request, ref ServiceResponse response)
        {
            // check if admin
            SessData sdata = (request.user_data as SockSess).sdata as SessData;
            if (sdata == null || !sdata.Admin) return;

            // parse to dictionary
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>((string)request.data);

            // find session
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), int.Parse(dc["port"]));
            SockSess sess = sessctl.FindSession(SockType.accept, null, ep);

            // close session
            if (sess != null)
                sessctl.DelSession(sess);

            // write response
            response.id = dc["id"];
            if (sess != null) {
                response.errcode = 0;
                response.errmsg = "shutdown " + ep.ToString();
            } else {
                response.errcode = 1;
                response.errmsg = "cannot find " + ep.ToString();
            }
            response.data = "";
        }

        private void ClientSendService(ServiceRequest request, ref ServiceResponse response)
        {
            // check if admin
            SessData sdata = (request.user_data as SockSess).sdata as SessData;
            if (sdata == null || !sdata.Admin) return;

            // parse to dictionary
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>((string)request.data);

            // find session
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), int.Parse(dc["port"]));
            SockSess sess = sessctl.FindSession(SockType.accept, null, ep);

            // send message
            if (sess != null)
                sessctl.SendSession(sess, Encoding.UTF8.GetBytes(dc["data"]));

            // write response
            response.id = dc["id"];
            if (sess != null) {
                response.errcode = 0;
                response.errmsg = "send to " + ep.ToString();
            } else {
                response.errcode = 1;
                response.errmsg = "cannot find " + ep.ToString();
            }
            response.data = "";

            // log
            if (sess != null) {
                string logmsg = DateTime.Now + " (" + (request.user_data as SockSess).rep.ToString()
                    + " => " + sess.rep.ToString() + ")" + Environment.NewLine;
                logmsg += Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(dc["data"]));

                log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType).Info(logmsg);
            }
        }

        private void ClientSendByCcidService(ServiceRequest request, ref ServiceResponse response)
        {
            // check if admin
            SessData sdata = (request.user_data as SockSess).sdata as SessData;
            if (sdata == null || !sdata.Admin) return;

            // parse to dictionary
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>((string)request.data);

            // find session
            SockSess sess = null;
            foreach (var item in sessctl.GetSessionTable()) {
                if (item.type != SockType.accept) continue;
                SessData sd = item.sdata as SessData;
                if (sd.Ccid == dc["ccid"]) {
                    sess = item; // take last one as result, so comment "break" at next line
                    //break;
                }
            }

            // send message
            if (sess != null)
                sessctl.SendSession(sess, Encoding.UTF8.GetBytes(dc["data"]));

            // write response
            response.id = dc["id"];
            if (sess != null) {
                response.errcode = 0;
                response.errmsg = "send to " + dc["ccid"];
            } else {
                response.errcode = 1;
                response.errmsg = "cannot find " + dc["ccid"];
            }
            response.data = "";

            // log
            if (sess != null) {
                string logmsg = DateTime.Now + " (" + (request.user_data as SockSess).rep.ToString()
                    + " => " + sess.rep.ToString() + ")" + Environment.NewLine;
                logmsg += Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(dc["data"]));

                log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType).Info(logmsg);
            }
        }

        private void ClientUpdateService(ServiceRequest request, ref ServiceResponse response)
        {
            // check if admin
            SessData sdata = (request.user_data as SockSess).sdata as SessData;
            if (sdata == null || !sdata.Admin) return;

            // parse to dictionary
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>((string)request.data);

            // update sess data
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), int.Parse(dc["port"]));
            SockSess sess = sessctl.FindSession(SockType.accept, null, ep);
            if (sess != null) {
                SessData sd = sess.sdata as SessData;
                sd.Ccid = dc["ccid"];
                sd.Name = dc["name"];
            }

            // write response
            response.id = dc["id"];
            if (sess != null) {
                response.errcode = 0;
                response.errmsg = "update " + ep.ToString();
            } else {
                response.errcode = 1;
                response.errmsg = "cannot find " + ep.ToString();
            }
            response.data = "";
        }
    }
}
