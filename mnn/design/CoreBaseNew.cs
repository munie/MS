using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using mnn.net;
using mnn.misc.service;

namespace mnn.design {
    public class CoreBaseNew {
        protected List<SockSessNew> sesstab;
        protected ServiceCore servctl;

        public CoreBaseNew()
        {
            sesstab = new List<SockSessNew>();

            servctl = new ServiceCore();
            servctl.RegisterDefaultService("core.default", DefaultService);
            servctl.RegisterService("center.sessopen", SessOpenService);
            servctl.RegisterService("center.sessclose", SessCloseService);
            servctl.RegisterService("center.sesssend", SessSendService);
        }

        protected SockSessServer MakeListen(IPEndPoint ep)
        {
            SockSessServer server = new SockSessServer();
            server.Listen(ep);
            server.close_event += new SockSessDelegate(OnCloseEvent);
            server.accept_event += new SockSessServerDelegate(OnAcceptEvent);

            sesstab.Add(server);
            return server;
        }

        protected SockSessClient MakeConnect(IPEndPoint ep)
        {
            SockSessClient client = new SockSessClient();
            client.Connect(ep);
            client.close_event += new SockSessDelegate(OnCloseEvent);
            client.recv_event += new SockSessDelegate(OnRecvEvent);

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

        // SockSess Event

        protected virtual void OnAcceptEvent(object sender, SockSessAccept sess)
        {
            sess.close_event += new SockSessDelegate(OnCloseEvent);
            sess.recv_event += new SockSessDelegate(OnRecvEvent);
            sesstab.Add(sess);
        }

        protected virtual void OnCloseEvent(object sender)
        {
            SockSessNew sess = sender as SockSessNew;
            sesstab.Remove(sess);
        }

        protected virtual void OnRecvEvent(object sender)
        {
            SockSessNew sess = sender as SockSessNew;
            ServiceRequest request = ServiceRequest.Parse(sess.rfifo.Take());
            request.user_data = sess;
            ServiceResponse response = new ServiceResponse();

            servctl.DoService(request, ref response);
            if (response.raw_data != null && response.raw_data.Length != 0)
                sess.wfifo.Append(response.raw_data);
        }

        // Center Service

        protected virtual void DefaultService(ServiceRequest request, ref ServiceResponse response)
        {
            response.raw_data = Encoding.UTF8.GetBytes("Failure: unknown request\r\n");
        }

        protected virtual void SessOpenService(ServiceRequest request, ref ServiceResponse response)
        {
            // get param string & parse to dictionary
            string msg = Encoding.UTF8.GetString(request.raw_data);
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
                response.raw_data = Encoding.UTF8.GetBytes("Success: " + dc["type"] + " " + ep.ToString() + "\r\n");
            } catch (Exception) {
                response.raw_data = Encoding.UTF8.GetBytes("Failure: can't open " + ep.ToString() + "\r\n");
            }
        }

        protected virtual void SessCloseService(ServiceRequest request, ref ServiceResponse response)
        {
            // get param string & parse to dictionary
            string msg = Encoding.UTF8.GetString(request.raw_data);
            if (!msg.Contains('?')) return;
            msg = msg.Substring(msg.IndexOf('?') + 1);
            IDictionary<string, string> dc = SockConvert.ParseUrlQueryParam(msg);

            SockType sockType = (SockType)Enum.Parse(typeof(SockType), dc["type"]);
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), int.Parse(dc["port"]));
            SockSessNew sess = FindSockSessFromSessGroup(sockType, ep);

            if (sess != null) {
                sess.Close();
                response.raw_data = Encoding.UTF8.GetBytes("Success: shutdown " + ep.ToString() + "\r\n");
            } else
                response.raw_data = Encoding.UTF8.GetBytes("Failure: can't find " + ep.ToString() + "\r\n");
        }

        protected virtual void SessSendService(ServiceRequest request, ref ServiceResponse response)
        {
            // retrieve param_list of url
            string url = Encoding.UTF8.GetString(request.raw_data);
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
                response.raw_data = Encoding.UTF8.GetBytes("Success: send to " + ep.ToString() + "\r\n");
            } else
                response.raw_data = Encoding.UTF8.GetBytes("Failure: can't find " + ep.ToString() + "\r\n");
        }
    }
}
