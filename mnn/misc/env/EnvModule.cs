using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Xml;
using mnn.module;
using mnn.net;
using Newtonsoft.Json;

namespace mnn.misc.env {
    public abstract  class EnvModule : IModule {
        protected SockClientTcp tcp;
        protected abstract string LogPrefix { get; }
        protected abstract string ErrLogPrefix { get; }

        public EnvModule() { }

        // IModule ========================================================================

        public virtual void Init()
        {
            XmlDocument xdoc = new XmlDocument();
            xdoc.Load(EnvConst.CONF_PATH);

            foreach (XmlNode item in xdoc.SelectNodes(EnvConst.CONF_SERVER)) {
                if (item.Attributes["protocol"].Value == "tcp") {
                    tcp = new SockClientTcp(new IPEndPoint(
                            IPAddress.Parse(item.Attributes["ipaddress"].Value),
                            int.Parse(item.Attributes["port"].Value)
                            ));
                    break;
                }
            }
        }

        public virtual void Final()
        {
            tcp.Dispose();
        }

        // Private Tools ===========================================================================

        public void SessCloseRequest(string sessid, SockClientTcpSendCallback method = null)
        {
            object req = new {
                id = "service.sessclose",
                sessid = sessid,
            };

            tcp.Send(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(req)), method);
        }

        public void SessSendRequest(string sessid, string msg, SockClientTcpSendCallback method = null)
        {
            object req = new {
                id = "service.sesssend",
                sessid = sessid,
                data = msg,
            };

            tcp.Send(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(req)), method);
        }

        public void SessDetailRequest(SockClientTcpSendCallback method = null)
        {
            tcp.Send(Encoding.UTF8.GetBytes("{'id':'service.sessdetail'}"), method);
        }
    }
}
