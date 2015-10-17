using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Xml.Serialization;
using System.Diagnostics;
using System.Windows;
using System.Xml;
using System.Threading;
using Mnn.MnnSock.Deprecated;
using Mnn.MnnMisc.MnnModule;
using Mnn.MnnMisc.MnnEnv;

namespace StationConsole.CtrlLayer
{
    class MessageUnit
    {
        public IPEndPoint EP;
        public byte[] Content;
    }

    public class Controler
    {
        // From config.xml
        public Encoding coding = Encoding.Default;

        // tables
        private List<ServerUnit> serverTable = new List<ServerUnit>();
        private List<ClientUnit> clientTable = new List<ClientUnit>();
        private List<ModuleUnit> moduleTable = new List<ModuleUnit>();
        // rwlock for moduleTable
        private ReaderWriterLock rwlock = new ReaderWriterLock();

        // Message Handle Thread
        private const int max_msg_count = 980;
        private bool isExitThread = false;
        private Semaphore sem = new Semaphore(0, max_msg_count);
        private Queue<MessageUnit> msgQueue = new Queue<MessageUnit>();

        public void InitConfig()
        {
            if (File.Exists(System.AppDomain.CurrentDomain.BaseDirectory + @"\stationconsole.xml") == false) {
                System.Windows.MessageBox.Show("未找到配置文件： stationconsole.xml");
                Thread.CurrentThread.Abort();
            }

            /// ** Initialize Start ====================================================
            try {
                XmlDocument xdoc = new XmlDocument();
                xdoc.Load(System.AppDomain.CurrentDomain.BaseDirectory + @"\stationconsole.xml");

                // coding Config
                XmlNode node = xdoc.SelectSingleNode("/configuration/encoding");
                coding = Encoding.GetEncoding(node.InnerText);

                // Server Config
                foreach (XmlNode item in xdoc.SelectNodes("/configuration/serverconfig/server")) {
                    ServerUnit server = new ServerUnit();
                    server.ID = item.Attributes["id"].Value;
                    server.Name = item.Attributes["name"].Value;
                    server.ServerType = item.Attributes["type"].Value;
                    server.Protocol = item.Attributes["protocol"].Value;

                    if (server.Protocol == "pipe") {
                        server.PipeName = item.Attributes["pipename"].Value;
                    }
                    else {
                        server.IpAddress = item.Attributes["ipaddress"].Value;
                        server.Port = int.Parse(item.Attributes["port"].Value);
                    }

                    server.AutoRun = bool.Parse(item.Attributes["autorun"].Value);
                    server.CanStop = bool.Parse(item.Attributes["canstop"].Value);

                    serverTable.Add(server);
                }
            }
            catch (Exception ex) {
                Mnn.MnnUtil.Logger.WriteException(ex);
                System.Windows.MessageBox.Show("配置文件读取错误： stationconsole.xml");
            }

            foreach (var item in serverTable) {
                App.Mindow.AddServer(item);
            }
            /// ** Initialize End ====================================================
        }

        public void InitServer()
        {
            // 启动监听
            foreach (var item in serverTable) {
                // AtCmd Server
                if (item.ServerType == "atcmd") {
                    if (item.Protocol == "udp") {
                        item.Server = new UdpServer();
                        item.Server.ClientReadMsg += AtCmdServer_ClientReadMsg;
                        item.Server.ClientSendMsg += AtCmdServer_ClientSendMsg;
                        if (item.AutoRun == true)
                            item.Server.Start(new IPEndPoint(IPAddress.Parse(item.IpAddress), item.Port));
                    }
                    //else if (item.Protocol == "pipe") {
                    //    PipeServer pipeServer = new PipeServer();
                    //    pipeServer.ClientReadMsg += AtCmdServer_ClientReadMsg;
                    //    //pipeServer.Start(item.PipeName);
                    //}
                }

                // Work Server
                else if (item.ServerType == "work") {
                    if (item.Protocol == "tcp") {
                        TcpServer tcp = new TcpServer();
                        tcp.ClientConnect += WorkServer_ClientConnect;
                        tcp.ClientDisconn += WorkServer_ClientDisconn;
                        tcp.ClientReadMsg += WorkServer_ClientReadMsg;
                        tcp.ClientSendMsg += WorkServer_ClientSendMsg;
                        item.Server = tcp;
                        if (item.AutoRun == true)
                            item.Server.Start(new IPEndPoint(IPAddress.Parse(item.IpAddress), item.Port));
                    }
                    else if (item.Protocol == "udp") {
                        item.Server = new UdpServer();
                        item.Server.ClientReadMsg += WorkServer_ClientReadMsg;
                        item.Server.ClientSendMsg += WorkServer_ClientSendMsg;
                        if (item.AutoRun == true)
                            item.Server.Start(new IPEndPoint(IPAddress.Parse(item.IpAddress), item.Port));
                    }
                }
            }

        }

