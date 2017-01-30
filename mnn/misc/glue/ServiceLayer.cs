using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using mnn.service;
using mnn.util;

namespace mnn.misc.glue {
    public class ServiceLayer : IRunable {
        public SimpleFilterCore filtctl;
        public SimpleServiceCore servctl;

        public ServiceLayer()
        {
            filtctl = new SimpleFilterCore();
            filtctl.filter_done += new Service.ServiceDoneDelegate(OnFilterDone);

            servctl = new SimpleServiceCore();
            servctl.service_before += new Service.ServiceBeforeDelegate(OnServiceBefore);
            servctl.service_done += new Service.ServiceDoneDelegate(OnServiceDone);
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
                    log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType)
                        .Error("Exception thrown out by core thread.", ex);
                }
            }
        }

        protected virtual void Exec()
        {
            if (filtctl.Exec() == 0)
                System.Threading.Thread.Sleep(100);
            servctl.Exec();
        }

        // Service Event ===========================================================================

        protected virtual void OnFilterDone(ServiceRequest request, ServiceResponse response)
        {
            ServiceRequest new_request = response.data as ServiceRequest;

            if (new_request != null)
                servctl.AddRequest(new_request);
        }

        protected virtual void OnServiceBefore(ref ServiceRequest request)
        {
            //if (request is UnknownRequest) {
            //    try {
            //        byte[] result = Convert.FromBase64String(Encoding.UTF8.GetString((byte[])request.data));
            //        result = EncryptSym.AESDecrypt(result);
            //        if (result != null)
            //            request.data = result;
            //    } catch (Exception) { }
            //}
        }

        protected virtual void OnServiceDone(ServiceRequest request, ServiceResponse response) { }

        // Default Service =========================================================================

        protected virtual void DefaultService(ServiceRequest request, ref ServiceResponse response)
        {
            if (request.id.StartsWith("service.")) {
                response.id = "unknown";
                response.errcode = 10024;
                response.errmsg = "unknown request";
            } else {
                throw new Exception("bad request");
            }
        }
    }
}
