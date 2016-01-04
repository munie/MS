using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Net;
using mnn.net;
using mnn.net.ctlcenter;

namespace SockMaster {
    class CtlCenter : CtlCenterBase {
        public static readonly string BASE_DIR = System.AppDomain.CurrentDomain.BaseDirectory;
        public static readonly string CONF_NAME = "SockMaster.xml";
        public static readonly string CONF_PATH = BASE_DIR + CONF_NAME;

        // hook for ui to display socket infomation
        public DataUI DataUI { get; set; }

        public void Init()
        {
            /// ** init DataUI
            DataUI = new DataUI();

            // open ctlcenter port
            SockSess result = null;
            do {
                DataUI.Port = 5964 + new Random().Next() % 1024;
                result = sessctl.MakeListen(new IPEndPoint(IPAddress.Parse("0.0.0.0"), DataUI.Port));
            } while (result == null);

            // dispatcher register
            dispatcher.RegisterDefaultService("default_service", default_service);
            dispatcher.RegisterService("sock_open_service", sock_open_service, Encoding.UTF8.GetBytes("/center/sockopen"));
            dispatcher.RegisterService("sock_close_service", sock_close_service, Encoding.UTF8.GetBytes("/center/sockclose"));
            dispatcher.RegisterService("sock_send_service", sock_send_service, Encoding.UTF8.GetBytes("/center/socksend"));
        }

        public void Config()
        {
            if (File.Exists(BASE_DIR + CONF_NAME) == false) {
                System.Windows.MessageBox.Show(CONF_NAME + ": can't find it.");
                return;
            }

            try {
                XmlDocument doc = new XmlDocument();
                doc.Load(BASE_DIR + CONF_NAME);

                /// ** config DataUI
                foreach (XmlNode item in doc.SelectNodes("/configuration/sockets/sockitem")) {
                    SockUnit sock = new SockUnit();
                    sock.ID = item.Attributes["id"].Value;
                    sock.Name = item.Attributes["name"].Value;
                    sock.Type = (SockType)Enum.Parse(typeof(SockType), item.Attributes["type"].Value);
                    string[] str = item.Attributes["ep"].Value.Split(':');
                    IPEndPoint ep = new IPEndPoint(IPAddress.Parse(str[0]), int.Parse(str[1]));
                    sock.Lep = sock.Type == SockType.listen ? ep : null;
                    sock.Rep = sock.Type == SockType.connect ? ep : null;
                    sock.Autorun = bool.Parse(item.Attributes["autorun"].Value);
                    sock.UpdateTitle();
                    DataUI.SockTable.Add(sock);

                    if (sock.Autorun) {
                        SockSess result;
                        if (sock.Type == SockType.listen)
                            result = sessctl.MakeListen(sock.Lep);
                        else
                            result = sessctl.AddConnect(sock.Rep);
                        if (result == null)
                            sock.State = SockState.Closed;
                        else
                            sock.State = SockState.Opened;
                    }
                }
            } catch (Exception) {
                System.Windows.MessageBox.Show(CONF_NAME + ": syntax error.");
            }
        }

        public void Perform(int next)
        {
            sessctl.Perform(next);
        }

        // Session Event ==========================================================================

        protected override void sess_create(object sender, SockSess sess)
        {
            /// ** update DataUI
            DataUI.SockAdd(sess.type, sess.lep, sess.rep);
        }

        protected override void sess_delete(object sender, SockSess sess)
        {
            /// ** update DataUI
            DataUI.SockDel(sess.type, sess.lep, sess.rep);
        }

        // Center Service =========================================================================

        protected override void default_service(SockRequest request, ref SockResponse response)
        {
            string log = DateTime.Now + " (" + request.rep.ToString() + " => " + request.lep.ToString() + ")\n";
            log += SockConvert.ParseBytesToString(request.data) + "\n\n";

            /// ** update DataUI
            DataUI.Logger(log);
        }

        protected override void sock_open_service(SockRequest request, ref SockResponse response)
        {
            // get param string & parse to dictionary
            string msg = Encoding.UTF8.GetString(request.data);
            if (!msg.Contains('?')) return;
            msg = msg.Substring(msg.IndexOf('?') + 1);
            IDictionary<string, string> dc = SockConvert.ParseUrlQueryParam(msg);

            // find session and open
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), int.Parse(dc["port"]));
            SockSess result = null;
            if (dc["type"] == SockType.listen.ToString() && sessctl.FindSession(SockType.listen, ep, null) == null)
                result = sessctl.MakeListen(ep);
            else if (dc["type"] == SockType.connect.ToString())
                result = sessctl.AddConnect(ep);
            else
                result = null;

            /// ** update DataUI
            if (result != null)
                DataUI.SockOpen(dc["id"], result.lep, result.rep);
            else
                DataUI.SockClose(dc["id"]);
        }

        protected override void sock_close_service(SockRequest request, ref SockResponse response)
        {
            // get param string & parse to dictionary
            string msg = Encoding.UTF8.GetString(request.data);
            if (!msg.Contains('?')) return;
            msg = msg.Substring(msg.IndexOf('?') + 1);
            IDictionary<string, string> dc = SockConvert.ParseUrlQueryParam(msg);

            // find session
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), int.Parse(dc["port"]));
            SockSess result = null;
            if (dc["type"] == SockType.listen.ToString())
                result = sessctl.FindSession(SockType.listen, ep, null);
            else if (dc["type"] == SockType.connect.ToString())
                result = sessctl.FindSession(SockType.connect, ep, null);
            else// if (dc["type"] == SockType.accept.ToString())
                result = sessctl.FindSession(SockType.accept, null, ep);

            // close session
            if (result != null)
                sessctl.DelSession(result);

            /// ** update DataUI
            if (result != null)
                DataUI.SockClose(dc["id"]);
        }
    }
}
