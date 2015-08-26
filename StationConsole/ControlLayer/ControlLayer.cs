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
using Mnn.MnnAtCmd;

namespace StationConsole
{
    class ControlLayer
    {
        public void Run()
        {
            foreach (var item in Program.DLayer.atCmdServerConfigTable) {
                if (item.Protocol == "udp") {
                    AtCmdSockServer<Mnn.MnnSocket.UdpServer> sockServer =
                        new AtCmdSockServer<Mnn.MnnSocket.UdpServer>();
                    sockServer.ExecCommand += new ExecuteAtCmdDeleagte(AtCmdServer_ExecCommand);
                    sockServer.Run(new IPEndPoint(IPAddress.Parse(item.IpAddress), Convert.ToInt32(item.Port)));
                }
                else if (item.Protocol == "tcp") {
                    AtCmdSockServer<Mnn.MnnSocket.TcpServer> sockServer =
                        new AtCmdSockServer<Mnn.MnnSocket.TcpServer>();
                    sockServer.ExecCommand += new ExecuteAtCmdDeleagte(AtCmdServer_ExecCommand);
                    sockServer.Run(new IPEndPoint(IPAddress.Parse(item.IpAddress), Convert.ToInt32(item.Port)));
                }
                else if (item.Protocol == "pipe") {
                    AtCmdPipeServer pipeServer = new AtCmdPipeServer();
                    pipeServer.ExecCommand += new ExecuteAtCmdDeleagte(AtCmdServer_ExecCommand);
                    pipeServer.Run(item.PipeName);
                }
            }
        }

        void AtCmdServer_ExecCommand(AtCmdUnit atCmdUnit)
        {
            if (atCmdUnit.Schema == AtCmdUnitSchema.ClientPoint && atCmdUnit.Direct == AtCmdUnitDirect.Respond)
            {
                if (atCmdUnit.Type == AtCmdUnitType.ClientConnect) {
                    StringReader sr = new StringReader(atCmdUnit.Data);
                    XmlSerializer xmlFormat = new XmlSerializer(typeof(ClientPointUnit));
                    ClientPointUnit clientPoint = xmlFormat.Deserialize(sr) as ClientPointUnit;

                    Program.MWindow.AddClientPoint(clientPoint);
                }
                else if (atCmdUnit.Type == AtCmdUnitType.ClientDisconn) {
                    //string[] strTmp = atCmdUnit.Data.Split(":".ToArray());
                    //Program.mainWindow.RemoveClientPoint(
                    //    new IPEndPoint(IPAddress.Parse(strTmp[0]), Convert.ToInt32(strTmp[1])));

                    Program.MWindow.RemoveClientPoint(atCmdUnit.Data);
                }
                else if (atCmdUnit.Type == AtCmdUnitType.ClientReadMsg ||
                    atCmdUnit.Type == AtCmdUnitType.ClientSendMsg) {
                    Program.MWindow.DisplayMessage(atCmdUnit.Data);
                }
                else if (atCmdUnit.Type == AtCmdUnitType.ClientUpdate) {
                    StringReader sr = new StringReader(atCmdUnit.Data);
                    XmlSerializer xmlFormat = new XmlSerializer(typeof(ClientPointUnit));
                    ClientPointUnit clientPoint = xmlFormat.Deserialize(sr) as ClientPointUnit;

                    Program.MWindow.UpdateClientPoint(clientPoint);
                }
            }
        }

        // At Commands ========================================================================

        public void AtLoadPlugin(string filePath)
        {
            int listenPort = 0;
            DataHandlePlugin dataHandle = new DataHandlePlugin();

            try {
                listenPort = dataHandle.LoadDataHandlePlugin(filePath);
                dataHandle.InitializeSource();
            }
            catch (Exception ex) {
                MessageBox.Show(ex.Message, "Error");
                return;
            }

            // 加载模块已经成功
            dataHandle.ListenPort = listenPort;
            dataHandle.ListenState = DataHandlePlugin.ListenStateStoped;
            dataHandle.ChineseName = FileVersionInfo.GetVersionInfo(filePath).ProductName;
            dataHandle.FileName = dataHandle.AssemblyName;
            dataHandle.TimerState = DataHandlePlugin.TimerStateStoped;
            dataHandle.TimerInterval = 0;
            dataHandle.TimerCommand = "";

            // 加入 table
            Program.MWindow.AddDataHandle(dataHandle);
        }

