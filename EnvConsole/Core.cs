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
using mnn.util;
using mnn.misc.module;
using mnn.misc.env;
using mnn.misc.service;
using EnvConsole.Unit;

namespace EnvConsole {
    class Core : CoreBase {
        public Encoding Coding { get; set; }
        public DataUI DataUI { get; set; }
        private ModuleCtl modctl;

        public Core()
        {
            // start node
            Process process = new Process();
            process.StartInfo.FileName = "node";
            process.StartInfo.Arguments = "js\\main.js";
            //process.StartInfo.CreateNoWindow = true;
            //process.StartInfo.UseShellExecute = false;
            try {
                process.Start();
            } catch (Exception) {
                System.Windows.MessageBox.Show("Start nodejs failed.");
                //Thread.CurrentThread.Abort();
            }
            System.Windows.Application.Current.Exit += new System.Windows.ExitEventHandler((s, e) =>
            {
                try {
                    process.Kill();
                } catch (Exception) { }
            });

            // init log4net
            var config = new FileInfo(AppDomain.CurrentDomain.BaseDirectory + "EnvConsole.xml");
            log4net.Config.XmlConfigurator.ConfigureAndWatch(config);

            // init fields
            Coding = Encoding.UTF8;
            DataUI = new DataUI();
            modctl = new ModuleCtl();

            // servctl register
            servctl.RegisterDefaultService("DefaultService", DefaultService);
            servctl.RegisterService("SockSendService", SockSendService, Coding.GetBytes("/center/socksend"));
            servctl.RegisterService("ClientListService", ClientListService, Coding.GetBytes("/center/clientlist"));
            servctl.RegisterService("ClientCloseService", ClientCloseService, Coding.GetBytes("/center/clientclose"));
            servctl.RegisterService("ClientSendService", ClientSendService, Coding.GetBytes("/center/clientsend"));
            servctl.RegisterService("ClientSendByCcidService", ClientSendByCcidService, Coding.GetBytes("/center/clientsendbyccid"));
            servctl.RegisterService("ClientUpdateService", ClientUpdateService, Coding.GetBytes("/center/clientupdate"));

            // load all modules from directory "DataHandles"
            if (Directory.Exists(EnvConst.Module_PATH)) {
                foreach (var item in Directory.GetFiles(EnvConst.Module_PATH)) {
                    string str = item.Substring(item.LastIndexOf("\\") + 1);
                    if (str.Contains("Module") && str.ToLower().EndsWith(".dll"))
                        ModuleLoad(item);
                }
            }
        }

        public void Config()
        {
            if (File.Exists(EnvConst.CONF_PATH) == false) {
                System.Windows.MessageBox.Show(EnvConst.CONF_NAME + ": can't find it.");
                Thread.CurrentThread.Abort();
            }

            try {
                XmlDocument xdoc = new XmlDocument();
                xdoc.Load(EnvConst.CONF_PATH);

                // Encoding Config
                XmlNode node = xdoc.SelectSingleNode(EnvConst.CONF_ENCODING);
                Coding = Encoding.GetEncoding(node.InnerText);

                // Server Config
                foreach (XmlNode item in xdoc.SelectNodes(EnvConst.CONF_SERVER)) {
                    ServerUnit server = new ServerUnit();
                    server.ID = item.Attributes["id"].Value;
                    server.Name = item.Attributes["name"].Value;
                    server.Target = (ServerTarget)Enum.Parse(typeof(ServerTarget), item.Attributes["target"].Value);
                    server.Protocol = item.Attributes["protocol"].Value;
                    server.IpAddress = item.Attributes["ipaddress"].Value;
                    server.Port = int.Parse(item.Attributes["port"].Value);
                    server.AutoRun = bool.Parse(item.Attributes["autorun"].Value);
                    server.CanStop = bool.Parse(item.Attributes["canstop"].Value);
                    server.ListenState = ServerUnit.ListenStateStoped;
                    server.TimerState = server.Target == ServerTarget.center
                        ? ServerUnit.TimerStateDisable : ServerUnit.TimerStateStoped;
                    server.TimerInterval = 0;
                    server.TimerCommand = "";
                    server.Timer = null;
                    DataUI.ServerTable.Add(server);
                }
            } catch (Exception ex) {
                log4net.ILog log = log4net.LogManager.GetLogger(typeof(Core));
                log.Error("Exception of reading configure file.", ex);
                System.Windows.MessageBox.Show(EnvConst.CONF_NAME + ": syntax error.");
            }

            // autorun
            foreach (var item in DataUI.ServerTable) {
                if (item.AutoRun) {
                    if (item.Protocol == "tcp") {
                        SockSess result = sessctl.MakeListen(new IPEndPoint(IPAddress.Parse(item.IpAddress), item.Port));
                        if (result != null)
                            item.ListenState = ServerUnit.ListenStateStarted;
                    } else if (item.Protocol == "udp") {
                    }
                }
            }
        }

        // Session Event ==========================================================================

        protected override void OnSessCreate(object sender, SockSess sess)
        {
            if (sess.type == SockType.accept) {
                sess.sdata = new SessData() {
                    Ccid = "",
                    Name = "",
                    TimeConn = DateTime.Now,
                    IsAdmin = false,
                };
                /// ** update DataUI
                DataUI.ClientAdd(sess.lep, sess.rep);
            }
        }

        protected override void OnSessDelete(object sender, SockSess sess)
        {
            /// ** update DataUI
            if (sess.type == SockType.accept)
                DataUI.ClientDel(sess.rep);
        }

