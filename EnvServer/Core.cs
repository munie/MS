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
            servctl.RegisterService("core.sesslogin", SessLoginService);
        }

        // Session Event ==================================================================================

        protected override void OnSessCreate(object sender, SockSess sess)
        {
            ServiceResponse response = new ServiceResponse();
            response.id = "notice.core.sesscreate";
            response.data = new {
                type = sess.type.ToString(),
                localip = sess.lep.ToString(),
                remoteip = sess.rep == null ? "" : sess.rep.ToString(),
                conntime = DateTime.Now.ToString(),
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
            response.id = "notice.core.sessdelete";
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
                    conntime = sd != null ? sd.ConnTime.ToString() : DateTime.MinValue.ToString(),
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

            sd.IsAdmin = bool.Parse((string)dc["admin"]);
            sd.ConnTime = DateTime.Now;
        }
    }
}
