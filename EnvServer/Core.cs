using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using mnn.net;
using mnn.service;
using mnn.module;
using mnn.misc.glue;
using mnn.misc.env;
using Newtonsoft.Json;

namespace EnvServer {
    public class Core : BaseLayer {
        public Core()
        {
            sess_listen_event += new SockSessOpenDelegate(OnSessCreate);
            sess_accept_event += new SockSessOpenDelegate(OnSessCreate);
            sess_connect_event += new SockSessOpenDelegate(OnSessCreate);
            sess_close_event += new SockSessCloseDelegate(OnSessDelete);

            servctl.RegisterService("service.sesslogin", LoginService, OnServiceDone);
        }

        // Module Event =====================================================================

        protected override void OnModuleCtlAdd(object sender, Module module)
        {
            base.OnModuleCtlAdd(sender, module);

            ServiceResponse response = new ServiceResponse();
            response.id = "notice.moduleadd";
            response.data = new {
                name = module.AssemblyName,
                version = module.Version,
                state = module.State.ToString(),
            };

            NoticeAdmin(response);
        }

        protected override void OnModuleCtlDelete(object sender, Module module)
        {
            base.OnModuleCtlDelete(sender, module);

            ServiceResponse response = new ServiceResponse();
            response.id = "notice.moduledelete";
            response.data = new {
                name = module.AssemblyName,
                state = module.State.ToString(),
            };

            NoticeAdmin(response);
        }

        protected override void OnModuleLoad(Module module)
        {
            base.OnModuleLoad(module);

            ServiceResponse response = new ServiceResponse();
            response.id = "notice.moduleupdate";
            response.data = new {
                name = module.AssemblyName,
                state = module.State.ToString(),
            };

            NoticeAdmin(response);
        }

        protected override void OnModuleUnload(Module module)
        {
            base.OnModuleUnload(module);

            ServiceResponse response = new ServiceResponse();
            response.id = "notice.moduleupdate";
            response.data = new {
                name = module.AssemblyName,
                state = module.State.ToString(),
            };

            NoticeAdmin(response);
        }

        // Session Event ====================================================================

        private void OnSessCreate(object sender, SockSess sess)
        {
            log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
            if (sess is SockSessServer)
                log.Info(String.Format("Session #L listened at {0}.", sess.lep.ToString()));
            else if (sess is SockSessAccept)
                log.Info(String.Format("Session #A accepted to {0}.", sess.rep.ToString()));
            else// if (sess is SockSessClient)
                log.Info(String.Format("Session #C connected to {0}.", sess.rep.ToString()));

            ServiceResponse response = new ServiceResponse();
            if (sess is SockSessServer)
                response.id = "notice.sesslisten";
            else if (sess is SockSessAccept)
                response.id = "notice.sessaccept";
            else// if (sess is SockSessClient)
                response.id = "notice.sessconnect";
            response.data = new {
                sessid = sess.id,
                type = sess.GetType().Name,
                localip = sess.lep.ToString(),
                remoteip = sess.rep == null ? "0:0" : sess.rep.ToString(),
                tick = sess.tick,
                conntime = sess.conntime,
            };

            NoticeAdmin(response);
        }

        private void OnSessDelete(object sender, SockSess sess)
        {
            log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
            if (sess is SockSessServer)
                log.Info(String.Format("Session #* deleted from {0}.", sess.lep.ToString()));
            else
                log.Info(String.Format("Session #* deleted from {0}.", sess.rep.ToString()));

            ServiceResponse response = new ServiceResponse();
            response.id = "notice.sessclose";
            response.data = new {
                sessid = sess.id,
            };

            NoticeAdmin(response);
        }

        // SockSess Service =================================================================

        protected override void SessDetailService(ServiceRequest request, ref ServiceResponse response)
        {
            // make pack of session detail
            List<object> pack = new List<object>();
            foreach (var item in sesstab) {
                Dictionary<string, string> sd = item.sdata as Dictionary<string, string>;
                pack.Add(new {
                    sessid = item.id,
                    type = item.GetType().Name,
                    localip = item.lep.ToString(),
                    remoteip = item.rep == null ? "0:0" : item.rep.ToString(),
                    tick = item.tick,
                    conntime = item.conntime,
                    ccid = sd != null && sd.ContainsKey("ccid") ? sd["ccid"] : "",
                    name = sd != null && sd.ContainsKey("name") ? sd["name"] : "",
                    admin = sd != null && sd.ContainsKey("admin") ? sd["admin"] : "false",
                });
            }

            response.data = pack;
        }

        // Core Service =====================================================================

        private void LoginService(ServiceRequest request, ref ServiceResponse response)
        {
            if (request.sessdata == null)
                return;

            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>((string)request.data);

            request.sessdata["ccid"] = dc["ccid"];
            request.sessdata["name"] = dc["name"];
            if (dc.ContainsKey("admin"))
                request.sessdata["admin"] = dc["admin"];
        }

        // Utilization ======================================================================

        private void NoticeAdmin(ServiceResponse response)
        {
            foreach (var item in sesstab) {
                Dictionary<string, string> sd = item.sdata as Dictionary<string, string>;
                if (sd != null && sd.ContainsKey("admin") && sd["admin"] == "true")
                    item.wfifo.Append(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response)));
            }
        }
    }
}