        // Center Service =========================================================================

        protected override void SockSendService(ServiceRequest request, ref ServiceResponse response)
        {
            base.SockSendService(request, ref response);

            /// ** update DataUI
            if (response.data != null) {
                string log = DateTime.Now + " (" + (request.sdata as SockSess).rep.ToString() + " => " + "*.*.*.*" + ")\n";
                log += "Request: " + Coding.GetString(request.data) + "\n";
                log += "Respond: " + Coding.GetString(response.data) + "\n\n";
                DataUI.Logger(log);
            }
        }

        private bool CheckServerTargetCenter(int port)
        {
            /// ** dangerous !!! access DataUI
            var subset = from s in DataUI.ServerTable
                         where s.Target == ServerTarget.center && s.Port == port
                         select s;
            if (!subset.Any())
                return false;
            else
                return true;
        }

        private void ClientListService(ServiceRequest request, ref ServiceResponse response)
        {
            if (!CheckServerTargetCenter((request.sdata as SockSess).lep.Port)) return;

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
            // check target center
            if (!CheckServerTargetCenter((request.sdata as SockSess).lep.Port)) return;

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

            /// ** update DataUI
            if (result != null)
                DataUI.ClientDel(ep);
        }

        private void ClientSendService(ServiceRequest request, ref ServiceResponse response)
        {
            // check target center
            if (!CheckServerTargetCenter((request.sdata as SockSess).lep.Port)) return;

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

            /// ** update DataUI
            if (result != null) {
                string log = DateTime.Now + " (" + (request.sdata as SockSess).rep.ToString()
                    + " => " + result.rep.ToString() + ")\n";
                log += Coding.GetString(Coding.GetBytes(param_data)) + "\n\n";
                DataUI.Logger(log);
            }
        }

        private void ClientSendByCcidService(ServiceRequest request, ref ServiceResponse response)
        {
            // check target center
            if (!CheckServerTargetCenter((request.sdata as SockSess).lep.Port)) return;

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

            /// ** update DataUI
            if (result != null) {
                string log = DateTime.Now + " (" + (request.sdata as SockSess).rep.ToString()
                    + " => " + result.rep.ToString() + ")\n";
                log += Coding.GetString(Coding.GetBytes(param_data)) + "\n\n";
                DataUI.Logger(log);
            }
        }

        private void ClientUpdateService(ServiceRequest request, ref ServiceResponse response)
        {
            // check target center
            if (!CheckServerTargetCenter((request.sdata as SockSess).lep.Port)) return;

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

            /// ** update DataUI
            if (result != null) {
                DataUI.ClientUpdate(ep, "ID", dc["ccid"]);
                DataUI.ClientUpdate(ep, "Name", dc["name"]);
            }
        }

        // Methods ============================================================================

        public void ModuleLoad(string filePath)
        {
            ModuleNode module = null;

            try {
                module = modctl.Add(filePath);
            } catch (Exception ex) {
                System.Windows.MessageBox.Show(filePath + ": load failed.\r\n" + ex.ToString());
                return;
            }

            // 加载模块已经成功
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(filePath);
            ModuleUnit unit = new ModuleUnit();
            unit.FileName = module.AssemblyName;
            unit.FileVersion = fvi.FileVersion;
            unit.FileComment = fvi.Comments;
            unit.Module = module;

            // 加入 table
            DataUI.RwlockModuleTable.AcquireWriterLock(-1);
            DataUI.ModuleTable.Add(unit);
            DataUI.RwlockModuleTable.ReleaseWriterLock();

            // 注册处理方法
            if (module.CheckInterface(new string[] { typeof(IEnvHandler).FullName })) {
                servctl.RegisterService(module.ModuleID,
                    (ServiceRequest request, ref ServiceResponse response) =>
                    {
                        object[] args = new object[] { request, response };
                        module.Invoke(typeof(IEnvHandler).FullName, SEnvHandler.DO_HANDLER, ref args);
                        response.data = (args[1] as ServiceResponse).data;

                        /// ** update DataUI
                        string log = DateTime.Now + " (" + (request.sdata as SockSess).rep.ToString()
                            + " => " + (request.sdata as SockSess).lep.ToString() + ")\n";
                        log += Coding.GetString(request.data) + "\n\n";
                        DataUI.Logger(log);
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
        }

        public void ModuleUnload(string fileName)
        {
            DataUI.RwlockModuleTable.AcquireWriterLock(-1);

            var subset = from s in DataUI.ModuleTable where s.FileName.Equals(fileName) select s;
            if (subset.Any()) {
                ModuleNode node = subset.First().Module;
                // 注销处理方法
                if (node.CheckInterface(new string[] { typeof(IEnvHandler).FullName }))
                    servctl.DeregisterService(node.ModuleID);
                else if (node.CheckInterface(new string[] { typeof(IEnvFilter).FullName }))
                    servctl.DeregisterFilter(node.ModuleID);

                // 移出 table
                modctl.Del(node);
                DataUI.ModuleTable.Remove(subset.First());
            }

            DataUI.RwlockModuleTable.ReleaseWriterLock();
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

                /// ** update DataUI
                if (result != null)
                    DataUI.ServerStart(ip, port);
                else
                    DataUI.ServerStop(ip, port);
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

                /// ** update DataUI
                if (result != null)
                    DataUI.ServerStop(ip, port);
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
