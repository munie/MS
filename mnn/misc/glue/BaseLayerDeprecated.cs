using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using mnn.net.deprecated;
using mnn.service;
using mnn.module;
using Newtonsoft.Json;

namespace mnn.misc.glue {
    public class BaseLayerDeprecated : ModulizedServiceLayer {
        public SessCtl sessctl;

        public BaseLayerDeprecated()
        {
            sessctl = new SessCtl();
            sessctl.sess_parse += new SessCtl.SessDelegate(OnSessParse);
            sessctl.sess_create += new SessCtl.SessDelegate(OnSessCreate);
            sessctl.sess_delete += new SessCtl.SessDelegate(OnSessDelete);

            servctl.RegisterService("service.sessopen", SessOpenService, OnServiceDone);
            servctl.RegisterService("service.sessclose", SessCloseService, OnServiceDone);
            servctl.RegisterService("service.sesssend", SessSendService, OnServiceDone);
            servctl.RegisterService("service.sessdetail", SessDetailService, OnServiceDone);
        }

        protected override void Exec()
        {
            sessctl.Exec(100);
            filtctl.Exec();
            servctl.Exec();
        }

        protected override void OnServiceDone(ServiceRequest request, ServiceResponse response)
        {
            if (request.sessdata == null)
                return;

            sessctl.BeginInvoke(new Action(() =>
            {
                SockSess sess = sessctl.FindSession(request.sessdata["sessid"]);
                if (sess != null) {
                    sess.sdata = request.sessdata;
                    sessctl.SendSession(sess, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response)));
                }
            }));
        }

        // Session Event ==================================================================================

        protected virtual void OnSessParse(object sender, SockSess sess)
        {
            // init request
            ServiceRequest request = ServiceRequest.Parse(sess.RfifoTake());
            request.sessdata = sess.sdata as Dictionary<string, string>;
            request.sessdata["sessid"] = sess.id;
            request.sessdata["lep"] = sess.lep.ToString();
            request.sessdata["rep"] = sess.rep.ToString();

            // rfifo skip
            sess.RfifoSkip(request.packlen);

            // add request to service core
            filtctl.AddRequest(request);
        }

        protected virtual void OnSessCreate(object sender, SockSess sess)
        {
            if (sess.type == SockType.accept) {
                Dictionary<string, string> sd = new Dictionary<string, string>();
                sd.Add("sessid", sess.id);
                sd.Add("lep", sess.lep.ToString());
                sd.Add("rep", sess.rep.ToString());
                sess.sdata = sd;
            }
        }

        protected virtual void OnSessDelete(object sender, SockSess sess) { }

        // Service =========================================================================

        protected virtual void SessOpenService(ServiceRequest request, ref ServiceResponse response)
        {
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>((string)request.data);

            // find session and open
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), Convert.ToInt32(dc["port"]));
            SockSess sess = null;
            if (dc["type"] == SockType.listen.ToString() && sessctl.FindSession(SockType.listen, ep, null) == null)
                sess = sessctl.MakeListen(ep);
            else if (dc["type"] == SockType.connect.ToString())
                sess = sessctl.AddConnect(ep);
            else
                sess = null;

            if (sess != null) {
                response.errcode = 0;
                response.errmsg = dc["type"] + " " + ep.ToString();
            } else {
                response.errcode = 1;
                response.errmsg = "cannot find " + ep.ToString();
            }
        }

        protected virtual void SessCloseService(ServiceRequest request, ref ServiceResponse response)
        {
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>((string)request.data);

            // find session
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), Convert.ToInt32(dc["port"]));
            SockSess sess = null;
            if (dc["type"] == SockType.listen.ToString())
                sess = sessctl.FindSession(SockType.listen, ep, null);
            else if (dc["type"] == SockType.connect.ToString())
                sess = sessctl.FindSession(SockType.connect, ep, null);
            else// if (dc["type"] == SockType.accept.ToString())
                sess = sessctl.FindSession(SockType.accept, null, ep);

            // close session
            if (sess != null)
                sessctl.DelSession(sess);

            if (sess != null) {
                response.errcode = 0;
                response.errmsg = "shutdown " + ep.ToString();
            } else {
                response.errcode = 1;
                response.errmsg = "cannot find " + ep.ToString();
            }
        }

        protected virtual void SessSendService(ServiceRequest request, ref ServiceResponse response)
        {
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>((string)request.data);

            // find session
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), Convert.ToInt32(dc["port"]));
            SockSess sess = null;
            if (dc["type"] == SockType.listen.ToString())
                sess = sessctl.FindSession(SockType.listen, ep, null);
            else if (dc["type"] == SockType.connect.ToString())
                sess = sessctl.FindSession(SockType.connect, ep, null);
            else// if (dc["type"] == SockType.accept.ToString())
                sess = sessctl.FindSession(SockType.accept, null, ep);

            // send message
            if (sess != null)
                sessctl.SendSession(sess, Encoding.UTF8.GetBytes(dc["data"]));

            if (sess != null) {
                response.errcode = 0;
                response.errmsg = "send to " + ep.ToString();
            } else {
                response.errcode = 1;
                response.errmsg = "cannot find " + ep.ToString();
            }
        }

        protected virtual void SessDetailService(ServiceRequest request, ref ServiceResponse response)
        {
            List<object> pack = new List<object>();
            foreach (var item in sessctl.GetSessionTable()) {
                pack.Add(new {
                    type = item.type.ToString(),
                    localip = item.lep.ToString(),
                    remoteip = item.rep == null ? "" : item.rep.ToString(),
                });
            }

            response.data = pack;
        }
    }
}
