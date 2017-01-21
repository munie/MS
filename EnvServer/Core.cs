using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using mnn.net;
using mnn.service;
using mnn.module;
using mnn.misc.glue;
using Newtonsoft.Json;

namespace EnvServer {
    public class Core : CoreBase {
        public Core()
        {
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

            foreach (var item in sessctl.GetSessionTable()) {
                SessData sd = item.sdata as SessData;
                if (sd != null && sd.IsAdmin)
                    sessctl.SendSession(item, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response)));
            }
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

            foreach (var item in sessctl.GetSessionTable()) {
                SessData sd = item.sdata as SessData;
                if (sd != null && sd.IsAdmin)
                    sessctl.SendSession(item, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response)));
            }
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

            foreach (var item in sessctl.GetSessionTable()) {
                SessData sd = item.sdata as SessData;
                if (sd != null && sd.IsAdmin)
                    sessctl.SendSession(item, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response)));
            }
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

            foreach (var item in sessctl.GetSessionTable()) {
                SessData sd = item.sdata as SessData;
                if (sd != null && sd.IsAdmin)
                    sessctl.SendSession(item, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response)));
            }
        }

        // Session Event ====================================================================

        protected override void OnSessCreate(object sender, SockSess sess)
        {
            ServiceResponse response = new ServiceResponse();
            response.id = "notice.sesscreate";
            response.data = new {
                type = sess.type.ToString(),
                localip = sess.lep.ToString(),
                remoteip = sess.rep == null ? "" : sess.rep.ToString(),
                tick = sess.tick,
                conntime = sess.conntime,
            };

            foreach (var item in sessctl.GetSessionTable()) {
                SessData sd = item.sdata as SessData;
                if (sd != null && sd.IsAdmin)
                    sessctl.SendSession(item, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response)));
            }
        }

        protected override void OnSessDelete(object sender, SockSess sess)
        {
            ServiceResponse response = new ServiceResponse();
            response.id = "notice.sessdelete";
            response.data = new {
                type = sess.type.ToString(),
                localip = sess.lep.ToString(),
                remoteip = sess.rep == null ? "" : sess.rep.ToString(),
            };

            foreach (var item in sessctl.GetSessionTable()) {
                SessData sd = item.sdata as SessData;
                if (sd != null && sd.IsAdmin)
                    sessctl.SendSession(item, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response)));
            }
        }

        // Service ==========================================================================

        protected override void SessDetailService(ServiceRequest request, ref ServiceResponse response)
        {
            // make pack of session detail
            List<object> pack = new List<object>();
            foreach (var item in sessctl.GetSessionTable()) {
                SessData sd = item.sdata as SessData;
                pack.Add(new {
                    type = item.type.ToString(),
                    localip = item.lep.ToString(),
                    remoteip = item.rep == null ? "" : item.rep.ToString(),
                    tick = item.tick,
                    conntime = item.conntime,
                    parentport = sd != null ? sd.ParentPort : 0,
                    ccid = sd != null ? sd.Ccid : "",
                    name = sd != null ? sd.Name : "",
                });
            }

            response.data = pack;
        }

        private void SessLoginService(ServiceRequest request, ref ServiceResponse response)
        {
            SockSess sess = request.user_data as SockSess;
            if (sess == null)
                return;

            if (sess.sdata == null)
                sess.sdata = new SessData();
            SessData sd = sess.sdata as SessData;

            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>(Encoding.UTF8.GetString(request.raw_data));

            sd.IsAdmin = bool.Parse(dc["admin"]);
            sd.Ccid = dc["ccid"];
            sd.Name = dc["name"];
            sd.ParentPort = sess.lep.Port;
        }
    }
}
