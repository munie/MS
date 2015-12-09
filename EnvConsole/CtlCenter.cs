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
using mnn.net;
using mnn.net.deprecated;
using mnn.util;
using mnn.misc.module;
using mnn.misc.env;
using EnvConsole.Unit;

namespace EnvConsole
{
    class CtlCenter : CtlCenterBase
    {
        public static readonly string BASE_DIR = System.AppDomain.CurrentDomain.BaseDirectory;
        public static readonly string CONF_NAME = "EnvConsole.xml";
        public static readonly string CONF_PATH = BASE_DIR + CONF_NAME;
        public static readonly string Module_PATH = BASE_DIR + "Modules";
        public static readonly string PACK_PARSE = "PackParse";
        public Encoding Coding { get; set; }
        public DataUI DataUI { get; set; }
        private ModuleCtl modctl;

        public CtlCenter()
        {
            // init fields
            Coding = Encoding.UTF8;
            DataUI = new DataUI();
            modctl = new ModuleCtl();

            // dispatcher register
            dispatcher = new Dispatcher();
            dispatcher.RegisterDefaultService("default_service", default_service);
            dispatcher.Register("client_list_service", client_list_service, Coding.GetBytes("/center/clientlist"));
            dispatcher.Register("client_close_service", client_close_service, Coding.GetBytes("/center/clientclose"));
            dispatcher.Register("client_send_service", client_send_service, Coding.GetBytes("/center/clientsend"));
            dispatcher.Register("client_send_by_ccid_service", client_send_by_ccid_service, Coding.GetBytes("/center/clientsendbyccid"));
            dispatcher.Register("client_update_service", client_update_service, Coding.GetBytes("/center/clientupdate"));

            // load all modules from directory "DataHandles"
            if (Directory.Exists(Module_PATH)) {
                foreach (var item in Directory.GetFiles(Module_PATH)) {
                    string str = item.Substring(item.LastIndexOf("\\") + 1);
                    if (str.Contains("Module") && str.ToLower().EndsWith(".dll"))
                        ModuleLoad(item);
                }
            }

            // cmtctl ...
            InitPackParse();
        }

