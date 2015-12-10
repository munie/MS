using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using mnn.misc.module;
using mnn.net;

namespace mnn.misc.env {
    public abstract  class EnvModule : IModule {
        protected static readonly string BASE_DIR = System.AppDomain.CurrentDomain.BaseDirectory;
        protected static readonly string CONF_NAME = "EnvConsole.xml";
        protected static readonly string CONF_PATH = BASE_DIR + CONF_NAME;
        protected SockClientTcp tcp = new SockClientTcp(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2000));
        protected abstract string LogPrefix { get; }
        protected abstract string ErrLogPrefix { get; }

        // IModule ========================================================================

        public virtual void Init() { }

        public virtual void Final() { }

        public abstract string GetModuleID();

        // Private Tools ===========================================================================

        protected void SendClientClose(string ip, int port, SockClientTcpSendCallback method = null)
        {
            string url = "/center/clientclose" + "?ip=" + ip + "&port=" + port;

            try {
                tcp.SendEncryptUrl(url, method);
            } catch (Exception) { }
        }

        protected void SendClientMsg(string ip, int port, string msg, SockClientTcpSendCallback method = null)
        {
            string url = "/center/clientsend" + "?ip=" + ip + "&port=" + port + "&data=" + msg;

            try {
                tcp.SendEncryptUrl(url, method);
            } catch (Exception) { }
        }

        protected void SendClientMsgByCcid(string ccid, string msg, SockClientTcpSendCallback method = null)
        {
            string url = "/center/clientsendbyccid" + "?ccid=" + ccid + "&data=" + msg;

            try {
                tcp.SendEncryptUrl(url, method);
            } catch (Exception) { }
        }

        protected void SendClientUpdate(string ip, int port, string ccid, string name, SockClientTcpSendCallback method = null)
        {
            string url = "/center/clientupdate" + "?ip=" + ip + "&port=" + port + "&ccid=" + ccid + "&name=" + name;

            try {
                tcp.SendEncryptUrl(url, method);
            } catch (Exception) { }
        }
    }
}
