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
    public class Core : BaseLayerNew {
        public Core()
        {
            sess_listen_event += new SockSessOpenDelegate(OnSessCreate);
            sess_accept_event += new SockSessOpenDelegate(OnSessCreate);
            sess_connect_event += new SockSessOpenDelegate(OnSessCreate);
            sess_close_event += new SockSessCloseDelegate(OnSessDelete);

            servctl.RegisterService("service.sesslogin", SessLoginService);
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

        protected void OnSessCreate(object sender, SockSessNew sess)
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

        protected void OnSessDelete(object sender, SockSessNew sess)
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

        // Service ==========================================================================

        protected override void SessDetailService(ServiceRequest request, ref ServiceResponse response)
        {
            // make pack of session detail
            List<object> pack = new List<object>();
            foreach (var item in sesstab) {
                SessData sd = item.sdata as SessData;
                pack.Add(new {
                    sessid = item.id,
                    type = item.GetType().Name,
                    localip = item.lep.ToString(),
                    remoteip = item.rep == null ? "0:0" : item.rep.ToString(),
                    tick = item.tick,
                    conntime = item.conntime,
                    ccid = sd != null ? sd.Ccid : "",
                    name = sd != null ? sd.Name : "",
                    isadmin = sd != null ? sd.Admin.ToString() : "",
                });
            }

            response.data = pack;
        }

        private void SessLoginService(ServiceRequest request, ref ServiceResponse response)
        {
            SockSessNew sess = request.user_data as SockSessNew;
            if (sess == null)
                return;

            if (sess.sdata == null)
                sess.sdata = new SessData();
            SessData sd = sess.sdata as SessData;

            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>((string)request.data);

            sd.Ccid = dc["ccid"];
            sd.Name = dc["name"];
            sd.Admin = bool.Parse(dc["admin"]);
        }

        // Utilization

        private void NoticeAdmin(ServiceResponse response)
        {
            foreach (var item in sesstab) {
                SessData sd = item.sdata as SessData;
                if (sd != null && sd.Admin)
                    item.wfifo.Append(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response)));
            }
        }
    }
}
