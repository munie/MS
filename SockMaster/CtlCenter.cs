using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Net;
using mnn.net;

namespace SockMaster {
    class CtlCenter : ControlCenter {
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
            sessctl.MakeListen(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 5964));

            // dispatcher register
            dispatcher.RegisterDefaultController("default_controller", default_controller);
            dispatcher.Register("sock_open_controller", sock_open_controller, 0x0C01);
            dispatcher.Register("sock_close_controller", sock_close_controller, 0x0C02);
            dispatcher.Register("sock_send_controller", sock_send_controller, 0x0C03);
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
                    //sock.Type = item.Attributes["type"].Value == "listen" ? SockType.listen : SockType.connect;
                    string[] str = item.Attributes["ep"].Value.Split(':');
                    if (str.Count() == 2)
                        sock.EP = new IPEndPoint(IPAddress.Parse(str[0]), int.Parse(str[1]));
                    sock.Autorun = bool.Parse(item.Attributes["autorun"].Value);
                    sock.UpdateTitle();
                    DataUI.SockTable.Add(sock);

                    if (sock.Autorun) {
                        SockSess result;
                        if (sock.Type == SockType.listen)
                            result = sessctl.MakeListen(sock.EP);
                        else
                            result = sessctl.AddConnect(sock.EP);
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

        // Override Session Event ==========================================================================

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

        // Self Request Controller =========================================================================

        private Dictionary<string, string> msg_parse(string msg)
        {
            Dictionary<string, string> dc = new Dictionary<string, string>();

            msg = msg.Replace(", ", ",");
            string[] values = msg.Split(',');
            foreach (var item in values) {
                string[] tmp = item.Split(':');
                dc.Add(tmp[0], tmp[1]);
            }

            return dc;
        }

        private void default_controller(SockRequest request, SockResponse response)
        {
            string log = DateTime.Now + " (" + request.rep.ToString() + " => " + request.lep.ToString() + ")\n";
            log += SockConvert.ParseBytesToString(request.data) + "\n\n";

            /// ** update DataUI
            DataUI.Logger(log);
        }

        private void sock_open_controller(SockRequest request, SockResponse response)
        {
            string msg = Encoding.UTF8.GetString(request.data.Skip(2).ToArray());
            Dictionary<string, string> dc = msg_parse(msg);
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), int.Parse(dc["port"]));

            SockSess result = null;
            if (dc["type"] == SockType.listen.ToString())
                result = sessctl.MakeListen(ep);
            else if (dc["type"] == SockType.connect.ToString())
                result = sessctl.AddConnect(ep);

            /// ** update DataUI
            if (result != null)
                DataUI.SockOpen(ep);
            else
                DataUI.SockClose(ep);
        }

        private void sock_close_controller(SockRequest request, SockResponse response)
        {
            string msg = Encoding.UTF8.GetString(request.data.Skip(2).ToArray());
            Dictionary<string, string> dc = msg_parse(msg);
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), int.Parse(dc["port"]));

            foreach (var item in sessctl.sess_table) {
                IPEndPoint tmpep;
                if (item.type == SockType.listen)
                    tmpep = item.lep;
                else
                    tmpep = item.rep;
                if (tmpep.Equals(ep)) {
                    sessctl.DelSession(item);
                    break;
                }
            }

            /// ** update DataUI
            DataUI.SockClose(ep);
        }

        private void sock_send_controller(SockRequest request, SockResponse response)
        {
            string msg = Encoding.UTF8.GetString(request.data.Skip(2).ToArray());
            Dictionary<string, string> dc = msg_parse(msg);
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), int.Parse(dc["port"]));

            foreach (var item in sessctl.sess_table) {
                IPEndPoint tmpep;
                if (item.type == SockType.listen)
                    tmpep = item.lep;
                else
                    tmpep = item.rep;
                if (tmpep.Equals(ep)) {
                    sessctl.SendSession(item, Encoding.UTF8.GetBytes(dc["data"]));
                    break;
                }
            }
        }
    }
}
