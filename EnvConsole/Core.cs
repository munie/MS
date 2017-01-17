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
using EnvConsole.Unit;

namespace EnvConsole {
    class Core : CoreBase {
        public Encoding Coding { get; set; }

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

            // init fields
            Coding = Encoding.UTF8;

            // servctl register
            servctl.RegisterDefaultService("DefaultService", DefaultService);
            servctl.RegisterService("SockSendService", SockSendService, Coding.GetBytes("/center/socksend"));
            servctl.RegisterService("ClientListService", ClientListService, Coding.GetBytes("/center/clientlist"));
            servctl.RegisterService("ClientCloseService", ClientCloseService, Coding.GetBytes("/center/clientclose"));
            servctl.RegisterService("ClientSendService", ClientSendService, Coding.GetBytes("/center/clientsend"));
            servctl.RegisterService("ClientSendByCcidService", ClientSendByCcidService, Coding.GetBytes("/center/clientsendbyccid"));
            servctl.RegisterService("ClientUpdateService", ClientUpdateService, Coding.GetBytes("/center/clientupdate"));
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
                };
            }
        }

        // Center Service =========================================================================

        protected override void SockSendService(ServiceRequest request, ref ServiceResponse response)
        {
            base.SockSendService(request, ref response);

            if (response.data != null) {
                string logmsg = DateTime.Now + " (" + (request.user_data as SockSess).rep.ToString() + " => " + "*.*.*.*" + ")\n";
                logmsg += "Request: " + Coding.GetString(request.data) + "\n";
                logmsg += "Respond: " + Coding.GetString(response.data) + "\n\n";

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
            response.data = Coding.GetBytes(sb.ToString());
        }

        private void ClientCloseService(ServiceRequest request, ref ServiceResponse response)
        {
            // check if admin
            SockSess sess = request.user_data as SockSess;
            SessData sdata = sess.sdata as SessData;
            if (sdata == null || !sdata.IsAdmin) return;

            // get param string & parse to dictionary
            string url = Encoding.UTF8.GetString(request.data);
            if (!url.Contains('?')) return;
            string param_list = url.Substring(url.IndexOf('?') + 1);
            IDictionary<string, string> dc = SockConvert.ParseUrlQueryParam(param_list);

            // find session
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), int.Parse(dc["port"]));
            SockSess result = sessctl.FindSession(SockType.accept, null, ep);

            // close session
            if (result != null)
                sessctl.DelSession(result);

            // write response
            if (result != null)
                response.data = Coding.GetBytes("Success: shutdown " + ep.ToString() + "\r\n");
            else
                response.data = Coding.GetBytes("Failure: can't find " + ep.ToString() + "\r\n");
        }

        private void ClientSendService(ServiceRequest request, ref ServiceResponse response)
        {
            // check if admin
            SockSess sess = request.user_data as SockSess;
            SessData sdata = sess.sdata as SessData;
            if (sdata == null || !sdata.IsAdmin) return;

            // get param string & parse to dictionary
            string url = Coding.GetString(request.data);
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
            SockSess result = sessctl.FindSession(SockType.accept, null, ep);

            // send message
            if (result != null)
                sessctl.SendSession(result, Coding.GetBytes(param_data));

            // write response
            if (result != null)
                response.data = Coding.GetBytes("Success: sendto " + ep.ToString() + "\r\n");
            else
                response.data = Coding.GetBytes("Failure: can't find " + ep.ToString() + "\r\n");

            // log
            if (result != null) {
                string logmsg = DateTime.Now + " (" + (request.user_data as SockSess).rep.ToString()
                    + " => " + result.rep.ToString() + ")\n";
                logmsg += Coding.GetString(Coding.GetBytes(param_data)) + "\n\n";

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

            // get param string & parse to dictionary
            string url = Coding.GetString(request.data);
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
                sessctl.SendSession(result, Coding.GetBytes(param_data));

            // write response
            if (result != null)
                response.data = Coding.GetBytes("Success: sendto " + dc["ccid"] + "\r\n");
            else
                response.data = Coding.GetBytes("Failure: can't find " + dc["ccid"] + "\r\n");

            // log
            if (result != null) {
                string logmsg = DateTime.Now + " (" + (request.user_data as SockSess).rep.ToString()
                    + " => " + result.rep.ToString() + ")\n";
                logmsg += Coding.GetString(Coding.GetBytes(param_data)) + "\n\n";

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

            // get param string & parse to dictionary
            string url = Coding.GetString(request.data);
            if (!url.Contains('?')) return;
            string param_list = url.Substring(url.IndexOf('?') + 1);
            IDictionary<string, string> dc = SockConvert.ParseUrlQueryParam(param_list);

            // update sess data
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), int.Parse(dc["port"]));
            SockSess result = sessctl.FindSession(SockType.accept, null, ep);
            if (result != null) {
                SessData sd = result.sdata as SessData;
                sd.Ccid = dc["ccid"];
                sd.Name = dc["name"];
            }

            // write response
            if (result != null)
                response.data = Coding.GetBytes("Success: update " + ep.ToString() + "\r\n");
            else
                response.data = Coding.GetBytes("Failure: can't find " + ep.ToString() + "\r\n");
        }

        // Interface ==============================================================================

        public bool ModuleLoad(string filePath)
        {
            ModuleNode module = null;

            try {
                module = modctl.Add(filePath);
            } catch (Exception ex) {
                System.Windows.MessageBox.Show(filePath + ": load failed.\r\n" + ex.ToString());
                return false;
            }

            // 注册处理方法
            if (module.CheckInterface(new string[] { typeof(IEnvHandler).FullName })) {
                servctl.RegisterService(module.ModuleID,
                    (ServiceRequest request, ref ServiceResponse response) =>
                    {
                        object[] args = new object[] { request, response };
                        module.Invoke(typeof(IEnvHandler).FullName, SEnvHandler.DO_HANDLER, ref args);
                        response.data = (args[1] as ServiceResponse).data;

                        // log
                        string logmsg = DateTime.Now + " (" + (request.user_data as SockSess).rep.ToString()
                            + " => " + (request.user_data as SockSess).lep.ToString() + ")\n";
                        logmsg += Coding.GetString(request.data) + "\n\n";
                        log4net.ILog logger = log4net.LogManager.GetLogger(typeof(Core));
                        logger.Info(logmsg);
                    },
                    Coding.GetBytes(module.ModuleID));
            } else if (module.CheckInterface(new string[] { typeof(IEnvFilter).FullName })) {
                servctl.RegisterFilter(module.ModuleID,
                    (ref ServiceRequest request, ServiceResponse response) =>
                    {
                        object[] args = new object[] { request, response };
                        bool retval = (bool)module.Invoke(typeof(IEnvFilter).FullName, SEnvFilter.DO_FILTER, ref args);
                        request.SetData((args[0] as ServiceRequest).data);
                        return retval;
                    });
            }

            return true;
        }

        public bool ModuleUnload(string fileName)
        {
            ModuleNode node = modctl.FindModule(fileName);
            if (node == null) return false;

            // 注销处理方法
            if (node.CheckInterface(new string[] { typeof(IEnvHandler).FullName }))
                servctl.DeregisterService(node.ModuleID);
            else if (node.CheckInterface(new string[] { typeof(IEnvFilter).FullName }))
                servctl.DeregisterFilter(node.ModuleID);

            // 移出 table
            modctl.Del(node);

            return true;
        }

        public void ServerStart(string ip, int port, string protocol = "tcp")
        {
            // only handle tcp
            if (protocol != "tcp") return;

            sessctl.BeginInvoke(new Action(() =>
            {
                // define variables
                IPEndPoint ep = new IPEndPoint(IPAddress.Parse(ip), port);
                SockSess result = null;

                // make listen
                if (sessctl.FindSession(SockType.listen, ep, null) == null)
                    result = sessctl.MakeListen(ep);
                else
                    return;
            }));
        }

        public void ServerStop(string ip, int port, string protocol = "tcp")
        {
            // only handle tcp
            if (protocol != "tcp") return;

            sessctl.BeginInvoke(new Action(() =>
            {
                // define variables
                IPEndPoint ep = new IPEndPoint(IPAddress.Parse(ip), port);
                SockSess result = null;

                // find and delete session
                result = sessctl.FindSession(SockType.listen, ep, null);
                if (result != null)
                    sessctl.DelSession(result);
            }));
        }

        public void ServerTimerStart(ServerUnit server)
        {
            server.Timer = new System.Timers.Timer(server.TimerInterval * 1000);
            // limbda 不会锁住DataUI.ServerTable
            server.Timer.Elapsed += new System.Timers.ElapsedEventHandler((s, ea) =>
                sessctl.BeginInvoke(new Action(() =>
                {
                    // define variables
                    IPEndPoint ep = new IPEndPoint(IPAddress.Parse(server.IpAddress), server.Port);
                    SockSess result = null;

                    // find and send msg to session
                    result = sessctl.FindSession(SockType.listen, ep, null);
                    if (result != null)
                        sessctl.SendSession(result, Coding.GetBytes(server.TimerCommand));
                }))
            );

            server.Timer.Start();
            server.TimerState = ServerUnit.TimerStateStarted;
        }

        public void ServerTimerStop(ServerUnit server)
        {
            server.Timer.Stop();
            server.Timer.Close();
            server.TimerState = ServerUnit.TimerStateStoped;
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
                    sessctl.SendSession(result, Coding.GetBytes(msg));
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