        public void InitDefaultModule()
        {
            // 加载 DataHandles 文件夹下的所有模块
            string modulePath = System.AppDomain.CurrentDomain.BaseDirectory + @"\Modules";

            if (Directory.Exists(modulePath)) {
                string[] files = Directory.GetFiles(modulePath);

                // Load dll files one by one
                foreach (var item in files) {
                    if ((item.EndsWith(".dll") || item.EndsWith(".dll")) && item.Contains("Module_")) {
                        AtModuleLoad(item);
                    }
                }
            }
        }

        public void InitMsgHandle()
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
            App.Mindow.RemoveMessage();

            bool IsHandled = false;
            string msgstr = coding.GetString(msg.Content);
            rwlock.AcquireReaderLock(-1);
            foreach (var item in moduleTable) {
                // 水库代码太恶心，没办法的办法
                if (item.ID != "HT=" && msgstr.Contains(item.ID)) {
                    try {
                        item.Module.Invoke("Mnn.MnnMisc.MnnDataHandle.IDataHandle", "HandleMsg", new object[] { msg.EP, msg.Content });
                    }
                    catch (Exception) { }
                    IsHandled = true;
                    break;
                }
            }
            // 水库代码太恶心，没办法的办法
            if (IsHandled == false) {
                foreach (var item in moduleTable) {
                    if (item.ID == "HT=" && msgstr.Contains(item.ID)) {
                        try {
                            item.Module.Invoke("Mnn.MnnMisc.MnnDataHandle.IDataHandle", "HandleMsg", new object[] { msg.EP, msg.Content });
                        }
                        catch (Exception) { }
                        break;
                    }
                }
            }
            rwlock.ReleaseReaderLock();
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
            }
            catch (Exception ex) {
                Mnn.MnnUtil.Logger.WriteException(ex);
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
            App.Mindow.DisplayMessage(logFormat);
            Mnn.MnnUtil.Logger.Write(logFormat);
        }

        private void AtCmdServer_ClientSendMsg(object sender, ClientEventArgs e)
        {
            AtCommand atCmd = null;

            try {
                using (MemoryStream memory = new MemoryStream(e.Data)) {
                    XmlSerializer xmlFormat = new XmlSerializer(typeof(AtCommand));
                    atCmd = xmlFormat.Deserialize(memory) as AtCommand;
                }
            }
            catch (Exception ex) {
                Mnn.MnnUtil.Logger.WriteException(ex);
                return;
            }

            // 打印至窗口，写命令日志
            string logFormat = e.RemoteEP.ToString() + " " + DateTime.Now.ToString() + "发送命令："
                + "|FromID=" + atCmd.FromID.ToString()
                + "|ToID=" + atCmd.ToID
                + "|ToEP=" + atCmd.ToEP
                + "|DataType=" + atCmd.DataType.ToString()
                + "|Data=" + atCmd.Data;
            App.Mindow.DisplayMessage(logFormat);
            Mnn.MnnUtil.Logger.Write(logFormat);
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
                    App.Mindow.UpdateClient(
                        new IPEndPoint(IPAddress.Parse(strTmp[0]), Convert.ToInt32(strTmp[1])),
                        "ID", atCmd.Data);
                }
                else if (atCmd.DataType == AtCommandDataType.ClientUpdateName) {
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
                    string[] strTmp = atCmd.ToEP.Split(":".ToArray());
                    App.Mindow.UpdateClient(
                        new IPEndPoint(IPAddress.Parse(strTmp[0]), Convert.ToInt32(strTmp[1])),
                        "Name", atCmd.Data);
                }
                else if (atCmd.DataType == AtCommandDataType.ClientClose) {
                    AtClientClose(atCmd.ToID);
                }
                else if (atCmd.DataType == AtCommandDataType.ClientSendMsg) {
                    // 设置发送结果
                    lock (clientTable) {
                        var subset = from s in clientTable where s.ID.Equals(atCmd.ToID) select s;
                        if (subset.Count() != 0)
                            atCmd.Result = subset.Count() != 0 ? "Success" : "Failure";
                    }

                    //// 发送数据
                    AtClientSendMessage(atCmd.ToID, atCmd.Data);

                    //// 反馈发送结果
                    rwlock.AcquireReaderLock(-1);
                    foreach (var item in moduleTable) {
                        if (item.ID.Equals(atCmd.FromID)) {
                            try {
                                item.Module.Invoke("Mnn.MnnMisc.MnnDataHandle.IDataHandle", "AtCmdResult", new object[] { atCmd });
                            }
                            catch (Exception) { }
                            break;
                        }
                    }
                    rwlock.ReleaseReaderLock();
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

            lock (clientTable) {
                clientTable.Add(client);
            }

            App.Mindow.AddClient(client);
        }

        private void WorkServer_ClientDisconn(object sender, ClientEventArgs e)
        {
            lock (clientTable) {
                var subset = from s in clientTable
                             where s.RemoteEP.Equals(e.RemoteEP)
                             select s;

                if (subset.Count() != 0) {
                    // 通知 主窗体
                    //if (string.IsNullOrEmpty(subset.First().ID) == false)
                        App.Mindow.RemoveClient(subset.First());
                    // 移出 table
                    clientTable.Remove(subset.First());
                }
            }
        }

