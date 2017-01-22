using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using mnn.service;
using mnn.util;

namespace mnn.misc.glue {
    public class ServiceLayer : IRunable {
        public ServiceCore filtctl;
        public ServiceCore servctl;

        public ServiceLayer()
        {
            // init filtctl
            filtctl = new ServiceCore();
            filtctl.RegisterDefaultService("filter.default", DefaultFilter);

            // init servctl
            servctl = new ServiceCore();
            servctl.serv_before_do += new ServiceDoBeforeDelegate(OnServBeforeDo);
            servctl.serv_done += new ServiceDoneDelegate(OnServDone);
            servctl.RegisterDefaultService("service.default", DefaultService);
        }

        public void Run()
        {
            System.Threading.Thread thread = new System.Threading.Thread(() => {
                RunForever();
            });
            thread.IsBackground = true;
            thread.Start();
        }

        public void RunForever()
        {
            while (true) {
                try {
                    Exec();
                } catch (Exception ex) {
                    log4net.ILog log = log4net.LogManager.GetLogger(typeof(BaseLayer));
                    log.Error("Exception thrown out by core thread.", ex);
                }
            }
        }

        protected virtual void Exec()
        {
            filtctl.Exec(0);
            servctl.Exec(1000);
        }

        // Service Event ==================================================================================

        protected virtual void OnServBeforeDo(ref ServiceRequest request)
        {
            if (request.content_mode == ServiceRequestContentMode.uri) {
                try {
                    byte[] result = Convert.FromBase64String(Encoding.UTF8.GetString(request.raw_data));
                    result = EncryptSym.AESDecrypt(result);
                    if (result != null)
                        request.raw_data = result;
                } catch (Exception) { }
            }
        }

        protected virtual void OnServDone(ServiceRequest request, ServiceResponse response) { }

        // Service =========================================================================

        protected virtual void DefaultFilter(ServiceRequest request, ref ServiceResponse response)
        {
            servctl.AddRequest(request);
        }

        protected virtual void DefaultService(ServiceRequest request, ref ServiceResponse response)
        {
            if (request.content_mode == ServiceRequestContentMode.json) {
                IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                    <Dictionary<string, dynamic>>(Encoding.UTF8.GetString(request.raw_data));

                if (((string)dc["id"]).StartsWith("service.")) {
                    response.id = "unknown";
                    response.errcode = 10024;
                    response.errmsg = "unknown request";
                } else {
                    throw new Exception("bad request");
                }
            }
        }
    }
}