        public void Config()
        {
            if (File.Exists(CONF_PATH) == false) {
                System.Windows.MessageBox.Show(CONF_NAME + ": can't find it.");
                Thread.CurrentThread.Abort();
            }

            try {
                XmlDocument xdoc = new XmlDocument();
                xdoc.Load(CONF_PATH);

                // coding Config
                XmlNode node = xdoc.SelectSingleNode("/configuration/encoding");
                Coding = Encoding.GetEncoding(node.InnerText);

                // Server Config
                foreach (XmlNode item in xdoc.SelectNodes("/configuration/servers/server")) {
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
                mnn.util.Logger.WriteException(ex);
                System.Windows.MessageBox.Show(CONF_NAME + ": syntax error.");
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

        public void Perform(int next)
        {
            sessctl.Perform(next);
        }

        // Override Session Event ==========================================================================

        protected override void sess_create(object sender, SockSess sess)
        {
            if (sess.type == SockType.accept) {
                sess.sdata = new SessData() {
                    Ccid = "",
                    Name = "",
                    TimeConn = DateTime.Now,
                    IsAdmin = checkServerTargetCenter(sess.lep.Port),
                };
                /// ** update DataUI
                DataUI.ClientAdd(sess.lep, sess.rep);
            }
        }

        protected override void sess_delete(object sender, SockSess sess)
        {
            /// ** update DataUI
            if (sess.type == SockType.accept)
                DataUI.ClientDel(sess.rep);
        }

        // Request Controller =========================================================================

        private AtCmdCtl cmdctl;
        private void InitPackParse()
        {
            cmdctl = new AtCmdCtl();
            cmdctl.Register(PACK_PARSE, PackParse);
            Thread thread = new Thread(() => { while (true) cmdctl.Perform(1000); });
            thread.IsBackground = true;
            thread.Start();
        }
        private void PackParse(object[] args)
        {
            SockRequest request = args[0] as SockRequest;
            string content = Coding.GetString(request.data);
            DataUI.PackParsed();

            // 加锁
            DataUI.RwlockModuleTable.AcquireReaderLock(-1);

            // 调用模块处理消息
            foreach (var item in DataUI.ModuleTable) {
                if (content.Contains(item.Module.ModuleID)) {
                    try {
                        item.Module.Invoke(typeof(IEnvHandler).FullName, SEnvHandler.HANDLE_MSG, ref args);
                    } catch (Exception) { }
                    goto _out;
                }
            }

            // 如果没有得到处理，尝试翻译模块处理
            var subset = from s in DataUI.ModuleTable where s.Module.CheckInterface(new string[] { typeof(IEnvTranslate).FullName }) select s;
            if (subset.Count() == 0) goto _out;
            ModuleNode node = subset.First().Module as ModuleNode;
            try {
                object[] tmp = new object[] { content };
                content = (string)node.Invoke(typeof(IEnvTranslate).FullName, SMsgTrans.TRANSLATE, ref tmp);
                if (string.IsNullOrEmpty(content))
                    goto _out;
                request.data = Encoding.UTF8.GetBytes(content);
            } catch (Exception) {
                goto _out;
            }

            // 再次调用模块处理消息
            foreach (var item in DataUI.ModuleTable) {
                if (content.Contains(item.Module.ModuleID)) {
                    try {
                        item.Module.Invoke(typeof(IEnvHandler).FullName, SEnvHandler.HANDLE_MSG, ref args);
                    } catch (Exception) { }
                    goto _out;
                }
            }

        _out:
            // 解锁
            DataUI.RwlockModuleTable.ReleaseReaderLock();
            SockResponse response = args[1] as SockResponse;
            if (response.data != null && response.data.Length > 0) {
                sessctl.BeginInvoke(new Action(() => {
                    SockSess result = sessctl.FindSession(SockType.accept, request.lep, request.rep);
                    if (result != null)
                        sessctl.SendSession(result, response.data);
                }));
            }

            //bool IsHandled = false;
            //DataUI.RwlockModuleTable.AcquireReaderLock(-1);
            //foreach (var item in DataUI.ModuleTable) {
            //    // 水库代码太恶心，没办法的办法
            //    if (item.Module.ModuleID != "HT=" && pack.Content.Contains(item.Module.ModuleID)) {
            //        try {
            //            item.Module.Invoke(SMsgProc.FULL_NAME, SMsgProc.HANDLE_MSG, new object[] { pack.EP, pack.Content });
            //        } catch (Exception) { }
            //        IsHandled = true;
            //        break;
            //    }
            //}
            //// 水库代码太恶心，没办法的办法
            //if (IsHandled == false) {
            //    foreach (var item in DataUI.ModuleTable) {
            //        if (item.Module.ModuleID == "HT=" && pack.Content.Contains(item.Module.ModuleID)) {
            //            try {
            //                item.Module.Invoke(SMsgProc.FULL_NAME, SMsgProc.HANDLE_MSG, new object[] { pack.EP, pack.Content });
            //            } catch (Exception) { }
            //            break;
            //        }
            //    }
            //}
            //DataUI.RwlockModuleTable.ReleaseReaderLock();
        }

        private bool checkServerTargetCenter(int port)
        {
            /// ** dangerous !!! access DataUI
            var subset = from s in DataUI.ServerTable
                         where s.Target == ServerTarget.center && s.Port == port
                         select s;
            if (subset.Count() == 0)
                return false;
            else
                return true;
        }

        private void default_service(SockRequest request, SockResponse response)
        {
            string log = DateTime.Now + " (" + request.rep.ToString() + " => " + request.lep.ToString() + ")\n";
            log += Coding.GetString(request.data) + "\n\n";

            /// ** update DataUI
            DataUI.Logger(log);
            DataUI.PackRecved();

            cmdctl.AppendCommand(PACK_PARSE, new object[] { request, response });

            foreach (var item in sessctl.GetSessionTable()) {
                SessData sd = item.sdata as SessData;
                if (sd.IsAdmin)
                    sessctl.SendSession(item, request.data);
            }
        }

        private void client_list_service(SockRequest request, SockResponse response)
        {
            if (!checkServerTargetCenter(request.lep.Port)) return;

            StringBuilder sb = new StringBuilder();
            foreach (var item in sessctl.GetSessionTable()) {
                if (item.type != SockType.accept) continue;
                SessData sd = item.sdata as SessData;
                if (String.IsNullOrEmpty(sd.Ccid)) continue;
                sb.Append("{"
                    + "\"dev\":\"" + item.lep.Port + "\""
                    + "\"ip\":\"" + item.rep.ToString() + "\""
                    + "\"time\":\"" + sd.TimeConn + "\""
                    + "\"ccid\":\"" + sd.Ccid + "\""
                    + "\"name\":\"" + sd.Name + "\""
                    + "}");
            }
            sb.Insert(0, '[');
            sb.Append(']');
            sb.Replace("}{", "},{");
            response.data = Coding.GetBytes(sb.ToString());
        }

        private void client_close_service(SockRequest request, SockResponse response)
        {
            // check target center
            if (!checkServerTargetCenter(request.lep.Port)) return;

            // get param string & parse to dictionary
            string msg = Encoding.UTF8.GetString(request.data);
            if (!msg.Contains('?')) return;
            msg = msg.Substring(msg.IndexOf('?') + 1);
            IDictionary<string, string> dc = SockConvert.ParseHttpQueryParam(msg);

            // find session and close
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), int.Parse(dc["port"]));
            SockSess result = sessctl.FindSession(SockType.accept, null, ep);

            if (result != null)
                sessctl.DelSession(result);

            /// ** update DataUI
            if (result != null)
                DataUI.ClientDel(ep);

            // respond
            if (result != null)
                response.data = Coding.GetBytes("OK");
            else
                response.data = Coding.GetBytes("no such client");
        }

        private void client_send_service(SockRequest request, SockResponse response)
        {
            // check target center
            if (!checkServerTargetCenter(request.lep.Port)) return;

            // get param string & parse to dictionary
            string msg = Coding.GetString(request.data);
            if (!msg.Contains('?')) return;
            msg = msg.Substring(msg.IndexOf('?') + 1);
            IDictionary<string, string> dc = SockConvert.ParseHttpQueryParam(msg);

            // find session and send message
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), int.Parse(dc["port"]));
            SockSess result = sessctl.FindSession(SockType.accept, null, ep);

