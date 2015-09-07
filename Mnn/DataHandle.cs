using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using Mnn.MnnUnit;

namespace Mnn
{
    public abstract class DataHandle : Mnn.MnnModule.IModule, IDataHandle
    {
        class DataHandleMsg
        {
            public IPEndPoint EP;
            public string Content;
        }

        // Fileds for Main Thread
        private const int max_msg_count = 1000;
        private bool isExitThread = false;
        private Semaphore sem = new Semaphore(0, max_msg_count);
        private Queue<DataHandleMsg> msgQueue = new Queue<DataHandleMsg>();

        // Socket for sending @Cmd to StationConsole
        private Socket atCmdSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        private IPEndPoint atCmdEP = null;
        //private IPEndPoint atCmdEP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2000);

        public abstract string LogPrefix { get; }
        public abstract string ErrLogPrefix { get; }

        // IDataHandle Interface ===================================================================

        public abstract string GetModuleID();

        public void Init()
        {
            //Thread thread = new Thread(() =>
            //{
            //    isExitThread = false;
            //    DataHandleMsg msg = null;

            //    while (true) {
            //        if (isExitThread == true) {
            //            isExitThread = false;
            //            break;
            //        }

            //        sem.WaitOne();
            //        lock (msgQueue) {
            //            msg = msgQueue.Dequeue();
            //        }
            //        try {
            //            HandleMsg(msg.EP, msg.Content);
            //        }
            //        catch (Exception ex) {
            //            MnnUtil.Logger.WriteException(ex, ErrLogPrefix);
            //        }
            //    }
            //});

            //thread.IsBackground = true;
            //thread.Start();
        }

        public void Final()
        {
            isExitThread = true;
        }

        public void AppendMsg(System.Net.IPEndPoint ep, string msg)
        {
            if (msgQueue.Count() >= max_msg_count)
                return;

            lock (msgQueue) {
                msgQueue.Enqueue(new DataHandleMsg() { EP = ep, Content = msg });
            }
            sem.Release();
        }

        public abstract void HandleMsg(IPEndPoint ep, string msg);

        public abstract void HandleAlive(IPEndPoint ep, IDictionary<string, string> dc);

        public abstract void HandleAlarm(IPEndPoint ep, IDictionary<string, string> dc);

        public abstract void HandleDetect(IPEndPoint ep, IDictionary<string, string> dc);

        public virtual void AtCmdResult(AtCommand atCmd) { }

        // Private Tools ===========================================================================

        protected void SendAtCmd(AtCommand atCmd)
        {
            if (atCmdEP == null) {
                try {
                    XmlDocument xdoc = new XmlDocument();
                    xdoc.Load(AppDomain.CurrentDomain.BaseDirectory + "\\config.xml");

                    foreach (XmlNode item in xdoc.SelectNodes("/configuration/serverconfig/server")) {
                        if (item.Attributes["type"].Value == "atcmd" && item.Attributes["protocol"].Value == "udp") {
                            atCmdEP = new IPEndPoint(
                                IPAddress.Parse(item.Attributes["ipaddress"].Value),
                                int.Parse(item.Attributes["port"].Value));
                            break;
                        }
                    }
                }
                catch (Exception) { }

                if (atCmdEP == null)
                    atCmdEP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2000);
            }

            byte[] atCmdBuffer = new byte[2048];
            MemoryStream memoryStream = new MemoryStream(atCmdBuffer);
            XmlSerializer xmlFormat = new XmlSerializer(typeof(AtCommand));
            //xmlFormat.Serialize(atCmdPipeClientStream, atCmdUnit);
            xmlFormat.Serialize(memoryStream, atCmd);

            atCmdSocket.SendTo(atCmdBuffer, (int)memoryStream.Position, SocketFlags.None, atCmdEP);
            memoryStream.Close();
        }

        protected void SendAtCmdClientClose(string ccid, IPEndPoint ep)
        {
            /// ** Report ClientUpdateID to Console
            AtCommand atCmd = new AtCommand();
            atCmd.Direct = AtCommandDirect.Request;
            atCmd.ID = Guid.NewGuid().ToString();
            atCmd.FromID = GetModuleID();
            atCmd.FromSchema = UnitSchema.Module;
            atCmd.ToID = ccid;
            atCmd.ToSchema = UnitSchema.Client;
            atCmd.ToEP = ep.ToString();
            atCmd.DataType = AtCommandDataType.ClientClose;
            atCmd.Data = "";
            SendAtCmd(atCmd);
        }

        protected void SendAtCmdClientUpdate(string ccid, string name, IPEndPoint ep)
        {
            /// ** Report ClientUpdateID to Console
            AtCommand atCmd = new AtCommand();
            atCmd.Direct = AtCommandDirect.Request;
            atCmd.ID = Guid.NewGuid().ToString();
            atCmd.FromID = GetModuleID();
            atCmd.FromSchema = UnitSchema.Module;
            atCmd.ToID = ccid;
            atCmd.ToSchema = UnitSchema.Client;
            atCmd.ToEP = ep.ToString();
            atCmd.DataType = AtCommandDataType.ClientUpdateID;
            atCmd.Data = ccid;
            SendAtCmd(atCmd);

            /// ** Report ClientUpdateName to Console
            atCmd = new AtCommand();
            atCmd.Direct = AtCommandDirect.Request;
            atCmd.ID = Guid.NewGuid().ToString();
            atCmd.FromID = GetModuleID();
            atCmd.FromSchema = UnitSchema.Module;
            atCmd.ToID = ccid;
            atCmd.ToSchema = UnitSchema.Client;
            atCmd.ToEP = ep.ToString();
            atCmd.DataType = AtCommandDataType.ClientUpdateName;
            atCmd.Data = name;
            SendAtCmd(atCmd);
        }

        protected void SendAtCmdClientSendMsg(string ccid, IPEndPoint ep, string msg)
        {
            /// ** Report ClientUpdateID to Console
            AtCommand atCmd = new AtCommand();
            atCmd.Direct = AtCommandDirect.Request;
            atCmd.ID = Guid.NewGuid().ToString();
            atCmd.FromID = GetModuleID();
            atCmd.FromSchema = UnitSchema.Module;
            atCmd.ToID = ccid;
            atCmd.ToSchema = UnitSchema.Client;
            atCmd.ToEP = ep.ToString();
            atCmd.DataType = AtCommandDataType.ClientSendMsg;
            atCmd.Data = msg;
            SendAtCmd(atCmd);

            string logFormat = ep.ToString() + " " + DateTime.Now.ToString() + "发送数据：" + msg;
            MnnUtil.Logger.Write(logFormat, LogPrefix);
        }

    }
}
