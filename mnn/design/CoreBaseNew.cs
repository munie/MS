using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using mnn.net;

namespace mnn.design {
    public class CoreBaseNew {
        protected List<SockSessNew> sess_group;
        protected DispatcherBase dispatcher;

        public CoreBaseNew()
        {
            sess_group = new List<SockSessNew>();
            dispatcher = new DispatcherBase();

            dispatcher.RegisterDefaultService("DefaultService", DefaultService);
            dispatcher.RegisterService("SockOpenService", SockOpenService, Encoding.UTF8.GetBytes("/center/sockopen"));
            dispatcher.RegisterService("SockCloseService", SockCloseService, Encoding.UTF8.GetBytes("/center/sockclose"));
            dispatcher.RegisterService("SockSendService", SockSendService, Encoding.UTF8.GetBytes("/center/socksend"));
        }

        protected SockSessServer MakeListen(IPEndPoint ep)
        {
            SockSessServer server = new SockSessServer();
            server.Listen(ep);
            server.close_event += new SockSessDelegate(CloseEvent);
            server.accept_event += new SockSessServerDelegate(AcceptEvent);

            sess_group.Add(server);
            return server;
        }

        protected SockSessClient MakeConnect(IPEndPoint ep)
        {
            SockSessClient client = new SockSessClient();
            client.Connect(ep);
            client.close_event += new SockSessDelegate(CloseEvent);
            client.recv_event += new SockSessDelegate(RecvEvent);

            sess_group.Add(client);
            return client;
        }

        protected SockSessNew FindSockSessFromSessGroup(SockType sockType, IPEndPoint ep)
        {
            IEnumerable<SockSessNew> subset = null;
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

        // SockSess Event

        protected virtual void AcceptEvent(object sender, SockSessAccept sess)
        {
            sess.close_event += new SockSessDelegate(CloseEvent);
            sess.recv_event += new SockSessDelegate(RecvEvent);
            sess_group.Add(sess);
        }

        protected virtual void CloseEvent(object sender)
        {
            SockSessNew sess = sender as SockSessNew;
            sess_group.Remove(sess);
        }

        protected virtual void RecvEvent(object sender)
        {
            SockSessNew sess = sender as SockSessNew;
            SockRequest request = new SockRequest(sess.lep, sess.rep, sess.rfifo.Take());
            SockResponse response = new SockResponse();

            dispatcher.Handle(request, ref response);
            if (response.data != null && response.data.Length != 0)
                sess.wfifo.Append(response.data);
        }

        // Center Service

        protected virtual void DefaultService(SockRequest request, ref SockResponse response)
        {
            response.data = Encoding.UTF8.GetBytes("Failure: unknown request\r\n");
        }

        protected virtual void SockOpenService(SockRequest request, ref SockResponse response)
        {
            // get param string & parse to dictionary
            string msg = Encoding.UTF8.GetString(request.data);
            if (!msg.Contains('?')) return;
            msg = msg.Substring(msg.IndexOf('?') + 1);
            IDictionary<string, string> dc = SockConvert.ParseUrlQueryParam(msg);

            SockType sockType = (SockType)Enum.Parse(typeof(SockType), dc["type"]);
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), int.Parse(dc["port"]));

            try {
                SockSessNew sess = null;
                if (sockType == SockType.listen)
                    sess = MakeListen(ep);
                else
                    sess = MakeConnect(ep);
                response.data = Encoding.UTF8.GetBytes("Success: " + dc["type"] + " " + ep.ToString() + "\r\n");
            } catch (Exception) {
                response.data = Encoding.UTF8.GetBytes("Failure: can't open " + ep.ToString() + "\r\n");
            }
        }

        protected virtual void SockCloseService(SockRequest request, ref SockResponse response)
        {
            // get param string & parse to dictionary
            string msg = Encoding.UTF8.GetString(request.data);
            if (!msg.Contains('?')) return;
            msg = msg.Substring(msg.IndexOf('?') + 1);
            IDictionary<string, string> dc = SockConvert.ParseUrlQueryParam(msg);

            SockType sockType = (SockType)Enum.Parse(typeof(SockType), dc["type"]);
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), int.Parse(dc["port"]));
            SockSessNew sess = FindSockSessFromSessGroup(sockType, ep);

            if (sess != null) {
                sess.Close();
                response.data = Encoding.UTF8.GetBytes("Success: shutdown " + ep.ToString() + "\r\n");
            } else
                response.data = Encoding.UTF8.GetBytes("Failure: can't find " + ep.ToString() + "\r\n");
        }

        protected virtual void SockSendService(SockRequest request, ref SockResponse response)
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
            SockSessNew sess = FindSockSessFromSessGroup(sockType, ep);

            if (sess != null) {
                sess.wfifo.Append(Encoding.UTF8.GetBytes(param_data));
                response.data = Encoding.UTF8.GetBytes("Success: send to " + ep.ToString() + "\r\n");
            } else
                response.data = Encoding.UTF8.GetBytes("Failure: can't find " + ep.ToString() + "\r\n");
        }
    }
}