            if (result != null)
                sessctl.SendSession(result, Coding.GetBytes(dc["data"]));

            if (result != null)
                response.data = Coding.GetBytes("OK");
            else
                response.data = Coding.GetBytes("no such client");
        }

        private void client_send_by_ccid_service(SockRequest request, SockResponse response)
        {
            // check target center
            if (!checkServerTargetCenter(request.lep.Port)) return;

            // get param string & parse to dictionary
            string msg = Coding.GetString(request.data);
            if (!msg.Contains('?')) return;
            msg = msg.Substring(msg.IndexOf('?') + 1);
            IDictionary<string, string> dc = SockConvert.ParseHttpQueryParam(msg);

            // find session and send message
            SockSess result = null;
            foreach (var item in sessctl.GetSessionTable()) {
                if (item.type != SockType.accept) continue;
                SessData sd = item.sdata as SessData;
                if (sd.Ccid == dc["ccid"]) {
                    result = item;
                    break;
                }
            }

            if (result != null)
                sessctl.SendSession(result, Coding.GetBytes(dc["data"]));

            if (result != null)
                response.data = Coding.GetBytes("OK");
            else
                response.data = Coding.GetBytes("no such client");
        }

        private void client_update_service(SockRequest request, SockResponse response)
        {
            // check target center
            if (!checkServerTargetCenter(request.lep.Port)) return;

            // get param string & parse to dictionary
            string msg = Coding.GetString(request.data);
            if (!msg.Contains('?')) return;
            msg = msg.Substring(msg.IndexOf('?') + 1);
            IDictionary<string, string> dc = SockConvert.ParseHttpQueryParam(msg);

            // update sess data
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), int.Parse(dc["port"]));
            SockSess sess = sessctl.FindSession(SockType.accept, null, ep);
            if (sess != null) {
                SessData sd = sess.sdata as SessData;
                sd.Ccid = dc["ccid"];
                sd.Name = dc["name"];
            }

            /// ** update DataUI
            DataUI.ClientUpdate(ep, "ID", dc["ccid"]);
            DataUI.ClientUpdate(ep, "Name", dc["name"]);
        }

        // Methods ============================================================================

        public void ModuleLoad(string filePath)
        {
            ModuleNode module = modctl.Add(filePath);
            if (module == null) {
                System.Windows.MessageBox.Show(filePath + ": load failed.");
                return;
            }
            // 如果是消息处理模块，必须实现消息处理接口，否则加载失败
            if (module.ModuleID.IndexOf("HT=") != -1 && !module.CheckInterface(new string[] { typeof(IEnvHandler).FullName })) {
                modctl.Del(module);
                System.Windows.MessageBox.Show(filePath + ": load failed.");
                return;
            }

            // 加载模块已经成功
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(filePath);
            ModuleUnit unit = new ModuleUnit();
            unit.FileName = module.AssemblyName;
            unit.FileComment = fvi.Comments;
            unit.Module = module;

            // 加入 table
            DataUI.RwlockModuleTable.AcquireWriterLock(-1);
            DataUI.ModuleTable.Add(unit);
            DataUI.RwlockModuleTable.ReleaseWriterLock();
        }

        public void ModuleUnload(string fileName)
        {
            DataUI.RwlockModuleTable.AcquireWriterLock(-1);

            var subset = from s in DataUI.ModuleTable where s.FileName.Equals(fileName) select s;
            if (subset.Count() != 0) {
                // 移出 table
                modctl.Del(subset.First().Module);
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
            {
                sessctl.BeginInvoke(new Action(() =>
                {
                    // define variables
                    IPEndPoint ep = new IPEndPoint(IPAddress.Parse(server.IpAddress), server.Port);
                    SockSess result = null;

                    // find and send msg to session
                    result = sessctl.FindSession(SockType.listen, ep, null);
                    if (result != null)
                        sessctl.SendSession(result, Coding.GetBytes(server.TimerCommand));
                }));
            });
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
