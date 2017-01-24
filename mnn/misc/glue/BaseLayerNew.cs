using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using mnn.net;
using mnn.service;
using Newtonsoft.Json;

namespace mnn.misc.glue {
    public class BaseLayerNew : ServiceLayer {
        protected List<SockSessNew> sesstab;
        public delegate void SockSessOpenDelegate(object sender, SockSessNew sess);
        public delegate void SockSessCloseDelegate(object sender, SockSessNew sess);
        public SockSessOpenDelegate sess_open_event;
        public SockSessCloseDelegate sess_close_event;

        public BaseLayerNew()
        {
            servctl.RegisterService("service.sessopen", SessOpenService);
            servctl.RegisterService("service.sessclose", SessCloseService);
            servctl.RegisterService("service.sesssend", SessSendService);

            sesstab = new List<SockSessNew>();
            sess_open_event = null;
            sess_close_event = null;
        }

        protected override void OnServiceDone(ServiceRequest request, ServiceResponse response)
        {
            SockSessNew sess = request.user_data as SockSessNew;
            if (sess != null && response != null)
                sess.wfifo.Append(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response)));
        }

        // SockSess Event

        protected virtual void OnAcceptEvent(object sender, SockSessAccept sess)
        {
            sess.close_event += new SockSessNew.SockSessDelegate(OnCloseEvent);
            sess.recv_event += new SockSessNew.SockSessDelegate(OnRecvEvent);
            sesstab.Add(sess);

            if (sess_open_event != null)
                sess_open_event(this, sess);
        }

        protected virtual void OnCloseEvent(object sender)
        {
            SockSessNew sess = sender as SockSessNew;
            sesstab.Remove(sess);

            if (sess_close_event != null)
                sess_close_event(this, sess);
        }

        protected virtual void OnRecvEvent(object sender)
        {
            SockSessNew sess = sender as SockSessNew;

            while (sess.rfifo.Size() != 0) {
                ServiceRequest request = ServiceRequest.Parse(sess.rfifo.Peek());
                if (request.packlen == 0)
                    break;

                sess.rfifo.Skip(request.packlen);
                request.user_data = sess;
                servctl.AddRequest(request);
            }
        }

        // Center Service

        protected virtual void SessOpenService(ServiceRequest request, ref ServiceResponse response)
        {
            // parse to dictionary
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>((string)request.data);

            SockType sockType = (SockType)Enum.Parse(typeof(SockType), dc["type"]);
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), Convert.ToInt32(dc["port"]));

            try {
                SockSessNew sess = null;
                if (sockType == SockType.listen)
                    sess = MakeListen(ep);
                else
                    sess = MakeConnect(ep);

                if (sess_open_event != null)
                    sess_open_event(this, sess);

                response.errcode = 0;
                response.errmsg = dc["type"] + " " + ep.ToString();
            } catch (Exception) {
                response.errcode = 1;
                response.errmsg = "can't open " + ep.ToString();
            }
        }

        protected virtual void SessCloseService(ServiceRequest request, ref ServiceResponse response)
        {
            // parse to dictionary
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>((string)request.data);

            SockType sockType = (SockType)Enum.Parse(typeof(SockType), dc["type"]);
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), Convert.ToInt32(dc["port"]));
            SockSessNew sess = FindSockSessFromSessGroup(sockType, ep);

            if (sess != null) {
                sess.Close();

                response.errcode = 0;
                response.errmsg = "shutdown " + ep.ToString();
            } else {
                response.errcode = 1;
                response.errmsg = "can't find " + ep.ToString();
            }
        }

        protected virtual void SessSendService(ServiceRequest request, ref ServiceResponse response)
        {
            // parse to dictionary
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>((string)request.data);

            SockType sockType = (SockType)Enum.Parse(typeof(SockType), dc["type"]);
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), Convert.ToInt32(dc["port"]));
            SockSessNew sess = FindSockSessFromSessGroup(sockType, ep);

            if (sess != null) {
                if (sess is SockSessServer)
                    (sess as SockSessServer).Broadcast(Convert.FromBase64String(dc["data"]));
                else
                    sess.wfifo.Append(Convert.FromBase64String(dc["data"]));
                response.errcode = 0;
                response.errmsg = "send to " + ep.ToString();
            } else {
                response.errcode = 0;
                response.errmsg = "can't find " + ep.ToString();
            }
        }

        // Methods

        public SockSessServer MakeListen(IPEndPoint ep)
        {
            SockSessServer server = new SockSessServer();
            server.Listen(ep);
            server.close_event += new SockSessNew.SockSessDelegate(OnCloseEvent);
            server.accept_event += new SockSessServer.SockSessServerDelegate(OnAcceptEvent);

            sesstab.Add(server);
            return server;
        }

        protected SockSessClient MakeConnect(IPEndPoint ep)
        {
            SockSessClient client = new SockSessClient();
            client.Connect(ep);
            client.close_event += new SockSessNew.SockSessDelegate(OnCloseEvent);
            client.recv_event += new SockSessNew.SockSessDelegate(OnRecvEvent);

            sesstab.Add(client);
            return client;
        }

        protected SockSessNew FindSockSessFromSessGroup(SockType sockType, IPEndPoint ep)
        {
            IEnumerable<SockSessNew> subset = null;
            switch (sockType) {
                case SockType.listen:
                case SockType.connect:
                    subset = from s in sesstab where s.lep.Equals(ep) select s;
                    break;
                case SockType.accept:
                    subset = from s in sesstab where s.rep != null && s.rep.Equals(ep) select s;
                    break;
                default:
                    break;
            }

            if (subset != null && subset.Count() != 0)
                return subset.First();
            else
                return null;
        }

        protected SockSessNew FindSockSessFromSessGroup(IPEndPoint lep, IPEndPoint rep)
        {
            IEnumerable<SockSessNew> subset = from s in sesstab
                                              where s.lep.Equals(lep)
                                              && s.rep != null && s.rep.Equals(rep)
                                              select s;

            if (subset != null && subset.Count() != 0)
                return subset.First();
            else
                return null;
        }
    }
}
