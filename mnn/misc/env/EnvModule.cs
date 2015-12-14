using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Xml;
using mnn.misc.module;
using mnn.net;

namespace mnn.misc.env {
    public abstract  class EnvModule : IModule {
        protected SockClientTcp tcp;
        protected abstract string LogPrefix { get; }
        protected abstract string ErrLogPrefix { get; }

        public EnvModule()
        {
            XmlDocument xdoc = new XmlDocument();
            xdoc.Load(EnvConst.CONF_PATH);

            foreach (XmlNode item in xdoc.SelectNodes(EnvConst.CONF_SERVER)) {
                if (item.Attributes["target"].Value == "center" && item.Attributes["protocol"].Value == "tcp") {
                    tcp = new SockClientTcp(new IPEndPoint(
                            IPAddress.Parse(item.Attributes["ipaddress"].Value),
                            int.Parse(item.Attributes["port"].Value)
                            ));
                    break;
                }
            }
        }

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
            } catch (Exception ex) {
                log4net.ILog log = log4net.LogManager.GetLogger(typeof(EnvModule));
                log.Error("Exception of sending msg to client.", ex);
            }
        }

        protected void SendClientMsg(string ip, int port, string msg, SockClientTcpSendCallback method = null)
        {
            string url = "/center/clientsend" + "?ip=" + ip + "&port=" + port + "&data=" + msg;

            try {
                tcp.SendEncryptUrl(url, method);
            } catch (Exception ex) {
                log4net.ILog log = log4net.LogManager.GetLogger(typeof(EnvModule));
                log.Error("Exception of sending msg to client.", ex);
            }
        }

        protected void SendClientMsgByCcid(string ccid, string msg, SockClientTcpSendCallback method = null)
        {
            string url = "/center/clientsendbyccid" + "?ccid=" + ccid + "&data=" + msg;

            try {
                tcp.SendEncryptUrl(url, method);
            } catch (Exception ex) {
                log4net.ILog log = log4net.LogManager.GetLogger(typeof(EnvModule));
                log.Error("Exception of sending msg to client.", ex);
            }
        }

        protected void SendClientUpdate(string ip, int port, string ccid, string name, SockClientTcpSendCallback method = null)
        {
            string url = "/center/clientupdate" + "?ip=" + ip + "&port=" + port + "&ccid=" + ccid + "&name=" + name;

            try {
                tcp.SendEncryptUrl(url, method);
            } catch (Exception ex) {
                log4net.ILog log = log4net.LogManager.GetLogger(typeof(EnvModule));
                log.Error("Exception of sending msg to client.", ex);
            }
        }
    }
}
