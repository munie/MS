using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using mnn.net;
using mnn.service;
using Newtonsoft.Json;

namespace mnn.misc.glue {
    public class BaseLayer : ModulizedServiceLayer {
        protected List<SockSess> sesstab;
        private SockSessGroupState sessstate;
        public delegate void SockSessOpenDelegate(object sender, SockSess sess);
        public delegate void SockSessCloseDelegate(object sender, SockSess sess);
        public SockSessOpenDelegate sess_listen_event;
        public SockSessOpenDelegate sess_accept_event;
        public SockSessOpenDelegate sess_connect_event;
        public SockSessCloseDelegate sess_close_event;

        public BaseLayer()
        {
            servctl.RegisterService("service.sesslisten", SessListenService);
            servctl.RegisterService("service.sessconnect", SessConnectService);
            servctl.RegisterService("service.sessclose", SessCloseService);
            servctl.RegisterService("service.sesssend", SessSendService);
            servctl.RegisterService("service.sessdetail", SessDetailService);
            servctl.RegisterService("service.sessgroupstate", SessGroupStateService);

            sesstab = new List<SockSess>();
            sessstate = new SockSessGroupState();
            sess_listen_event = null;
            sess_accept_event = null;
            sess_connect_event = null;
            sess_close_event = null;
        }

        protected override void Exec()
        {
            foreach (var item in sesstab.ToList())
                item.DoSocket(0);

            base.Exec();
        }

        protected override void OnServiceDone(ServiceRequest request, ServiceResponse response)
        {
            sessstate.PackDecrease();

            SockSess sess = request.user_data as SockSess;
            if (sess != null && response != null)
                sess.wfifo.Append(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response)));
        }

        // SockSess Event

        protected virtual void OnAcceptEvent(object sender, SockSessAccept sess)
        {
            sess.close_event += new SockSess.SockSessDelegate(OnCloseEvent);
            sess.recv_event += new SockSess.SockSessDelegate(OnRecvEvent);
            sesstab.Add(sess);

            if (sess_accept_event != null)
                sess_accept_event(this, sess);

            sessstate.AcceptIncrease();
        }

        protected virtual void OnCloseEvent(object sender)
        {
            SockSess sess = sender as SockSess;
            sesstab.Remove(sess);

            if (sess_close_event != null)
                sess_close_event(this, sess);

            if (sess is SockSessAccept)
                sessstate.AcceptDecrease();
            else if (sess is SockSessServer)
                sessstate.ListenCount--;
            else if (sess is SockSessClient)
                sessstate.ConnectCount--;
        }

        protected virtual void OnRecvEvent(object sender)
        {
            SockSess sess = sender as SockSess;

            while (sess.rfifo.Size() != 0) {
                ServiceRequest request = ServiceRequest.Parse(sess.rfifo.Peek());
                if (request.packlen == 0)
                    break;

                sess.rfifo.Skip(request.packlen);
                request.user_data = sess;
                servctl.AddRequest(request);
                sessstate.PackIncrease();
            }
        }

        // Center Service

        protected virtual void SessListenService(ServiceRequest request, ref ServiceResponse response)
        {
            // parse to dictionary
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>((string)request.data);

            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), Convert.ToInt32(dc["port"]));

            try {
                SockSess sess = MakeListen(ep);
                response.errcode = 0;
                response.errmsg = "listen at " + ep.ToString();
            } catch (Exception) {
                response.errcode = 1;
                response.errmsg = "can't listen at " + ep.ToString();
            }
        }

        protected virtual void SessConnectService(ServiceRequest request, ref ServiceResponse response)
        {
            // parse to dictionary
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>((string)request.data);

            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), Convert.ToInt32(dc["port"]));

            try {
                SockSess sess = MakeConnect(ep);
                response.errcode = 0;
                response.errmsg = "connect to " + ep.ToString();
            } catch (Exception) {
                response.errcode = 1;
                response.errmsg = "can't connect to " + ep.ToString();
            }
        }

        protected virtual void SessCloseService(ServiceRequest request, ref ServiceResponse response)
        {
            // parse to dictionary
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>((string)request.data);

            SockSess sess = FindSockSessFromSessGroup(dc["sessid"]);

            if (sess != null) {
                sess.Close();
                response.errcode = 0;
                response.errmsg = "shutdown " + dc["sessid"];
            } else {
                response.errcode = 1;
                response.errmsg = "can't find " + dc["sessid"];
            }
        }

        protected virtual void SessSendService(ServiceRequest request, ref ServiceResponse response)
        {
            // parse to dictionary
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>((string)request.data);

            SockSess sess = FindSockSessFromSessGroup(dc["sessid"]);

            if (sess != null) {
                if (sess is SockSessServer)
                    (sess as SockSessServer).Broadcast(Convert.FromBase64String(dc["data"]));
                else
                    sess.wfifo.Append(Convert.FromBase64String(dc["data"]));
                response.errcode = 0;
                response.errmsg = "send to " + dc["sessid"];
            } else {
                response.errcode = 0;
                response.errmsg = "can't find " + dc["sessid"];
            }
        }

        protected virtual void SessDetailService(ServiceRequest request, ref ServiceResponse response)
        {
            List<object> pack = new List<object>();
            foreach (var item in sesstab) {
                pack.Add(new {
                    sessid = item.id,
                    type = item.GetType().Name,
                    localip = item.lep.ToString(),
                    remoteip = item.rep == null ? "" : item.rep.ToString(),
                    tick = item.tick,
                    conntime = item.conntime,
                });
            }

            response.data = pack;
        }

        protected virtual void SessGroupStateService(ServiceRequest request, ref ServiceResponse response)
        {
            response.data = sessstate;
        }

        // Methods

        public SockSessServer MakeListen(IPEndPoint ep)
        {
            SockSessServer server = new SockSessServer();
            server.Listen(ep);
            server.close_event += new SockSess.SockSessDelegate(OnCloseEvent);
            server.accept_event += new SockSessServer.SockSessServerDelegate(OnAcceptEvent);

            if (sess_listen_event != null)
                sess_listen_event(this, server);

            sesstab.Add(server);
            sessstate.ListenCount++;
            return server;
        }

        protected SockSessClient MakeConnect(IPEndPoint ep)
        {
            SockSessClient client = new SockSessClient();
            client.Connect(ep);
            client.close_event += new SockSess.SockSessDelegate(OnCloseEvent);
            client.recv_event += new SockSess.SockSessDelegate(OnRecvEvent);

            if (sess_connect_event != null)
                sess_connect_event(this, client);

            sesstab.Add(client);
            sessstate.ConnectCount++;
            return client;
        }

        protected SockSess FindSockSessFromSessGroup(string sessid)
        {
            foreach (var item in sesstab) {
                if (item.id.Equals(sessid))
                    return item;
            }

            return null;
        }
    }
}
