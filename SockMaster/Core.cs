using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Net;
using mnn.design;
using mnn.net;

namespace SockMaster {
    class Core {
        public static readonly string BASE_DIR = System.AppDomain.CurrentDomain.BaseDirectory;
        public static readonly string CONF_NAME = "SockMaster.xml";
        public static readonly string CONF_PATH = BASE_DIR + CONF_NAME;

        private List<SockSessBase> sess_group;
        private DispatcherBase dispatcher;
        // hook for ui to display socket information
        public DataUI DataUI { get; set; }

        public Core()
        {
            sess_group = new List<SockSessBase>();
            dispatcher = new DispatcherBase();
            DataUI = new DataUI();

            // open core port
            do {
                DataUI.Port = 5964 + new Random().Next() % 8192;
            }
            while (!SockSessServer.VerifyEndPointsValid(new IPEndPoint(IPAddress.Parse("0.0.0.0"), DataUI.Port)));
            MakeListen(new IPEndPoint(IPAddress.Parse("0.0.0.0"), DataUI.Port));

            // dispatcher register
            dispatcher.RegisterDefaultService("DefaultService", DefaultService);
            dispatcher.RegisterService("SockOpenService", SockOpenService, Encoding.UTF8.GetBytes("/center/sockopen"));
            dispatcher.RegisterService("SockCloseService", SockCloseService, Encoding.UTF8.GetBytes("/center/sockclose"));
            dispatcher.RegisterService("SockSendService", SockSendService, Encoding.UTF8.GetBytes("/center/socksend"));
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
                        SockOpen(sockType, ep, sockUnit);
                }
            } catch (Exception) {
                System.Windows.MessageBox.Show(CONF_NAME + ": syntax error.");
            }
        }

        private SockSessServer MakeListen(IPEndPoint ep)
        {
            SockSessServer server = new SockSessServer();
            server.Listen(ep);
            server.close_event += new SockSessDelegate(CloseEvent);
            server.accept_event += new SockSessServer.SockSessServerDelegate(AcceptEvent);

            sess_group.Add(server);
            return server;
        }

        private SockSessClient MakeConnect(IPEndPoint ep)
        {
            SockSessClient client = new SockSessClient();
            client.Connect(ep);
            client.close_event += new SockSessDelegate(CloseEvent);
            client.recv_event += new SockSessDelegate(RecvEvent);

            sess_group.Add(client);
            return client;
        }

        // Session Event

        private void AcceptEvent(object sender, SockSessAccept sess)
        {
            sess.close_event += new SockSessDelegate(AcceptCloseEvent);
            sess.recv_event += new SockSessDelegate(RecvEvent);
            sess_group.Add(sess);

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

        private void AcceptCloseEvent(object sender)
        {
            SockSessAccept sess = sender as SockSessAccept;
            sess_group.Remove(sess);

            SockUnit unit = DataUI.FindSockUnit(SockType.accept, sess.lep, sess.rep);
            DataUI.DelSockUnit(unit);
        }

        private void CloseEvent(object sender)
        {
            SockSessBase sess = sender as SockSessBase;
            sess_group.Remove(sess);

            SockUnit unit = null;
            if (sess.rep == null)
                unit = DataUI.FindSockUnit(SockType.listen, sess.lep, sess.rep);
            else
                unit = DataUI.FindSockUnit(SockType.connect, sess.lep, sess.rep);
            DataUI.CloseSockUnit(unit);
        }

        private void RecvEvent(object sender)
        {
            SockSessBase sess = sender as SockSessBase;
            SockRequest request = new SockRequest(sess.lep, sess.rep, sess.rfifo.Take());
            SockResponse response = new SockResponse();

            dispatcher.Handle(request, ref response);
            if (response.data != null && response.data.Length != 0)
                sess.wfifo.Append(response.data);
        }

        // Center Service

        protected void DefaultService(SockRequest request, ref SockResponse response)
        {
            string log = DateTime.Now + " (" + request.rep.ToString() + " => " + request.lep.ToString() + ")\n";
            log += SockConvert.ParseBytesToString(request.data) + "\n\n";

            /// ** update DataUI
            DataUI.Logger(log);
        }

        protected void SockOpenService(SockRequest request, ref SockResponse response)
        {
            // get param string & parse to dictionary
            string msg = Encoding.UTF8.GetString(request.data);
            if (!msg.Contains('?')) return;
            msg = msg.Substring(msg.IndexOf('?') + 1);
            IDictionary<string, string> dc = SockConvert.ParseUrlQueryParam(msg);

            SockType sockType = (SockType)Enum.Parse(typeof(SockType), dc["type"]);
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), int.Parse(dc["port"]));
            SockUnit unit = DataUI.FindSockUnit(dc["id"]);
            if (unit == null)
                return;

            SockOpen(sockType, ep, unit);
        }

        protected void SockCloseService(SockRequest request, ref SockResponse response)
        {
            // get param string & parse to dictionary
            string msg = Encoding.UTF8.GetString(request.data);
            if (!msg.Contains('?')) return;
            msg = msg.Substring(msg.IndexOf('?') + 1);
            IDictionary<string, string> dc = SockConvert.ParseUrlQueryParam(msg);

            SockType sockType = (SockType)Enum.Parse(typeof(SockType), dc["type"]);
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), int.Parse(dc["port"]));
            SockSessBase sess = FindSockSessFromSessGroup(sockType, ep);

            if (sess != null)
                sess.Close();
        }

        protected void SockSendService(SockRequest request, ref SockResponse response)
        {
            // retrieve param_list of url
            string url = Encoding.UTF8.GetString(request.data);
            if (!url.Contains('?')) return;
            string param_list = url.Substring(url.IndexOf('?') + 1);

            // retrieve param_data
            int index_data = param_list.IndexOf("&data=");
            if (index_data == -1) return;
            string param_data = param_list.Substring(index_data + 6);
            param_list = param_list.Substring(0, index_data);

            // retrieve param to dictionary
            IDictionary<string, string> dc = SockConvert.ParseUrlQueryParam(param_list);

            SockType sockType = (SockType)Enum.Parse(typeof(SockType), dc["type"]);
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), int.Parse(dc["port"]));
            SockSessBase sess = FindSockSessFromSessGroup(sockType, ep);

            if (sess != null) {
                sess.wfifo.Append(Encoding.UTF8.GetBytes(param_data));
                response.data = Encoding.UTF8.GetBytes("Success: sendto " + ep.ToString() + "\r\n");
            } else
                response.data = Encoding.UTF8.GetBytes("Failure: can't find " + ep.ToString() + "\r\n");
        }

        private void SockOpen(SockType sockType, IPEndPoint ep, SockUnit unit)
        {
            try {
                SockSessBase sess = null;
                if (sockType == SockType.listen)
                    sess = MakeListen(ep);
                else
                    sess = MakeConnect(ep);
                DataUI.OpenSockUnit(unit, sess.lep, sess.rep);
            } catch (Exception) {
                DataUI.CloseSockUnit(unit);
            }
        }

        private SockSessBase FindSockSessFromSessGroup(SockType sockType, IPEndPoint ep)
        {
            IEnumerable<SockSessBase> subset = null;
            switch (sockType) {
                case SockType.listen:
                case SockType.connect:
                    subset = from s in sess_group where s.lep.Equals(ep) select s;
                    break;
                case SockType.accept:
                    subset = from s in sess_group where s.rep != null && s.rep.Equals(ep) select s;
                    break;
                default:
                    break;
            }

            if (subset != null && subset.Count() != 0)
                return subset.First();
            else
                return null;
        }
    }
}
