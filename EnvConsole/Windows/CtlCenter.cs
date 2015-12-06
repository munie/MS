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

namespace EnvConsole.Windows
{
    class CtlCenter : ControlCenter
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
            dispatcher.RegisterDefaultController("default_controller", default_controller);
            //dispatcher.Register("sock_open_controller", sock_open_controller, 0x0C01);
            //dispatcher.Register("sock_close_controller", sock_close_controller, 0x0C02);
            //dispatcher.Register("sock_send_controller", sock_send_controller, 0x0C03);

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
                    server.Target = item.Attributes["target"].Value;
                    server.Protocol = item.Attributes["protocol"].Value;
                    server.IpAddress = item.Attributes["ipaddress"].Value;
                    server.Port = int.Parse(item.Attributes["port"].Value);
                    server.AutoRun = bool.Parse(item.Attributes["autorun"].Value);
                    server.CanStop = bool.Parse(item.Attributes["canstop"].Value);
                    server.ListenState = ServerUnit.ListenStateStoped;
                    server.TimerState = server.Target == "center" ? ServerUnit.TimerStateDisable : ServerUnit.TimerStateStoped;
                    server.TimerInterval = 0;
                    server.TimerCommand = "";
                    server.SockServer = null;
                    server.Timer = null;
                    DataUI.ServerTable.Add(server);
                    if (server.Protocol == "udp") {
                        server.SockServer = new UdpServer();
                        server.SockServer.ClientRecvPack += AtCmdServer_ClientRecvPack;
                        server.SockServer.ClientSendPack += AtCmdServer_ClientSendPack;
                    }
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
                        try {
                            item.SockServer.Start(new IPEndPoint(IPAddress.Parse(item.IpAddress), item.Port));
                            item.ListenState = ServerUnit.ListenStateStarted;
                        } catch (Exception ex) {
                            System.Windows.MessageBox.Show(ex.Message, "Error");
                        }
                    }
                }
            }
        }

        public void Perform(int next)
        {
            sessctl.Perform(next);
        }

        // SockServer =========================================================================

        private void AtCmdServer_ClientRecvPack(object sender, ClientEventArgs e)
        {
            AtCommand atCmd = null;

            try {
                using (MemoryStream memory = new MemoryStream(e.Data)) {
                    XmlSerializer xmlFormat = new XmlSerializer(typeof(AtCommand));
                    atCmd = xmlFormat.Deserialize(memory) as AtCommand;
                }
            } catch (Exception ex) {
                mnn.util.Logger.WriteException(ex);
                return;
            }

            AtCmdServer_ExecCommand(atCmd);

            // 打印至窗口，写命令日志
            string logFormat = e.RemoteEP.ToString() + " " + DateTime.Now.ToString() + "接收命令："
                + "|FromID=" + atCmd.FromID.ToString()
                + "|ToID=" + atCmd.ToID
                + "|ToEP=" + atCmd.ToEP
                + "|DataType=" + atCmd.DataType.ToString()
                + "|Data=" + atCmd.Data;
            mnn.util.Logger.Write(logFormat);
            DataUI.Logger(logFormat);
        }

        private void AtCmdServer_ClientSendPack(object sender, ClientEventArgs e)
        {
            AtCommand atCmd = null;

            try {
                using (MemoryStream memory = new MemoryStream(e.Data)) {
                    XmlSerializer xmlFormat = new XmlSerializer(typeof(AtCommand));
                    atCmd = xmlFormat.Deserialize(memory) as AtCommand;
                }
            } catch (Exception ex) {
                mnn.util.Logger.WriteException(ex);
                return;
            }

            // 打印至窗口，写命令日志
            string logFormat = e.RemoteEP.ToString() + " " + DateTime.Now.ToString() + "发送命令："
                + "|FromID=" + atCmd.FromID.ToString()
                + "|ToID=" + atCmd.ToID
                + "|ToEP=" + atCmd.ToEP
                + "|DataType=" + atCmd.DataType.ToString()
                + "|Data=" + atCmd.Data;
            mnn.util.Logger.Write(logFormat);
            DataUI.Logger(logFormat);
        }

        private void AtCmdServer_ExecCommand(AtCommand atCmd)
        {
            string[] strTmp = atCmd.ToEP.Split(':');
            IPEndPoint toep = new IPEndPoint(IPAddress.Parse(strTmp[0]), Convert.ToInt32(strTmp[1]));

            if (atCmd.ToSchema == UnitSchema.Client && atCmd.Direct == AtCommandDirect.Request) {
                // update DataUI => client's id
                if (atCmd.DataType == AtCommandDataType.ClientUpdateID)
                    DataUI.ClientUpdate(toep, "ID", atCmd.Data);
                // update DataUI => client's name
                else if (atCmd.DataType == AtCommandDataType.ClientUpdateName)
                    DataUI.ClientUpdate(toep, "Name", atCmd.Data);
                // close client
                else if (atCmd.DataType == AtCommandDataType.ClientClose)
                    ClientClose(strTmp[0], int.Parse(strTmp[1]));
                // send msg to client
                else if (atCmd.DataType == AtCommandDataType.ClientSendMsg) {
                    // 设置发送结果
                    lock (DataUI.ClientTable) {
                        var subset = from s in DataUI.ClientTable where s.ID.Equals(atCmd.ToID) select s;
                        if (subset.Count() != 0)
                            atCmd.Result = subset.Count() != 0 ? "Success" : "Failure";
                    }

                    //// 发送数据
                    ClientSendMessage(strTmp[0], int.Parse(strTmp[1]), atCmd.Data);

                    //// 反馈发送结果
                    DataUI.RwlockModuleTable.AcquireReaderLock(-1);
                    foreach (var item in DataUI.ModuleTable) {
                        if (item.Module.ModuleID.Equals(atCmd.FromID)) {
                            try {
                                item.Module.Invoke(SMsgProc.FULL_NAME, SMsgProc.ATCMD_RESULT, new object[] { atCmd });
                            } catch (Exception) { }
                            break;
                        }
                    }
                    DataUI.RwlockModuleTable.ReleaseReaderLock();
                }
            }
        }

        // Override Session Event ==========================================================================

        protected override void sess_create(object sender, SockSess sess)
        {
            if (sess.type == SockType.accept)
                DataUI.ClientAdd(sess.lep, sess.rep);
        }

        protected override void sess_delete(object sender, SockSess sess)
        {
            if (sess.type == SockType.accept)
                DataUI.ClientDel(sess.lep, sess.rep);
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
                        item.Module.Invoke(SMsgProc.FULL_NAME, SMsgProc.HANDLE_MSG, new object[] { request.rep, content });
                    } catch (Exception) { }
                    goto _out;
                }
            }

            // 如果没有得到处理，尝试翻译模块处理
            var subset = from s in DataUI.ModuleTable where s.Module.CheckInterface(new string[] { SMsgTrans.FULL_NAME }) select s;
            if (subset.Count() == 0) goto _out;
            ModuleNode node = subset.First().Module as ModuleNode;
            try {
                content = (string)node.Invoke(SMsgTrans.FULL_NAME, SMsgTrans.TRANSLATE, new object[] { content });
                if (string.IsNullOrEmpty(content))
                    goto _out;
            } catch (Exception) {
                goto _out;
            }

            // 再次调用模块处理消息
            foreach (var item in DataUI.ModuleTable) {
                if (content.Contains(item.Module.ModuleID)) {
                    try {
                        item.Module.Invoke(SMsgProc.FULL_NAME, SMsgProc.HANDLE_MSG, new object[] { request.rep, content });
                    } catch (Exception) { }
                    goto _out;
                }
            }

        _out:
            // 解锁
            DataUI.RwlockModuleTable.ReleaseReaderLock();

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

        private void default_controller(SockRequest request, SockResponse response)
        {
            string log = DateTime.Now + " (" + request.rep.ToString() + " => " + request.lep.ToString() + ")\n";
            log += Coding.GetString(request.data) + "\n\n";

            DataUI.Logger(log);
            DataUI.PackRecved();

            cmdctl.AppendCommand(PACK_PARSE, new object[] { request, response });
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
            if (module.ModuleID.IndexOf("HT=") != -1 && !module.CheckInterface(new string[] { SMsgProc.FULL_NAME })) {
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
