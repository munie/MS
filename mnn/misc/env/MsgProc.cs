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
using mnn.net;

namespace mnn.misc.env {
    public abstract class MsgProc : module.IModule, IMsgProc {
        protected abstract string LogPrefix { get; }
        protected abstract string ErrLogPrefix { get; }
        protected Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        // IModule ========================================================================

        public void Init() { }

        public void Final() { }

        public abstract string GetModuleID();

        public abstract string GetModuleInfo();

        // IMsgProc ========================================================================

        public abstract void HandleMsg(SockRequest request, SockResponse response);

        protected abstract void HandleAlive(SockRequest request, SockResponse response, IDictionary<string, string> dc);

        protected abstract void HandleAlarm(SockRequest request, SockResponse response, IDictionary<string, string> dc);

        protected abstract void HandleDetect(SockRequest request, SockResponse response, IDictionary<string, string> dc);

        // Private Tools ===========================================================================

        private bool IsSocketConnected(Socket client)
        {
            bool blockingState = client.Blocking;
            try {
                byte[] tmp = new byte[1];
                client.Blocking = false;
                client.Send(tmp, 0, 0);
                return true;
            } catch (SocketException e) {
                // 产生 10035 == WSAEWOULDBLOCK 错误，说明被阻止了，但是还是连接的
                if (e.NativeErrorCode.Equals(10035))
                    return true;
                else
                    return false;
            } finally {
                client.Blocking = blockingState;    // 恢复状态
            }
        }

        private void SendToClient(string url)
        {
            url = mnn.net.EncryptSym.AESEncrypt(url);
            byte[] buffer = Encoding.UTF8.GetBytes(url);
            buffer = new byte[] { 0x01, 0x0C, (byte)(0x04 + buffer.Length & 0xff), (byte)(0x04 + buffer.Length >> 8 & 0xff) }
                .Concat(buffer).ToArray();

            try {
                if (!IsSocketConnected(socket))
                    socket.Connect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2000));
                socket.Send(buffer);
            } catch (Exception ex) {
                util.Logger.WriteException(ex, ErrLogPrefix);
            }
        }

        protected void SendClientClose(string ip, int port)
        {
            string url = "/center/clientclose"
                + "?type=accept" + "&ip=" + ip + "&port=" + port;

            SendToClient(url);
        }

        protected void SendClientMsg(string ip, int port, string msg)
        {
            string url = "/center/clientsend"
                + "?type=accept" + "&ip=" + ip + "&port=" + port + "&data=" + msg;

            SendToClient(url);
        }

        protected void SendClientMsgByCcid(string ccid, string msg)
        {
            string url = "/center/clientsendbyccid"
                + "?type=accept" + "&ccid=" + ccid + "&data=" + msg;

            SendToClient(url);
        }

        protected void SendClientUpdate(string ip, int port, string ccid, string name)
        {
            string url = "/center/clientupdate"
                + "?ip=" + ip + "&port=" + port + "&ccid=" + ccid + "&name=" + name;

            SendToClient(url);
        }
    }
}
