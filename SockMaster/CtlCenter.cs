using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Net;
using mnn.net;

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
            sessctl.MakeListen(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 5964));

            // dispatcher register
            dispatcher.RegisterDefaultService("default_service", default_service);
            dispatcher.Register("sock_open_service", sock_open_service, Encoding.UTF8.GetBytes("/center/sockopen"));
            dispatcher.Register("sock_close_service", sock_close_service, Encoding.UTF8.GetBytes("/center/sockclose"));
            dispatcher.Register("sock_send_service", sock_send_service, Encoding.UTF8.GetBytes("/center/socksend"));
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

        private void default_service(SockRequest request, SockResponse response)
        {
            string log = DateTime.Now + " (" + request.rep.ToString() + " => " + request.lep.ToString() + ")\n";
            log += SockConvert.ParseBytesToString(request.data) + "\n\n";

            /// ** update DataUI
            DataUI.Logger(log);
        }

        private void sock_open_service(SockRequest request, SockResponse response)
        {
            string msg = Encoding.UTF8.GetString(request.data);
            if (!msg.Contains('?')) return;
            msg = msg.Substring(msg.IndexOf('?') + 1);

            IDictionary<string, string> dc = SockConvert.ParseHttpQueryParam(msg);
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), int.Parse(dc["port"]));
            SockSess result = null;

            if (dc["type"] == SockType.listen.ToString() && sessctl.FindSession(SockType.listen, ep, null) == null)
                result = sessctl.MakeListen(ep);
            else if (dc["type"] == SockType.connect.ToString() && sessctl.FindSession(SockType.connect, null, ep) == null)
                result = sessctl.AddConnect(ep);
            else
                return;

            /// ** update DataUI
            if (result != null)
                DataUI.SockOpen(ep);
            else
                DataUI.SockClose(ep);
        }

        private void sock_close_service(SockRequest request, SockResponse response)
        {
            string msg = Encoding.UTF8.GetString(request.data);
            if (!msg.Contains('?')) return;
            msg = msg.Substring(msg.IndexOf('?') + 1);

            IDictionary<string, string> dc = SockConvert.ParseHttpQueryParam(msg);
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), int.Parse(dc["port"]));
            SockSess result = null;

            if (dc["type"] == SockType.listen.ToString())
                result = sessctl.FindSession(SockType.listen, ep, null);
            else if (dc["type"] == SockType.connect.ToString())
                result = sessctl.FindSession(SockType.connect, null, ep);
            else// if (dc["type"] == SockType.accept.ToString())
                result = sessctl.FindSession(SockType.accept, null, ep);

            if (result != null)
                sessctl.DelSession(result);

            /// ** update DataUI
            if (result != null)
                DataUI.SockClose(ep);
        }

        private void sock_send_service(SockRequest request, SockResponse response)
        {
            // retrieve param_list of url
            string param_list = Encoding.UTF8.GetString(request.data);
            if (!param_list.Contains('?')) return;
            param_list = param_list.Substring(param_list.IndexOf('?') + 1);

            // retrieve param_data
            int index_data = param_list.IndexOf("&data=");
            if (index_data == -1) return;
            string param_data = param_list.Substring(index_data + 6);
            param_list = param_list.Substring(0, index_data);

            IDictionary<string, string> dc = SockConvert.ParseHttpQueryParam(param_list);
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), int.Parse(dc["port"]));
            SockSess result = null;

            if (dc["type"] == SockType.listen.ToString())
                result = sessctl.FindSession(SockType.listen, ep, null);
            else if (dc["type"] == SockType.connect.ToString())
                result = sessctl.FindSession(SockType.connect, null, ep);
            else// if (dc["type"] == SockType.accept.ToString())
                result = sessctl.FindSession(SockType.accept, null, ep);

            if (result != null)
                sessctl.SendSession(result, Encoding.UTF8.GetBytes(param_data));
        }
    }
}
