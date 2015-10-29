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
            this.InitMsgHandle();
        }

        // Fields ===========================================================================

        public static readonly string BASE_DIR = System.AppDomain.CurrentDomain.BaseDirectory;
        public static readonly string CONF_NAME = "EnvConsole.xml";
        public static readonly string CONF_PATH = BASE_DIR + CONF_NAME;
        public static readonly string Module_PATH = BASE_DIR + "Modules";
        public Encoding coding;
        private ObservableCollection<ServerUnit> serverTable;
        private ObservableCollection<ClientUnit> clientTable;
        private ObservableCollection<ModuleUnit> moduleTable;
        private ReaderWriterLock rwlockModuleTable;
        private ConsoleWindow console;

        // Message Handle Thread
        private const int MAX_MSG_COUNT = 980;
        private bool isExitThread;
        private Semaphore sem;
        class MessageUnit
        {
            public IPEndPoint EP;
            public string Content;
        }
        private Queue<MessageUnit> msgQueue;

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

            isExitThread = false;
            sem = new Semaphore(0, MAX_MSG_COUNT);
            msgQueue = new Queue<MessageUnit>();
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
                        item.SockServer.ClientReadMsg += AtCmdServer_ClientReadMsg;
                        item.SockServer.ClientSendMsg += AtCmdServer_ClientSendMsg;
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
                        tcp.ClientReadMsg += WorkServer_ClientReadMsg;
                        tcp.ClientSendMsg += WorkServer_ClientSendMsg;
                        item.SockServer = tcp;
                        if (item.AutoRun == true) {
                            item.SockServer.Start(new IPEndPoint(IPAddress.Parse(item.IpAddress), item.Port));
                            item.ListenState = ServerUnit.ListenStateStarted;
                        }
                    } else if (item.Protocol == "udp") {
                        item.SockServer = new UdpServer();
                        item.SockServer.ClientReadMsg += WorkServer_ClientReadMsg;
                        item.SockServer.ClientSendMsg += WorkServer_ClientSendMsg;
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

        private void InitMsgHandle()
        {
            Thread thread = new Thread(() =>
            {
                isExitThread = false;

                while (true) {
                    if (isExitThread == true) {
                        isExitThread = false;
                        break;
                    }

                    MsgHandle();
                }
            });

            thread.IsBackground = true;
            thread.Start();
        }

        private void MsgHandle()
        {
            MessageUnit msg = null;

            sem.WaitOne();
            lock (msgQueue) {
                msg = msgQueue.Dequeue();
            }
            console.MessageRemove();

            bool IsHandled = false;
            rwlockModuleTable.AcquireReaderLock(-1);
            foreach (var item in moduleTable) {
                // 水库代码太恶心，没办法的办法
                if (item.ID != "HT=" && msg.Content.Contains(item.ID)) {
                    try {
                        item.Module.Invoke(SMsgProc.FullName, SMsgProc.HandleMsg, new object[] { msg.EP, msg.Content });
                    } catch (Exception) { }
                    IsHandled = true;
                    break;
                }
            }
            // 水库代码太恶心，没办法的办法
            if (IsHandled == false) {
                foreach (var item in moduleTable) {
                    if (item.ID == "HT=" && msg.Content.Contains(item.ID)) {
                        try {
                            item.Module.Invoke(SMsgProc.FullName, SMsgProc.HandleMsg, new object[] { msg.EP, msg.Content });
                        } catch (Exception) { }
                        break;
                    }
                }
            }
            rwlockModuleTable.ReleaseReaderLock();
        }

        public void FinalMsgHandle()
        {
            isExitThread = true;
        }

        // Events for AsyncSocketListenItem =================================================

        private void AtCmdServer_ClientReadMsg(object sender, ClientEventArgs e)
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

        private void AtCmdServer_ClientSendMsg(object sender, ClientEventArgs e)
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
                        if (item.ID.Equals(atCmd.FromID)) {
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
            lock (clientTable) {
                var subset = from s in clientTable
                             where s.RemoteEP.Equals(e.RemoteEP)
                             select s;

                if (subset.Count() == 0) return;

                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    clientTable.Remove(subset.First());
                    console.ClientDisconn();
                }));
            }
        }

        private void WorkServer_ClientReadMsg(object sender, ClientEventArgs e)
        {
            string msg = coding.GetString(e.Data);

            if (msgQueue.Count() >= MAX_MSG_COUNT)
                return;

            lock (msgQueue) {
                msgQueue.Enqueue(new MessageUnit() { EP = e.RemoteEP, Content = msg });
            }
            sem.Release();
            console.MessageAppend();

            // 打印至窗口
            string logFormat = e.RemoteEP.ToString() + " " + DateTime.Now.ToString() + "接收数据：" + msg;
            console.MessageDisplay(logFormat);
        }

        private void WorkServer_ClientSendMsg(object sender, ClientEventArgs e)
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
            ModuleItem module = new ModuleItem();

            try {
                module.Load(filePath);
            } catch (Exception ex) {
                MessageBox.Show(ex.Message, "Error");
                return;
            }

            try {
                module.Invoke(SModule.FullName, SModule.Init, null);
            } catch (Exception ex) {
                module.UnLoad();
                MessageBox.Show(ex.Message, "Error");
                return;
            }

            // 加载模块已经成功
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(filePath);
            ModuleUnit moduleUnit = new ModuleUnit();
            moduleUnit.ID = (string)module.Invoke(SModule.FullName, SModule.GetModuleID, null);
            moduleUnit.Name = fvi.ProductName;
            moduleUnit.FilePath = filePath;
            moduleUnit.FileName = module.AssemblyName;
            moduleUnit.FileComment = fvi.Comments;
            moduleUnit.Module = module;

            // 加入 table
            rwlockModuleTable.AcquireWriterLock(-1);
            moduleTable.Add(moduleUnit);
            rwlockModuleTable.ReleaseWriterLock();
        }

        public void AtModuleUnload(string fileName)
        {
            rwlockModuleTable.AcquireWriterLock(-1);

            var subset = from s in moduleTable where s.FileName.Equals(fileName) select s;
            if (subset.Count() != 0) {
                try {
                    subset.First().Module.Invoke(SModule.FullName, SModule.Final, null);
                } catch (Exception) { }
                // 卸载模块
                subset.First().Module.UnLoad();
                // 移出 table
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
