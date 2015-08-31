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
using Mnn.MnnSocket;
using Mnn.MnnPlugin;

namespace StationConsole.ControlLayer
{
    public class Controler
    {
        // From config.xml
        public Encoding coding = Encoding.Default;

        private List<ServerUnit> serverTable = new List<ServerUnit>();
        private List<ClientUnit> clientTable = new List<ClientUnit>();
        private List<PluginUnit> pluginTable = new List<PluginUnit>();

        private ReaderWriterLock rwlock = new ReaderWriterLock();

        public void InitailizeConfig()
        {
            if (File.Exists(System.AppDomain.CurrentDomain.BaseDirectory + @"\config.xml") == false) {
                System.Windows.MessageBox.Show("未找到配置文件： config.xml");
                Thread.CurrentThread.Abort();
            }

            /// ** Initialize Start ====================================================
            try {
                XmlDocument xdoc = new XmlDocument();
                xdoc.Load(System.AppDomain.CurrentDomain.BaseDirectory + @"\config.xml");

                // coding Config
                XmlNode node = xdoc.SelectSingleNode("/configuration/encoding");
                coding = Encoding.GetEncoding(node.InnerText);

                // Server Config
                foreach (XmlNode item in xdoc.SelectNodes("/configuration/serverconfig/server")) {
                    ServerUnit server = new ServerUnit();
                    server.ID = item.Attributes["id"].Value;
                    server.Name = item.Attributes["name"].Value;
                    server.Schema = MnnUnitSchema.Server;
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
                System.Windows.MessageBox.Show("配置文件读取错误： config.xml");
            }

            foreach (var item in serverTable) {
                App.Mindow.AddServer(item);
            }
            /// ** Initialize End ====================================================
        }

        public void InitailizeServer()
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

        public void InitailizeDefaultPlugin()
        {
            // 加载 DataHandles 文件夹下的所有模块
            string pluginPath = System.AppDomain.CurrentDomain.BaseDirectory + @"\DataHandles";

            if (Directory.Exists(pluginPath)) {
                string[] files = Directory.GetFiles(pluginPath);

                // Load dll files one by one
                foreach (var item in files)
                    AtPluginLoad(item);
            }
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
            }

            if (AtCmdServer_ExecCommand(atCmd) == false && atCmd.FromSchema == MnnUnitSchema.Plugin) {
                lock (pluginTable) {
                    foreach (var item in pluginTable) {
                        if (item.ID.Equals(atCmd.FromID)) {
                            try {
                                item.Plugin.Invoke("IDataHandle", "AtCmdSendFailure", new object[] { atCmd });
                            }
                            catch (Exception) { }
                        }
                    }
                }
            }

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

        private bool AtCmdServer_ExecCommand(AtCommand atCmd)
        {
            if (atCmd.ToSchema == MnnUnitSchema.Client && atCmd.Direct == AtCommandDirect.Request) {
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
                    string ServerID = null;
                    lock (clientTable) {
                        foreach (var item in clientTable) {
                            if (item.ID.Equals(atCmd.ToID)) {
                                ServerID = item.ServerID;
                                break;
                            }
                        }
                    }
                    if (ServerID != null)
                        AtClientClose(ServerID, atCmd.ToID);
                }
                else if (atCmd.DataType == AtCommandDataType.ClientSendMsg) {
                    string ServerID = null;
                    lock (clientTable) {
                        foreach (var item in clientTable) {
                            if (item.ID.Equals(atCmd.ToID)) {
                                ServerID = item.ServerID;
                                break;
                            }
                        }
                    }
                    if (ServerID != null)
                        AtClientSendMessage(ServerID, atCmd.ToID, atCmd.Data);
                    else
                        return false;
                }
            }

            return true;
        }

        private void WorkServer_ClientConnect(object sender, ClientEventArgs e)
        {
            ClientUnit client = new ClientUnit();

            client.ID = "";
            client.Name = "";
            client.Schema = MnnUnitSchema.Client;
            client.RemoteEP = e.RemoteEP;
            lock (serverTable) {
                foreach (var item in serverTable) {
                    if (item.Port.Equals(e.LocalEP.Port)) {
                        client.ServerID = item.ID;
                        client.ServerName = item.Name;
                        break;
                    }
                }
            }
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
            string msg = coding.GetString(e.Data);

            bool IsHandled = false;
            rwlock.AcquireReaderLock(100);
            foreach (var item in pluginTable) {
                // 水库代码太恶心，没办法的办法
                if (item.ID != "HT=" && msg.Contains(item.ID)) {
                    try {
                        item.Plugin.Invoke("IDataHandle", "AppendMsg", new object[] { e.RemoteEP, msg });
                    }
                    catch (Exception) { }
                    IsHandled = true;
                    break;
                }
            }
            // 水库代码太恶心，没办法的办法
            if (IsHandled == false) {
                foreach (var item in pluginTable) {
                    if (item.ID == "HT=" && msg.Contains(item.ID)) {
                        try {
                            item.Plugin.Invoke("IDataHandle", "AppendMsg", new object[] { e.RemoteEP, msg });
                        }
                        catch (Exception) { }
                        break;
                    }
                }
            }
            rwlock.ReleaseReaderLock();

            // 打印至窗口
            string logFormat = e.RemoteEP.ToString() + " " + DateTime.Now.ToString() + "接收数据：" + msg;
            App.Mindow.DisplayMessage(logFormat);
        }

