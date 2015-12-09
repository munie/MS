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
        protected SockClientTcp tcp = new SockClientTcp(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2000));

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

        protected void SendClientClose(string ip, int port)
        {
            string url = "/center/clientclose"
                + "?type=accept" + "&ip=" + ip + "&port=" + port;

            try {
                tcp.SendEncryptUrl(url);
            } catch (Exception) { }
        }

        protected void SendClientMsg(string ip, int port, string msg)
        {
            string url = "/center/clientsend"
                + "?type=accept" + "&ip=" + ip + "&port=" + port + "&data=" + msg;

            try {
                tcp.SendEncryptUrl(url);
            } catch (Exception) { }
        }

        protected void SendClientMsgByCcid(string ccid, string msg)
        {
            string url = "/center/clientsendbyccid"
                + "?type=accept" + "&ccid=" + ccid + "&data=" + msg;

            try {
                tcp.SendEncryptUrl(url);
            } catch (Exception) { }
        }

        protected void SendClientUpdate(string ip, int port, string ccid, string name)
        {
            string url = "/center/clientupdate"
                + "?ip=" + ip + "&port=" + port + "&ccid=" + ccid + "&name=" + name;

            try {
                tcp.SendEncryptUrl(url);
            } catch (Exception) { }
        }
    }
}
