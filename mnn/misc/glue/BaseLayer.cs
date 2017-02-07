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
            RegisterService("service.sesslisten", SessListenService, OnServiceDone);
            RegisterService("service.sessconnect", SessConnectService, OnServiceDone);
            RegisterService("service.sessclose", SessCloseService, OnServiceDone);
            RegisterService("service.sesssend", SessSendService, OnServiceDone);
            RegisterService("service.sessdetail", SessDetailService, OnServiceDone);
            RegisterService("service.sessgroupstate", SessGroupStateService, OnServiceDone);

            sesstab = new List<SockSess>();
            sessstate = new SockSessGroupState();
            sess_listen_event = null;
            sess_accept_event = null;
            sess_connect_event = null;
            sess_close_event = null;
        }

        protected override void OnServiceDone(ServiceRequest request, ServiceResponse response)
        {
            sessstate.PackDecrease();

            Dictionary<string, string> sd = request.sessdata;
            if (sd != null && response != null) {
                SockSess sess = FindSockSessFromSessGroup(sd["sessid"]);
                if (sess != null) {
                    sess.sdata = request.sessdata;
                    sess.wfifo.Append(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response)));
                }
            }
        }

        // SockSess Event ======================================================================

        protected virtual void OnAcceptEvent(SockSessServer server)
        {
            SockSess accept = server.Accept();
            mnn.util.Loop.default_loop.Add(accept);

            Dictionary<string, string> sd = new Dictionary<string, string>();
            sd.Add("sessid", accept.id);
            sd.Add("lep", accept.lep.ToString());
            sd.Add("rep", accept.rep.ToString());
            accept.sdata = sd;
            accept.close_event += new SockSess.SockSessDelegate(OnCloseEvent);
            accept.recv_event += new SockSess.SockSessDelegate(OnRecvEvent);
            sesstab.Add(accept);

            if (sess_accept_event != null)
                sess_accept_event(this, accept);

            sessstate.AcceptIncrease();
        }

        protected virtual void OnCloseEvent(object sender)
        {
            SockSess sess = sender as SockSess;
            sesstab.Remove(sess);

            if (sess_close_event != null)
                sess_close_event(this, sess);

            if (sess is SockSessServer)
                sessstate.ListenCount--;
            else if (sess is SockSessClient)
                sessstate.ConnectCount--;
            else
                sessstate.AcceptDecrease();
        }

        protected virtual void OnRecvEvent(SockSess sess)
        {
            while (sess.rfifo.Size() != 0) {
                ServiceRequest request = ServiceRequest.Parse(sess.rfifo.Peek());
                if (request.packlen == 0)
                    break;

                sess.rfifo.Skip(request.packlen);
                request.sessdata = sess.sdata as Dictionary<string, string>;
                AddServiceRequest(request);
                sessstate.PackIncrease();
            }
        }

        // SockSess Service ====================================================================

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
                if (!(sess is SockSessServer))
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

        // SockSess Interface ==================================================================

        public SockSessServer MakeListen(IPEndPoint ep)
        {
            SockSessServer server = new SockSessServer();
            server.Bind(ep);
            server.Listen(100, OnAcceptEvent);
            server.close_event += new SockSess.SockSessDelegate(OnCloseEvent);

            if (sess_listen_event != null)
                sess_listen_event(this, server);

            mnn.util.Loop.default_loop.Add(server);
            sesstab.Add(server);
            sessstate.ListenCount++;
            return server;
        }

        public SockSessClient MakeConnect(IPEndPoint ep)
        {
            SockSessClient client = new SockSessClient();
            client.Connect(ep);
            client.close_event += new SockSess.SockSessDelegate(OnCloseEvent);
            client.recv_event += new SockSess.SockSessDelegate(OnRecvEvent);

            if (sess_connect_event != null)
                sess_connect_event(this, client);

            mnn.util.Loop.default_loop.Add(client);
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