        private void WorkServer_ClientSendMsg(object sender, ClientEventArgs e)
        {
            // 打印至窗口
            string logFormat = e.RemoteEP.ToString() + " " + DateTime.Now.ToString() + "发送数据：" + coding.GetString(e.Data);
            App.Mindow.DisplayMessage(logFormat);
            // 发送数据要写日志
            Mnn.MnnUtil.Logger.Write(logFormat);
        }

        // At Commands ========================================================================

        public void AtServerStart(string serverID)
        {
            lock (serverTable) {
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
            }
        }

        public void AtServerStart(string serverID, IPEndPoint ep)
        {
            lock (serverTable) {
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
            }
        }

        public void AtServerStop(string serverID)
        {
            lock (serverTable) {
                var subset = from s in serverTable
                             where s.ID.Equals(serverID)
                             select s;

                if (subset.Count() == 0)
                    return;

                // 逻辑上讲，不会出现异常
                subset.First().Server.Stop();
            }
        }

        public void AtServerTimerStart(string serverID, double interval, string timerCommand)
        {
            ServerUnit serverUnit = null;

            lock (serverTable) {
                foreach (var item in serverTable) {
                    if (item.ID.Equals(serverID) && item.Server is TcpServer) {
                        serverUnit = item;
                        break;
                    }
                }
            }

            if (serverUnit == null && !(serverUnit.Server is TcpServer))
                return;

            serverUnit.Timer = new System.Timers.Timer(interval);
            serverUnit.Timer.Elapsed += new System.Timers.ElapsedEventHandler((s, ea) =>
            {
                try {
                    (serverUnit.Server as TcpServer).Send(coding.GetBytes(timerCommand));
                }
                catch (Exception) { }
            });
            serverUnit.Timer.Start();
        }

        public void AtServerTimerStop(string serverID)
        {
            lock (serverTable) {
                foreach (var item in serverTable) {
                    if (item.ID.Equals(serverID) && item.Server is TcpServer) {
                        item.Timer.Stop();
                        item.Timer.Close();
                        break;
                    }
                }
            }
        }

        public void AtPluginLoad(string filePath)
        {
            string pluginID = null;
            PluginItem plugin = new PluginItem();

            try {
                plugin.Load(filePath);
            }
            catch (Exception ex) {
                MessageBox.Show(ex.Message, "Error");
                return;
            }

            try {
                pluginID = (string)plugin.Invoke("IDataHandle", "GetPluginID", null);
                plugin.Invoke("IDataHandle", "StartHandleMsg", null);
            }
            catch (Exception ex) {
                plugin.UnLoad();
                MessageBox.Show(ex.Message, "Error");
                return;
            }

            // 加载模块已经成功
            PluginUnit pluginUnit = new PluginUnit();
            pluginUnit.ID = pluginID;
            pluginUnit.Name = FileVersionInfo.GetVersionInfo(filePath).ProductName;
            pluginUnit.Schema = MnnUnitSchema.Plugin;
            pluginUnit.FileName = plugin.AssemblyName;
            pluginUnit.FilePath = filePath;
            pluginUnit.Plugin = plugin;

            // 加入 table
            rwlock.AcquireWriterLock(2000);
            pluginTable.Add(pluginUnit);
            rwlock.ReleaseWriterLock();

            App.Mindow.AddPlugin(pluginUnit);
        }

        public void AtPluginUnload(string fileName)
        {
            rwlock.AcquireWriterLock(2000);

            var subset = from s in pluginTable where s.FileName.Equals(fileName) select s;
            if (subset.Count() != 0) {
                try {
                    subset.First().Plugin.Invoke("IDataHandle", "StopHandleMsg", null);
                }
                catch (Exception) { }
                // 卸载模块
                subset.First().Plugin.UnLoad();
                // 移出 table
                App.Mindow.RemovePlugin(subset.First());
                pluginTable.Remove(subset.First());
            }

            rwlock.ReleaseWriterLock();
        }

        public void AtClientSendMessage(string serverID, string clientID, string msg)
        {
            // Find IPEndPoint of Client
            IPEndPoint ep = null;
            lock (clientTable) {
                var subset = from s in clientTable where s.ID.Equals(clientID) select s;
                if (subset.Count() != 0)
                    ep = subset.First().RemoteEP;
            }
            if (ep == null)
                return;

            lock (serverTable) {
                try {
                    var subset = from s in serverTable where s.ID.Equals(serverID) select s;
                    if (subset.Count() != 0)
                        subset.First().Server.Send(ep, coding.GetBytes(msg));
                }
                catch (Exception) { }
            }
        }

        public void AtClientClose(string serverID, string clientID)
        {
            // Find IPEndPoint of Client
            IPEndPoint ep = null;
            lock (clientTable) {
                var subset = from s in clientTable where s.ID.Equals(clientID) select s;
                if (subset.Count() != 0)
                    ep = subset.First().RemoteEP;
            }
            if (ep == null)
                return;

            // Close Client
            lock (serverTable) {
                var subset = from s in serverTable where s.ID.Equals(serverID) select s;
                if (subset.Count() != 0 && subset.First().Server is TcpServer) {
                    TcpServer tcp = subset.First().Server as TcpServer;
                    tcp.CloseClient(ep);
                }
            }
        }

    }
}
