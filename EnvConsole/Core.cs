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
using mnn.design;
using mnn.net;
using mnn.misc.env;
using mnn.misc.service;
using mnn.misc.module;
using Newtonsoft.Json;

namespace EnvConsole {
    class Core : CoreBase {
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
                log4net.ILog log = log4net.LogManager.GetLogger(typeof(Core));
                log.Error("Start nodejs failed.", ex);
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
                    TimeConn = DateTime.Now,
                    IsAdmin = false,
                    Timer = null,
                };
            }
        }

        // Center Service =========================================================================

        protected override void SessSendService(ServiceRequest request, ref ServiceResponse response)
        {
            base.SessSendService(request, ref response);

            if (response.raw_data != null) {
                string logmsg = "(" + (request.user_data as SockSess).rep.ToString()
                    + " => " + "*.*.*.*" + ")" + Environment.NewLine;
                logmsg += "\tRequest: " + Encoding.UTF8.GetString(request.raw_data) + Environment.NewLine;
                logmsg += "\tRespond: " + Encoding.UTF8.GetString(response.raw_data);

                log4net.ILog logger = log4net.LogManager.GetLogger(typeof(Core));
                logger.Info(logmsg);
            }
        }

        private void ClientListService(ServiceRequest request, ref ServiceResponse response)
        {
            // check if admin
            SockSess sess = request.user_data as SockSess;
            SessData sdata = sess.sdata as SessData;
            if (sdata == null || !sdata.IsAdmin) return;

            StringBuilder sb = new StringBuilder();
            foreach (var item in sessctl.GetSessionTable()) {
                if (item.type != SockType.accept) continue;
                SessData sd = item.sdata as SessData;
                if (String.IsNullOrEmpty(sd.Ccid)) continue;
                sb.Append("{"
                    + "\"dev\":\"" + item.lep.Port + "\","
                    + "\"ip\":\"" + item.rep.ToString() + "\","
                    + "\"time\":\"" + sd.TimeConn + "\","
                    + "\"ccid\":\"" + sd.Ccid + "\","
                    + "\"name\":\"" + sd.Name + "\""
                    + "}");
            }
            sb.Insert(0, '[');
            sb.Append(']');
            sb.Replace("}{", "},{");
            sb.Append("\r\n");
            response.raw_data = Encoding.UTF8.GetBytes(sb.ToString());
        }

        private void ClientCloseService(ServiceRequest request, ref ServiceResponse response)
        {
            // check if admin
            SockSess sess = request.user_data as SockSess;
            SessData sdata = sess.sdata as SessData;
            if (sdata == null || !sdata.IsAdmin) return;

            // parse to dictionary
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>(Encoding.UTF8.GetString(request.raw_data));

            // find session
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), int.Parse(dc["port"]));
            SockSess result = sessctl.FindSession(SockType.accept, null, ep);

            // close session
            if (result != null)
                sessctl.DelSession(result);

            // write response
            response.content = new BaseContent() { id = dc["id"] };
            if (result != null) {
                response.content.errcode = 0;
                response.content.errmsg = "shutdown " + ep.ToString();
            } else {
                response.content.errcode = 1;
                response.content.errmsg = "cannot find " + ep.ToString();
            }
            response.raw_data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response.content));
        }

        private void ClientSendService(ServiceRequest request, ref ServiceResponse response)
        {
            // check if admin
            SockSess sess = request.user_data as SockSess;
            SessData sdata = sess.sdata as SessData;
            if (sdata == null || !sdata.IsAdmin) return;

            // parse to dictionary
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>(Encoding.UTF8.GetString(request.raw_data));

            // find session
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), int.Parse(dc["port"]));
            SockSess result = sessctl.FindSession(SockType.accept, null, ep);

            // send message
            if (result != null)
                sessctl.SendSession(result, Encoding.UTF8.GetBytes(dc["data"]));

            // write response
            response.content = new BaseContent() { id = dc["id"] };
            if (result != null) {
                response.content.errcode = 0;
                response.content.errmsg = "send to " + ep.ToString();
            } else {
                response.content.errcode = 1;
                response.content.errmsg = "cannot find " + ep.ToString();
            }
            response.raw_data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response.content));

            // log
            if (result != null) {
                string logmsg = DateTime.Now + " (" + (request.user_data as SockSess).rep.ToString()
                    + " => " + result.rep.ToString() + ")" + Environment.NewLine;
                logmsg += Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(dc["data"]));

                log4net.ILog logger = log4net.LogManager.GetLogger(typeof(Core));
                logger.Info(logmsg);
            }
        }

        private void ClientSendByCcidService(ServiceRequest request, ref ServiceResponse response)
        {
            // check if admin
            SockSess sess = request.user_data as SockSess;
            SessData sdata = sess.sdata as SessData;
            if (sdata == null || !sdata.IsAdmin) return;

            // parse to dictionary
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>(Encoding.UTF8.GetString(request.raw_data));

            // find session
            SockSess result = null;
            foreach (var item in sessctl.GetSessionTable()) {
                if (item.type != SockType.accept) continue;
                SessData sd = item.sdata as SessData;
                if (sd.Ccid == dc["ccid"]) {
                    result = item; // take last one as result, so comment "break" at next line
                    //break;
                }
            }

            // send message
            if (result != null)
                sessctl.SendSession(result, Encoding.UTF8.GetBytes(dc["data"]));

            // write response
            response.content = new BaseContent() { id = dc["id"] };
            if (result != null) {
                response.content.errcode = 0;
                response.content.errmsg = "send to " + dc["ccid"];
            } else {
                response.content.errcode = 1;
                response.content.errmsg = "cannot find " + dc["ccid"];
            }
            response.raw_data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response.content));

            // log
            if (result != null) {
                string logmsg = DateTime.Now + " (" + (request.user_data as SockSess).rep.ToString()
                    + " => " + result.rep.ToString() + ")" + Environment.NewLine;
                logmsg += Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(dc["data"]));

                log4net.ILog logger = log4net.LogManager.GetLogger(typeof(Core));
                logger.Info(logmsg);
            }
        }

        private void ClientUpdateService(ServiceRequest request, ref ServiceResponse response)
        {
            // check if admin
            SockSess sess = request.user_data as SockSess;
            SessData sdata = sess.sdata as SessData;
            if (sdata == null || !sdata.IsAdmin) return;

            // parse to dictionary
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>(Encoding.UTF8.GetString(request.raw_data));

            // update sess data
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), int.Parse(dc["port"]));
            SockSess result = sessctl.FindSession(SockType.accept, null, ep);
            if (result != null) {
                SessData sd = result.sdata as SessData;
                sd.Ccid = dc["ccid"];
                sd.Name = dc["name"];
            }

            // write response
            response.content = new BaseContent() { id = dc["id"] };
            if (result != null) {
                response.content.errcode = 0;
                response.content.errmsg = "update " + ep.ToString();
            } else {
                response.content.errcode = 1;
                response.content.errmsg = "cannot find " + ep.ToString();
            }
            response.raw_data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response.content));
        }
    }
}
