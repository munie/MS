using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Net;
using mnn.misc.glue;
using mnn.net;
using mnn.service;

namespace SockMaster {
    class Core : BaseLayerNew {
        public static readonly string BASE_DIR = System.AppDomain.CurrentDomain.BaseDirectory;
        public static readonly string CONF_NAME = "SockMaster.xml";
        public static readonly string CONF_PATH = BASE_DIR + CONF_NAME;

        // hook for ui to display socket information
        public DataUI DataUI { get; set; }

        public Core()
        {
            DataUI = new DataUI();

            Config();

            // open core port
            IPEndPoint ep = SockSessServer.FindFreeEndPoint(IPAddress.Parse("0.0.0.0"), 5964);
            MakeListen(ep);
            DataUI.Port = ep.Port;
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
                    string[] str = item.Attributes["ep"].Value.Split(':');
                    IPEndPoint ep = new IPEndPoint(IPAddress.Parse(str[0]), int.Parse(str[1]));
                    SockType sockType = (SockType)Enum.Parse(typeof(SockType), item.Attributes["type"].Value);
                    SockUnit sockUnit = new SockUnit() {
                        ID = item.Attributes["id"].Value,
                        Name = item.Attributes["name"].Value,
                        Type = sockType,
                        Lep = sockType == SockType.listen ? ep : null,
                        Rep = sockType == SockType.connect ? ep : null,
                        State = SockState.Closed,
                        Autorun = bool.Parse(item.Attributes["autorun"].Value),
                    };
                    DataUI.AddSockUnit(sockUnit);

                    if (sockUnit.Autorun)
                        SockOpen(sockUnit.ID, sockType, ep);
                }
            } catch (Exception) {
                System.Windows.MessageBox.Show(CONF_NAME + ": syntax error.");
            }
        }

        // SockSess Event

        protected override void OnAcceptEvent(object sender, SockSessAccept sess)
        {
            base.OnAcceptEvent(sender, sess);

            SockUnit sockUnit = new SockUnit() {
                ID = "at" + sess.rep.ToString(),
                Name = "accept",
                Type = SockType.accept,
                Lep = sess.lep,
                Rep = sess.rep,
                State = SockState.Opened,
            };
            DataUI.AddSockUnit(sockUnit);
        }

        protected override void OnCloseEvent(object sender)
        {
            SockSessNew sess = sender as SockSessNew;
            sesstab.Remove(sess);

            if (sender is SockSessServer)
                DataUI.CloseSockUnit(SockType.listen, sess.lep, sess.rep);
            else if (sender is SockSessClient)
                DataUI.CloseSockUnit(SockType.connect, sess.lep, sess.rep);
            else/* if (sender is SockSessAccept)*/
                DataUI.DelSockUnit(SockType.accept, sess.lep, sess.rep);
        }

        // Center Service

        protected override void DefaultService(ServiceRequest request, ref ServiceResponse response)
        {
            base.DefaultService(request, ref response);

            string log = DateTime.Now + " (" + (request.user_data as SockSessNew).rep.ToString()
                + " => " + (request.user_data as SockSessNew).lep.ToString() + ")\n";
            log += SockConvert.ParseBytesToString(request.raw_data) + "\n\n";

            /// ** update DataUI
            DataUI.Logger(log);
        }

        protected override void SessOpenService(ServiceRequest request, ref ServiceResponse response)
        {
            // parse to dictionary
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>(Encoding.UTF8.GetString(request.raw_data));

            SockType sockType = (SockType)Enum.Parse(typeof(SockType), dc["type"]);
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), Convert.ToInt32(dc["port"]));

            SockOpen(dc["sockid"], sockType, ep);
        }

        private void SockOpen(string id, SockType sockType, IPEndPoint ep)
        {
            try {
                SockSessNew sess = null;
                if (sockType == SockType.listen)
                    sess = MakeListen(ep);
                else
                    sess = MakeConnect(ep);
                DataUI.OpenSockUnit(id, sess.lep, sess.rep);
            } catch (Exception) {
                DataUI.CloseSockUnit(id);
            }
        }
    }
}