        public void AtUnLoadPlugin(DataHandlePlugin dataHandlePlugin)
        {
            // 关闭端口
            if (dataHandlePlugin.ListenState == DataHandlePlugin.ListenStateStarted) {
                dataHandlePlugin.StopListener();
                dataHandlePlugin.ListenState = DataHandlePlugin.ListenStateStoped;
            }

            // 关闭定时器
            if (dataHandlePlugin.TimerState == DataHandlePlugin.TimerStateStarted) {
                dataHandlePlugin.StopTimerCommand();
                dataHandlePlugin.TimerState = DataHandlePlugin.TimerStateStoped;
            }

            // 卸载模块
            dataHandlePlugin.UnloadDataHandlePlugin();

            // 移出 table
            Program.MWindow.RemoveDataHandle(dataHandlePlugin);
        }

        public void AtStartListener(DataHandlePlugin dataHandlePlugin)
        {
            if (dataHandlePlugin.ListenState == DataHandlePlugin.ListenStateStarted)
                return;

            // 端口可能已经被其他程序监听
            try {
                dataHandlePlugin.StartListener(new IPEndPoint(Program.DLayer.ipAddress, dataHandlePlugin.ListenPort));
                dataHandlePlugin.ListenState = DataHandlePlugin.ListenStateStarted;
            }
            catch (Exception ex) {
                MessageBox.Show(ex.Message, "Error");
            }
        }

        public void AtStopListener(DataHandlePlugin dataHandlePlugin)
        {
            if (dataHandlePlugin.ListenState == DataHandlePlugin.ListenStateStoped)
                return;

            // 逻辑上讲，不会出现异常
            dataHandlePlugin.StopListener();
            dataHandlePlugin.ListenState = DataHandlePlugin.ListenStateStoped;
        }

        public void AtSetListener(DataHandlePlugin dataHandlePlugin, int listenPort)
        {
            if (dataHandlePlugin.ListenState == DataHandlePlugin.ListenStateStarted)
                return;

            dataHandlePlugin.ListenPort = listenPort;
        }

        public void AtStartTimer(DataHandlePlugin dataHandlePlugin)
        {
            if (dataHandlePlugin.TimerState == DataHandlePlugin.TimerStateStarted ||
                dataHandlePlugin.TimerInterval <= 0 || dataHandlePlugin.TimerCommand == "")
                return;

            dataHandlePlugin.StartTimerCommand(dataHandlePlugin.TimerInterval * 1000, dataHandlePlugin.TimerCommand);
            dataHandlePlugin.TimerState = DataHandlePlugin.TimerStateStarted;
        }

        public void AtStopTimer(DataHandlePlugin dataHandlePlugin)
        {
            if (dataHandlePlugin.TimerState == DataHandlePlugin.TimerStateStoped)
                return;

            dataHandlePlugin.StopTimerCommand();
            dataHandlePlugin.TimerState = DataHandlePlugin.TimerStateStoped;
        }

        public void AtSetTimer(DataHandlePlugin dataHandlePlugin, string cmd, double interval)
        {
            if (dataHandlePlugin.TimerState == DataHandlePlugin.TimerStateStarted)
                return;

            dataHandlePlugin.TimerCommand = cmd;
            dataHandlePlugin.TimerInterval = interval;
        }

        public void AtClientSendMessage(ClientPoint client, string msg)
        {
            lock (Program.DLayer.dataHandlePluginTable) {
                try {
                    string[] strTmp = client.RemoteIP.Split(":".ToArray());
                    var subset = from s in Program.DLayer.dataHandlePluginTable where s.ListenPort == client.LocalPort select s;
                    if (subset.Count() != 0)
                        subset.First().SendClientCommand(new IPEndPoint(IPAddress.Parse(strTmp[0]), Convert.ToInt32(strTmp[1])),
                                            Program.DLayer.coding.GetBytes(msg));
                }
                catch (Exception) { }
            }
        }

        public void AtClientClose(ClientPoint client)
        {
            lock (Program.DLayer.dataHandlePluginTable) {
                string[] strTmp = client.RemoteIP.Split(":".ToArray());

                var subset = from s in Program.DLayer.dataHandlePluginTable where s.ListenPort == client.LocalPort select s;
                if (subset.Count() != 0)
                    subset.First().CloseClient(new IPEndPoint(IPAddress.Parse(strTmp[0]), Convert.ToInt32(strTmp[1])));
            }
        }

    }
}