        private void WorkServer_ClientReadMsg(object sender, ClientEventArgs e)
        {
            if (msgQueue.Count() >= max_msg_count)
                return;

            lock (msgQueue) {
                msgQueue.Enqueue(new MessageUnit() { EP = e.RemoteEP, Content = e.Data });
            }
            sem.Release();
            App.Mindow.AddMessage();
        }

        private void WorkServer_ClientSendMsg(object sender, ClientEventArgs e)
        {
            // 打印至窗口
            string logFormat = e.RemoteEP.ToString() + " " + DateTime.Now.ToString() + "发送数据：" + coding.GetString(e.Data);
            App.Mindow.DisplayMessage(logFormat);
            // 发送数据要写日志
            Mnn.MnnUtil.Logger.Write(logFormat);
        }

        // At Command ========================================================================

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
                    subset.First().Server.Start(
                        new IPEndPoint(IPAddress.Parse(subset.First().IpAddress), subset.First().Port));
                }
                catch (Exception ex) {
                    MessageBox.Show(ex.Message, "Error");
                }
            //}
        }

        public void AtServerStart(string serverID, IPEndPoint ep)
        {
            //lock (serverTable) {
                var subset = from s in serverTable
                             where s.ID.Equals(serverID)
                             select s;

                if (subset.Count() == 0)
                    return;

                // 端口可能已经被其他程序监听
                try {
                    subset.First().Server.Start(ep);
                }
                catch (Exception ex) {
                    MessageBox.Show(ex.Message, "Error");
                }
            //}
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
                subset.First().Server.Stop();
            //}
        }

        public void AtServerTimerStart(string serverID, double interval, string timerCommand)
        {
            //lock (serverTable) {
                var subset = from s in serverTable
                             where s.ID.Equals(serverID) && s.Server is TcpServer
                             select s;

                if (subset.Count() == 0)
                    return;

                subset.First().Timer = new System.Timers.Timer(interval);
                // limbda 不会锁住serverTable
                subset.First().Timer.Elapsed += new System.Timers.ElapsedEventHandler((s, ea) =>
                {
                    try {
                        (subset.First().Server as TcpServer).Send(coding.GetBytes(timerCommand));
                    }
                    catch (Exception) { }
                });
                subset.First().Timer.Start();
            //}
        }

        public void AtServerTimerStop(string serverID)
        {
            //lock (serverTable) {
                foreach (var item in serverTable) {
                    if (item.ID.Equals(serverID) && item.Server is TcpServer) {
                        item.Timer.Stop();
                        item.Timer.Close();
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
            }
            catch (Exception ex) {
                MessageBox.Show(ex.Message, "Error");
                return;
            }

            try {
                module.Invoke("Mnn.MnnMisc.MnnModule.IModule", "Init", null);
            }
            catch (Exception ex) {
                module.UnLoad();
                MessageBox.Show(ex.Message, "Error");
                return;
            }

            // 加载模块已经成功
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(filePath);
            ModuleUnit moduleUnit = new ModuleUnit();
            moduleUnit.ID = (string)module.Invoke("Mnn.MnnMisc.MnnModule.IModule", "GetModuleID", null);
            moduleUnit.Name = fvi.ProductName;
            moduleUnit.Type = (UInt16)module.Invoke("Mnn.MnnMisc.MnnModule.IModule", "GetModuleType", null);
            moduleUnit.FilePath = filePath;
            moduleUnit.FileName = module.AssemblyName;
            moduleUnit.FileComment = fvi.Comments;
            moduleUnit.Module = module;

            // 加入 table
            rwlock.AcquireWriterLock(-1);
            moduleTable.Add(moduleUnit);
            rwlock.ReleaseWriterLock();

            App.Mindow.AddModule(moduleUnit);
        }

        public void AtModuleUnload(string fileName)
        {
            rwlock.AcquireWriterLock(-1);

            var subset = from s in moduleTable where s.FileName.Equals(fileName) select s;
            if (subset.Count() != 0) {
                try {
                    subset.First().Module.Invoke("Mnn.MnnMisc.MnnModule.IModule", "Final", null);
                }
                catch (Exception) { }
                // 卸载模块
                subset.First().Module.UnLoad();
                // 移出 table
                App.Mindow.RemoveModule(subset.First());
                moduleTable.Remove(subset.First());
            }

            rwlock.ReleaseWriterLock();
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
                        subset.First().Server.Send(ep, coding.GetBytes(msg));
                }
                catch (Exception) { }
            //}
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
                if (subserver.Count() != 0 && subserver.First().Server is TcpServer) {
                    TcpServer tcp = subserver.First().Server as TcpServer;
                    tcp.CloseClient(ep);
                }
            //}
        }

    }
}
