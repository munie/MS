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
        // hook for ui to display socket infomation
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
                    SockUnit sockUnit = new SockUnit();
                    sockUnit.ID = item.Attributes["id"].Value;
                    sockUnit.Name = item.Attributes["name"].Value;
                    sockUnit.Type = (SockType)Enum.Parse(typeof(SockType), item.Attributes["type"].Value);
                    string[] str = item.Attributes["ep"].Value.Split(':');
                    IPEndPoint ep = new IPEndPoint(IPAddress.Parse(str[0]), int.Parse(str[1]));
                    sockUnit.Lep = sockUnit.Type == SockType.listen ? ep : null;
                    sockUnit.Rep = sockUnit.Type == SockType.connect ? ep : null;
                    sockUnit.Autorun = bool.Parse(item.Attributes["autorun"].Value);
                    sockUnit.UpdateTitle();
                    DataUI.AddSockUnit(sockUnit);
                    //DataUI.SockUnitGroup.Add(sock);

                    if (sockUnit.Autorun) {
                        try {
                            if (sockUnit.Type == SockType.listen)
                                MakeListen(sockUnit.Lep);
                            else
                                MakeConnect(sockUnit.Rep);
                            sockUnit.State = SockState.Opened;
                        } catch (Exception) {
                            sockUnit.State = SockState.Closed;
                        }
                    }
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
            DataUI.SockAdd(SockType.accept, sess.lep, sess.rep);

            sess_group.Add(sess);
        }

        private void AcceptCloseEvent(object sender)
        {
            SockSessAccept sess = sender as SockSessAccept;
            SockUnit unit = DataUI.FindSockUnit(SockType.accept, sess.lep, sess.rep);
            DataUI.DelSockUnit(unit);
            //DataUI.SockDel(SockType.accept, sess.lep, sess.rep);

            sess_group.Remove(sess);
        }

        private void CloseEvent(object sender)
        {
            SockSessBase sess = sender as SockSessBase;
            SockUnit unit = null;
            if (sess.rep == null)
                unit = DataUI.FindSockUnit(SockType.listen, sess.lep, sess.rep);
            else
                unit = DataUI.FindSockUnit(SockType.connect, sess.lep, sess.rep);
            unit.State = SockState.Closed;
            sess_group.Remove(sess);
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

            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), int.Parse(dc["port"]));
            SockSessBase sess = null;
            try {
                if (dc["type"] == SockType.listen.ToString())
                    sess = MakeListen(ep);
                else if (dc["type"] == SockType.connect.ToString())
                    sess = MakeConnect(ep);
                else
                    return;
                DataUI.SockOpen(dc["id"], sess.lep, sess.rep);
            } catch (Exception) {
                DataUI.SockClose(dc["id"]);
            }
        }

        protected void SockCloseService(SockRequest request, ref SockResponse response)
        {
            // get param string & parse to dictionary
            string msg = Encoding.UTF8.GetString(request.data);
            if (!msg.Contains('?')) return;
            msg = msg.Substring(msg.IndexOf('?') + 1);
            IDictionary<string, string> dc = SockConvert.ParseUrlQueryParam(msg);

            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), int.Parse(dc["port"]));
            SockSessBase sess = FindSockSessFromSessGroup(dc["type"], ep);

            if (sess != null) {
                sess.Close();
                DataUI.SockClose(dc["id"]);
            }
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

            // find session
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), int.Parse(dc["port"]));
            SockSessBase sess = FindSockSessFromSessGroup(dc["type"], ep);
            if (sess != null) {
                sess.wfifo.Append(Encoding.UTF8.GetBytes(param_data));
                response.data = Encoding.UTF8.GetBytes("Success: sendto " + ep.ToString() + "\r\n");
            } else
                response.data = Encoding.UTF8.GetBytes("Failure: can't find " + ep.ToString() + "\r\n");
        }

        private SockSessBase FindSockSessFromSessGroup(string type, IPEndPoint ep)
        {
            IEnumerable<SockSessBase> subset = null;
            if (type == SockType.listen.ToString())
                subset = from s in sess_group where s.lep.Equals(ep) select s;
            else if (type == SockType.connect.ToString())
                subset = from s in sess_group where s.lep.Equals(ep) select s;
            else if (type == SockType.accept.ToString())
                subset = from s in sess_group where s.rep != null && s.rep.Equals(ep) select s;
            else
                return null;

            if (subset != null && subset.Count() != 0)
                return subset.First();
            else
                return null;
        }
    }
}
