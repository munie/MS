using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Threading;
using System.Collections.ObjectModel;
using System.IO;
using System.Diagnostics;
using System.Xml;
using System.Xml.Serialization;
using System.Net;
using mnn.net.deprecated;
using mnn.misc.module;
using mnn.misc.env;
using mnn.util;
using EnvConsole.Unit;

namespace EnvConsole.Windows
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Hide();
            this.Init();
            this.InitConfig();
            this.InitServer();
            this.InitDefaultModule();

            Thread thread = new Thread(() =>
            {
                cmdcer.Perform();
                modcer.Perform();
            });
            thread.IsBackground = true;
            thread.Start();
        }

        // Fields ===========================================================================

        public static readonly string BASE_DIR = System.AppDomain.CurrentDomain.BaseDirectory;
        public static readonly string CONF_NAME = "EnvConsole.xml";
        public static readonly string CONF_PATH = BASE_DIR + CONF_NAME;
        public static readonly string Module_PATH = BASE_DIR + "Modules";
        public static readonly string PACK_PARSE = "PackParse";
        public Encoding coding;
        private ObservableCollection<ServerUnit> serverTable;
        private ObservableCollection<ClientUnit> clientTable;
        private ObservableCollection<ModuleUnit> moduleTable;
        private ReaderWriterLock rwlockModuleTable;
        private ConsoleWindow console;
        private ModuleCenter modcer;
        private AtCmdCenter cmdcer;
        class PackUnit
        {
            public IPEndPoint EP;
            public string Content;
        }

        private void Init()
        {
            coding = Encoding.Default;
            serverTable = new ObservableCollection<ServerUnit>();
            clientTable = new ObservableCollection<ClientUnit>();
            moduleTable = new ObservableCollection<ModuleUnit>();
            rwlockModuleTable = new ReaderWriterLock();
            console = new ConsoleWindow();
            console.Owner = this;
            console.DataContext = new { ServerTable = serverTable, ClientTable = clientTable, ModuleTable = moduleTable };
            console.Show();
            modcer = new ModuleCenter();
            cmdcer = new AtCmdCenter();
            cmdcer.Add(PACK_PARSE, PackParse);
        }

        private void InitConfig()
        {
            if (File.Exists(CONF_PATH) == false) {
                System.Windows.MessageBox.Show(CONF_NAME + ": can't find it.");
                Thread.CurrentThread.Abort();
            }

            /// ** Initialize Start ====================================================
            try {
                XmlDocument xdoc = new XmlDocument();
                xdoc.Load(CONF_PATH);

                // coding Config
                XmlNode node = xdoc.SelectSingleNode("/configuration/encoding");
                coding = Encoding.GetEncoding(node.InnerText);

                // Server Config
                foreach (XmlNode item in xdoc.SelectNodes("/configuration/servers/server")) {
                    ServerUnit server = new ServerUnit();
                    server.ID = item.Attributes["id"].Value;
                    server.Name = item.Attributes["name"].Value;
                    server.ServerType = item.Attributes["type"].Value;
                    server.Protocol = item.Attributes["protocol"].Value;
                    server.IpAddress = item.Attributes["ipaddress"].Value;
                    server.Port = int.Parse(item.Attributes["port"].Value);
                    server.AutoRun = bool.Parse(item.Attributes["autorun"].Value);
                    server.CanStop = bool.Parse(item.Attributes["canstop"].Value);

                    server.ListenState = ServerUnit.ListenStateStoped;
                    if (server.Protocol == "tcp") {
                        server.TimerState = ServerUnit.TimerStateStoped;
                    } else {
                        server.TimerState = ServerUnit.TimerStateDisable;
                    }
                    server.TimerInterval = 0;
                    server.TimerCommand = "";

                    serverTable.Add(server);
                }
            } catch (Exception ex) {
                mnn.util.Logger.WriteException(ex);
                System.Windows.MessageBox.Show(CONF_NAME + ": syntax error.");
            }
            /// ** Initialize End ====================================================
        }

        private void InitServer()
        {
            // 启动监听
            foreach (var item in serverTable) {
                if (item.ServerType == "atcmd") {
                    if (item.Protocol == "udp") {
                        item.SockServer = new UdpServer();
                        item.SockServer.ClientRecvPack += AtCmdServer_ClientRecvPack;
                        item.SockServer.ClientSendPack += AtCmdServer_ClientSendPack;
                        if (item.AutoRun == true) {
                            item.SockServer.Start(new IPEndPoint(IPAddress.Parse(item.IpAddress), item.Port));
                            item.ListenState = ServerUnit.ListenStateStarted;
                        }
                    }
                } else if (item.ServerType == "work") {
                    if (item.Protocol == "tcp") {
                        TcpServer tcp = new TcpServer();
                        tcp.ClientConnect += WorkServer_ClientConnect;
                        tcp.ClientDisconn += WorkServer_ClientDisconn;
                        tcp.ClientRecvPack += WorkServer_ClientRecvPack;
                        tcp.ClientSendPack += WorkServer_ClientSendPack;
                        item.SockServer = tcp;
                        if (item.AutoRun == true) {
                            item.SockServer.Start(new IPEndPoint(IPAddress.Parse(item.IpAddress), item.Port));
                            item.ListenState = ServerUnit.ListenStateStarted;
                        }
                    } else if (item.Protocol == "udp") {
                        item.SockServer = new UdpServer();
                        item.SockServer.ClientRecvPack += WorkServer_ClientRecvPack;
                        item.SockServer.ClientSendPack += WorkServer_ClientSendPack;
                        if (item.AutoRun == true) {
                            item.SockServer.Start(new IPEndPoint(IPAddress.Parse(item.IpAddress), item.Port));
                            item.ListenState = ServerUnit.ListenStateStarted;
                        }
                    }
                }
            }
        }

        private void InitDefaultModule()
        {
            // 加载 DataHandles 文件夹下的所有模块
            if (Directory.Exists(Module_PATH)) {
                string[] files = Directory.GetFiles(Module_PATH);

                // Load dll files one by one
                foreach (var item in files) {
                    string str = item.Substring(item.LastIndexOf("\\") + 1);
                    if (str.Contains("Module") && str.ToLower().EndsWith(".dll"))
                        AtModuleLoad(item);
                }
            }
        }

        private void PackParse(object arg)
        {
            PackUnit pack = arg as PackUnit;
            console.PackParsed();

            bool IsHandled = false;
            rwlockModuleTable.AcquireReaderLock(-1);
            foreach (var item in moduleTable) {
                // 水库代码太恶心，没办法的办法
                if (item.Module.ModuleID != "HT=" && pack.Content.Contains(item.Module.ModuleID)) {
                    try {
                        item.Module.Invoke(SMsgProc.FullName, SMsgProc.HandleMsg, new object[] { pack.EP, pack.Content });
                    } catch (Exception) { }
                    IsHandled = true;
                    break;
                }
            }
            // 水库代码太恶心，没办法的办法
            if (IsHandled == false) {
                foreach (var item in moduleTable) {
                    if (item.Module.ModuleID == "HT=" && pack.Content.Contains(item.Module.ModuleID)) {
                        try {
                            item.Module.Invoke(SMsgProc.FullName, SMsgProc.HandleMsg, new object[] { pack.EP, pack.Content });
                        } catch (Exception) { }
                        break;
                    }
                }
            }
            rwlockModuleTable.ReleaseReaderLock();
        }

        // Events for AsyncSocketListenItem =================================================

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
            console.MessageDisplay(logFormat);
            mnn.util.Logger.Write(logFormat);
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
            console.MessageDisplay(logFormat);
            mnn.util.Logger.Write(logFormat);
        }

        private void AtCmdServer_ExecCommand(AtCommand atCmd)
        {
            if (atCmd.ToSchema == UnitSchema.Client && atCmd.Direct == AtCommandDirect.Request) {
                if (atCmd.DataType == AtCommandDataType.ClientUpdateID) {
                    // 更新逻辑层 client
                    lock (clientTable) {
                        foreach (var item in clientTable) {
                            if (item.RemoteEP.ToString().Equals(atCmd.ToEP)) {
                                item.ID = atCmd.Data;
                                break;
                            }
                        }
                    }
                    // 更新界面 client
                    string[] strTmp = atCmd.ToEP.Split(":".ToArray());
                    console.ClientUpdate(
                        new IPEndPoint(IPAddress.Parse(strTmp[0]), Convert.ToInt32(strTmp[1])),
                        "ID", atCmd.Data);
                } else if (atCmd.DataType == AtCommandDataType.ClientUpdateName) {
                    // 更新逻辑层 client
                    lock (clientTable) {
                        foreach (var item in clientTable) {
                            if (item.RemoteEP.ToString().Equals(atCmd.ToEP)) {
                                item.Name = atCmd.Data;
                                break;
                            }
                        }
                    }
                    // 更新界面 client
                    //string[] strTmp = atCmd.ToEP.Split(":".ToArray());
                    //CsWin.ClientUpdate(
                    //    new IPEndPoint(IPAddress.Parse(strTmp[0]), Convert.ToInt32(strTmp[1])),
                    //    "Name", atCmd.Data);
                } else if (atCmd.DataType == AtCommandDataType.ClientClose) {
                    AtClientClose(atCmd.ToID);
                } else if (atCmd.DataType == AtCommandDataType.ClientSendMsg) {
                    // 设置发送结果
                    lock (clientTable) {
                        var subset = from s in clientTable where s.ID.Equals(atCmd.ToID) select s;
                        if (subset.Count() != 0)
                            atCmd.Result = subset.Count() != 0 ? "Success" : "Failure";
                    }

                    //// 发送数据
                    AtClientSendMessage(atCmd.ToID, atCmd.Data);

                    //// 反馈发送结果
                    rwlockModuleTable.AcquireReaderLock(-1);
                    foreach (var item in moduleTable) {
                        if (item.Module.ModuleID.Equals(atCmd.FromID)) {
                            try {
                                item.Module.Invoke(SMsgProc.FullName, SMsgProc.AtCmdResult, new object[] { atCmd });
                            } catch (Exception) { }
                            break;
                        }
                    }
                    rwlockModuleTable.ReleaseReaderLock();
                }
            }

        }

        private void WorkServer_ClientConnect(object sender, ClientEventArgs e)
        {
            ClientUnit client = new ClientUnit();

            client.ID = "";
            client.Name = "";
            client.RemoteEP = e.RemoteEP;
            //lock (serverTable) {
            foreach (var item in serverTable) {
                if (item.Port.Equals(e.LocalEP.Port)) {
                    client.ServerID = item.ID;
                    client.ServerName = item.Name;
                    break;
                }
            }
            //}
            client.ConnectTime = DateTime.Now;

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                lock (clientTable) {
                    clientTable.Add(client);
                }
                console.ClienConnect();
            }));
        }

        private void WorkServer_ClientDisconn(object sender, ClientEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                lock (clientTable) {
                    var subset = from s in clientTable
                                 where s.RemoteEP.Equals(e.RemoteEP)
                                 select s;

                    if (subset.Count() == 0) return;


                    clientTable.Remove(subset.First());
                    console.ClientDisconn();

                }
            }));
        }

        private void WorkServer_ClientRecvPack(object sender, ClientEventArgs e)
        {
            string msg = coding.GetString(e.Data);

            cmdcer.AppendCommand(PACK_PARSE, new PackUnit() { EP = e.RemoteEP, Content = msg });
            console.PackRecved();

            // 打印至窗口
            string logFormat = e.RemoteEP.ToString() + " " + DateTime.Now.ToString() + "接收数据：" + msg;
            console.MessageDisplay(logFormat);
        }

        private void WorkServer_ClientSendPack(object sender, ClientEventArgs e)
        {
            // 打印至窗口
            string logFormat = e.RemoteEP.ToString() + " " + DateTime.Now.ToString() + "发送数据：" + coding.GetString(e.Data);
            console.MessageDisplay(logFormat);
            // 发送数据要写日志
            mnn.util.Logger.Write(logFormat);
        }

        // AtCmd ============================================================================

        public void AtServerStart(ServerUnit server)
        {
            // 端口可能已经被其他程序监听
            try {
                server.SockServer.Start(new IPEndPoint(IPAddress.Parse(server.IpAddress), server.Port));
                server.ListenState = ServerUnit.ListenStateStarted;
            } catch (Exception ex) {
                MessageBox.Show(ex.Message, "Error");
                server.ListenState = ServerUnit.ListenStateStoped;
            }
        }

        public void AtServerStart(string serverID)
        {
            //lock (serverTable) {
            var subset = from s in serverTable
                         where s.ID.Equals(serverID)
                         select s;

            if (subset.Count() == 0)
                return;

            // 端口可能已经被其他程序监听
            try {
                subset.First().SockServer.Start(
                    new IPEndPoint(IPAddress.Parse(subset.First().IpAddress), subset.First().Port));
                subset.First().ListenState = ServerUnit.ListenStateStarted;
            } catch (Exception ex) {
                MessageBox.Show(ex.Message, "Error");
                subset.First().ListenState = ServerUnit.ListenStateStoped;
            }
            //}
        }

        public void AtServerStop(ServerUnit server)
        {
            // 逻辑上讲，不会出现异常
            server.SockServer.Stop();
            server.ListenState = ServerUnit.ListenStateStoped;
        }

        public void AtServerTimerStart(ServerUnit server)
        {
            server.Timer = new System.Timers.Timer(server.TimerInterval * 1000);
            // limbda 不会锁住serverTable
            server.Timer.Elapsed += new System.Timers.ElapsedEventHandler((s, ea) =>
            {
                try {
                    (server.SockServer as TcpServer).Send(coding.GetBytes(server.TimerCommand));
                } catch (Exception) { }
            });
            server.Timer.Start();
            server.TimerState = ServerUnit.TimerStateStarted;
        }

        public void AtServerTimerStop(ServerUnit server)
        {
            server.Timer.Stop();
            server.Timer.Close();
            server.TimerState = ServerUnit.TimerStateStoped;
        }

        public void AtServerStop(string serverID)
        {
            //lock (serverTable) {
            var subset = from s in serverTable
                         where s.ID.Equals(serverID)
                         select s;

            if (subset.Count() == 0)
                return;

            // 逻辑上讲，不会出现异常
            subset.First().SockServer.Stop();
            subset.First().ListenState = ServerUnit.ListenStateStoped;
            //}
        }

        public void AtServerTimerStart(string serverID, double interval, string timerCommand)
        {
            //lock (serverTable) {
            var subset = from s in serverTable
                         where s.ID.Equals(serverID) && s.SockServer is TcpServer
                         select s;

            if (subset.Count() == 0)
                return;

            subset.First().Timer = new System.Timers.Timer(interval);
            // limbda 不会锁住serverTable
            subset.First().Timer.Elapsed += new System.Timers.ElapsedEventHandler((s, ea) =>
            {
                try {
                    (subset.First().SockServer as TcpServer).Send(coding.GetBytes(timerCommand));
                } catch (Exception) { }
            });
            subset.First().Timer.Start();
            subset.First().TimerState = ServerUnit.TimerStateStarted;
            //}
        }

        public void AtServerTimerStop(string serverID)
        {
            //lock (serverTable) {
            foreach (var item in serverTable) {
                if (item.ID.Equals(serverID) && item.SockServer is TcpServer) {
                    item.Timer.Stop();
                    item.Timer.Close();
                    item.TimerState = ServerUnit.TimerStateStoped;
                    break;
                }
            }
            //}
        }

        public void AtModuleLoad(string filePath)
        {
            ModuleNode module = modcer.AddModule(filePath);
            if (module == null) {
                MessageBox.Show(filePath + ": load failed.");
                return;
            }
            // 如果是消息处理模块，必须实现消息处理接口，否则加载失败
            if (module.ModuleID.IndexOf("HT=") != -1 && !module.CheckInterface(new string[] { SMsgProc.FullName })) {
                modcer.DelModule(module);
                MessageBox.Show(filePath + ": load failed.");
                return;
            }

            // 加载模块已经成功
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(filePath);
            ModuleUnit unit = new ModuleUnit();
            unit.FileName = module.AssemblyName;
            unit.FileComment = fvi.Comments;
            unit.Module = module;

            // 加入 table
            rwlockModuleTable.AcquireWriterLock(-1);
            moduleTable.Add(unit);
            rwlockModuleTable.ReleaseWriterLock();
        }

        public void AtModuleUnload(string fileName)
        {
            rwlockModuleTable.AcquireWriterLock(-1);

            var subset = from s in moduleTable where s.FileName.Equals(fileName) select s;
            if (subset.Count() != 0) {
                // 移出 table
                modcer.DelModule(subset.First().Module);
                moduleTable.Remove(subset.First());
            }

            rwlockModuleTable.ReleaseWriterLock();
        }

        public void AtClientSendMessage(ClientUnit client, string msg)
        {
            try {
                var subset = from s in serverTable where s.ID.Equals(client.ServerID) select s;
                if (subset.Count() != 0)
                    subset.First().SockServer.Send(client.RemoteEP, coding.GetBytes(msg));
            } catch (Exception) { }
        }

        public void AtClientSendMessage(string clientID, string msg)
        {
            // Find IPEndPoint of Client
            string serverID = null;
            IPEndPoint ep = null;
            lock (clientTable) {
                var subset = from s in clientTable where s.ID.Equals(clientID) select s;
                if (subset.Count() != 0) {
                    serverID = subset.First().ServerID;
                    ep = subset.First().RemoteEP;
                }
            }
            if (string.IsNullOrEmpty(serverID) || ep == null)
                return;

            //lock (serverTable) { //会死锁
            try {
                var subset = from s in serverTable where s.ID.Equals(serverID) select s;
                if (subset.Count() != 0)
                    subset.First().SockServer.Send(ep, coding.GetBytes(msg));
            } catch (Exception) { }
            //}
        }

        public void AtClientClose(ClientUnit client)
        {
            var subserver = from s in serverTable where s.ID.Equals(client.ServerID) select s;

            if (subserver.Count() != 0 && subserver.First().SockServer is TcpServer) {
                TcpServer tcp = subserver.First().SockServer as TcpServer;
                tcp.CloseClient(client.RemoteEP);
            }
        }

        public void AtClientClose(string clientID)
        {
            // Find IPEndPoint of Client
            string serverID = null;
            IPEndPoint ep = null;
            lock (clientTable) {
                var subset = from s in clientTable where s.ID.Equals(clientID) select s;
                if (subset.Count() != 0) {
                    serverID = subset.First().ServerID;
                    ep = subset.First().RemoteEP;
                }
            }
            if (string.IsNullOrEmpty(serverID) || ep == null)
                return;

            // Close Client
            //lock (serverTable) {
            var subserver = from s in serverTable where s.ID.Equals(serverID) select s;
            if (subserver.Count() != 0 && subserver.First().SockServer is TcpServer) {
                TcpServer tcp = subserver.First().SockServer as TcpServer;
                tcp.CloseClient(ep);
            }
            //}
        }
    }
}
