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

        // Interface ==============================================================================

        public bool ModuleLoad(string filepath)
        {
            Module module = null;

            try {
                module = modctl.Add(filepath);
            } catch (Exception ex) {
                log4net.ILog logger = log4net.LogManager.GetLogger(typeof(Core));
                logger.Info(filepath + ": load failed.", ex);
                return false;
            }

            // get services and filters
            object[] nil_args = new object[0];
            object servtab = module.Invoke(typeof(IModuleService).FullName, IModuleServiceSymbols.GET_SERVICE_TABLE, ref nil_args);
            object filttab = module.Invoke(typeof(IModuleService).FullName, IModuleServiceSymbols.GET_FILTER_TABLE, ref nil_args);

            // register services
            foreach (var item in servtab as IDictionary<string, string>) {
                var __item = item; // I dislike closure here
                if (!module.CheckMethod(__item.Value, typeof(ServiceDelegate).GetMethod("Invoke"))) {
                    log4net.ILog logger = log4net.LogManager.GetLogger(typeof(Core));
                    logger.Warn(String.Format("can't found {0} in {1}", __item.Value, module.ToString()));
                    continue;
                }
                servctl.RegisterService(__item.Key,
                    (ServiceRequest request, ref ServiceResponse response) =>
                    {
                        object swap = request.user_data;
                        request.user_data = null;

                        object[] args = new object[] { request, response };
                        module.Invoke(__item.Value, ref args);
                        response.raw_data = (args[1] as ServiceResponse).raw_data;

                        request.user_data = swap;

                        // log
                        string logmsg = DateTime.Now + " (" + (request.user_data as SockSess).rep.ToString()
                            + " => " + (request.user_data as SockSess).lep.ToString() + ")" + Environment.NewLine;
                        logmsg += Encoding.UTF8.GetString(request.raw_data);
                        log4net.ILog logger = log4net.LogManager.GetLogger(typeof(Core));
                        logger.Info(logmsg);
                    });
            }

            // register filters
            foreach (var item in filttab as IDictionary<string, string>) {
                var __item = item; // I dislike closure here
                if (!module.CheckMethod(__item.Value, typeof(FilterDelegate).GetMethod("Invoke"))) {
                    log4net.ILog logger = log4net.LogManager.GetLogger(typeof(Core));
                    logger.Warn(String.Format("can't found {0} in {1}", __item.Value, module.ToString()));
                    continue;
                }
                servctl.RegisterFilter(__item.Key,
                    (ref ServiceRequest request) =>
                    {
                        object swap = request.user_data;
                        request.user_data = null;

                        object[] args = new object[] { request };
                        bool retval = (bool)module.Invoke(__item.Value, ref args);
                        request.raw_data = (args[0] as ServiceRequest).raw_data;

                        request.user_data = swap;
                        return retval;
                    });
            }

            return true;
        }

        public bool ModuleUnload(string filename)
        {
            Module module = modctl.GetModule(filename);
            if (module == null) return false;

            // get services and filters
            object[] nil_args = new object[0];
            object servtab = module.Invoke(typeof(IModuleService).FullName, IModuleServiceSymbols.GET_SERVICE_TABLE, ref nil_args);
            object filttab = module.Invoke(typeof(IModuleService).FullName, IModuleServiceSymbols.GET_FILTER_TABLE, ref nil_args);

             // deregister service
            foreach (var item in servtab as IDictionary<string, string>)
                servctl.DeregisterService(item.Key);

             // deregister filters
            foreach (var item in filttab as IDictionary<string, string>)
                servctl.DeregisterFilter(item.Key);

            // remove from table
            modctl.Del(module);

            return true;
        }

        public void ServerStart(string ip, int port, string protocol = "tcp")
        {
            // TODO: currently only handle tcp
            if (protocol != "tcp") return;

            sessctl.BeginInvoke(new Action(() =>
            {
                // define variables
                IPEndPoint ep = new IPEndPoint(IPAddress.Parse(ip), port);

                // make listen
                SockSess result = sessctl.FindSession(SockType.listen, ep, null);
                if (result == null)
                    result = sessctl.MakeListen(ep);
            }));
        }

        public void ServerStop(string ip, int port, string protocol = "tcp")
        {
            // TODO: currently only handle tcp
            if (protocol != "tcp") return;

            sessctl.BeginInvoke(new Action(() =>
            {
                // define variables
                IPEndPoint ep = new IPEndPoint(IPAddress.Parse(ip), port);

                // find and delete session
                SockSess result = sessctl.FindSession(SockType.listen, ep, null);
                if (result != null)
                    sessctl.DelSession(result);
            }));
        }

        public void ServerTimerStart(string ip, int port, double interval, string command)
        {
            sessctl.BeginInvoke(new Action(() =>
            {
                // define variables
                IPEndPoint ep = new IPEndPoint(IPAddress.Parse(ip), port);

                // find and delete session
                SockSess result = sessctl.FindSession(SockType.listen, ep, null);
                if (result != null) {
                    SessData sdata = result.sdata as SessData;
                    if (sdata == null) return;

                    if (sdata.Timer != null)
                        sdata.Timer.Close();
                    sdata.Timer = new System.Timers.Timer(interval * 1000);
                    sdata.Timer.Elapsed += new System.Timers.ElapsedEventHandler((s, ea) =>
                        sessctl.BeginInvoke(new Action(() =>
                        {
                            sessctl.SendSession(result, Encoding.UTF8.GetBytes(command));
                        }))
                    );
                    sdata.Timer.Start();
                }
            }));
        }

        public void ServerTimerStop(string ip, int port, double interval, string command)
        {
            sessctl.BeginInvoke(new Action(() =>
            {
                // define variables
                IPEndPoint ep = new IPEndPoint(IPAddress.Parse(ip), port);

                // find and delete session
                SockSess result = sessctl.FindSession(SockType.listen, ep, null);
                if (result != null) {
                    SessData sdata = result.sdata as SessData;
                    if (sdata == null || sdata.Timer == null) return;

                    sdata.Timer.Stop();
                    sdata.Timer.Close();
                }
            }));
        }

        public void ClientSendMessage(string ip, int port, string msg)
        {
            sessctl.BeginInvoke(new Action(() =>
            {
                // define variables
                IPEndPoint ep = new IPEndPoint(IPAddress.Parse(ip), port);
                SockSess result = null;

                // find and send msg to session
                result = sessctl.FindSession(SockType.accept, null, ep);
                if (result != null)
                    sessctl.SendSession(result, Encoding.UTF8.GetBytes(msg));
            }));
        }

        public void ClientClose(string ip, int port)
        {
            sessctl.BeginInvoke(new Action(() =>
            {
                // define variables
                IPEndPoint ep = new IPEndPoint(IPAddress.Parse(ip), port);
                SockSess result = null;

                // find and delete session
                result = sessctl.FindSession(SockType.accept, null, ep);
                if (result != null)
                    sessctl.DelSession(result);
            }));
        }
    }
}
