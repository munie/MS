using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Net;
using mnn.net;
using mnn.util;

namespace SockMaster {
    class CtlCenter : ControlCenter {
        public static readonly string BASE_DIR = System.AppDomain.CurrentDomain.BaseDirectory;
        public static readonly string CONF_NAME = "SockMaster.xml";
        public static readonly string CONF_PATH = BASE_DIR + CONF_NAME;
        public static readonly string SOCK_OPEN = "sock_open";
        public static readonly string SOCK_CLOSE = "sock_close";
        public static readonly string SOCK_SEND = "sock_send";

        // self request control
        public AtCmdCtl cmdctl;
        // hook for ui to display socket infomation
        public DataUI DataUI { get; set; }

        public void Init()
        {
            // init cmdcer
            cmdctl = new AtCmdCtl();
            cmdctl.Register(SOCK_OPEN, sock_open);
            cmdctl.Register(SOCK_CLOSE, sock_close);
            cmdctl.Register(SOCK_SEND, sock_send);

            // init SockTable
            DataUI = new DataUI();
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

                // socket
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
                        sock.State = SockState.Opening;
                        cmdctl.AppendCommand(SOCK_OPEN, sock);
                    }
                }
            } catch (Exception) {
                System.Windows.MessageBox.Show(CONF_NAME + ": syntax error.");
            }
        }

        public void Perform(int next)
        {
            sessctl.Perform(next);
            cmdctl.Perform(0);
        }

        // Session Event ==================================================================================

        protected override void sess_parse(object sender, SockSess sess)
        {
            byte[] data = sess.rdata.Take(sess.rdata_size).ToArray();
            sess.rdata_size = 0;

            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                DataUI.Log = DateTime.Now + " (" + sess.rep.ToString() + " => " + sess.lep.ToString() + ")\n";
                DataUI.Log = SockConvert.ParseBytesToString(data) + "\n\n";
            }));
        }

        protected override void sess_create(object sender, SockSess sess)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                // update SockTable
                if (sess.type == SockType.accept) {
                    var subset = from s in DataUI.SockTable
                                 where s.Type == SockType.listen && s.EP.Port == sess.lep.Port
                                 select s;
                    foreach (var item in subset) {
                        item.Childs.Add(new SockUnit()
                        {
                            ID = "-",
                            Name = "accept",
                            Type = sess.type,
                            Sess = sess,
                            EP = sess.rep,
                            State = SockState.Opened,
                        });
                        break;
                    }
                }
            }));
        }

        protected override void sess_delete(object sender, SockSess sess)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                // update SockTable
                if (sess.type == SockType.accept) {
                    foreach (var item in DataUI.SockTable) {
                        if (item.Childs.Count == 0) continue;

                        foreach (var child in item.Childs) {
                            if (child.Sess == sess) {
                                item.Childs.Remove(child);
                                return;
                            }
                        }
                    }
                }
            }));
        }

        // Self Request Control ===========================================================================

        private void sock_open(object arg)
        {
            SockUnit sock = arg as SockUnit;
            if (sock == null || sock.State == SockState.Opened) return;

            SockSess sess = null;
            if (sock.Type == SockType.listen) {
                sess = sessctl.MakeListen(sock.EP);
            } else if (sock.Type == SockType.connect) {
                sess = sessctl.AddConnect(sock.EP);
            }

            if (sess != null) {
                sock.Sess = sess;
                sock.State = SockState.Opened;
            } else {
                sock.State = SockState.Closed;
            }
        }

        private void sock_close(object arg)
        {
            SockUnit sock = arg as SockUnit;
            if (sock == null || sock.State == SockState.Closed) return;

            sessctl.DelSession(sock.Sess);
            sock.State = SockState.Closed;
        }

        private void sock_send(object arg)
        {
            SockUnit sock = arg as SockUnit;
            if (sock == null || sock.State != SockState.Opened) return;

            if (sock.SendBuff != null) {
                sessctl.SendSession(sock.Sess, sock.SendBuff);
                sock.SendBuff = null;
            }
        }
    }
}
