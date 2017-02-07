using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using mnn.service;
using mnn.util;

namespace mnn.misc.glue {
    public class ServiceLayer {
        private List<Service> filttab;
        private Service default_filter;

        private List<Service> servtab;
        private Service default_service;

        public ServiceLayer()
        {
            filttab = new List<Service>();
            default_filter = new Service("filter.defualt", DefaultFilter, OnFilterDone);
            Loop.default_loop.Add(default_filter);

            servtab = new List<Service>();
            default_service = new Service("service.defualt", DefaultService, OnServiceDone);
            Loop.default_loop.Add(default_service);
        }

        public void Run()
        {
            Loop.default_loop.Run();
        }

        public void RegisterService(string id, Service.ServiceHandlerDelegate func,
                Service.ServiceDoneDelegate done)
        {
            Service serv = new Service(id, func, done);
            servtab.Add(serv);
            Loop.default_loop.Add(serv);
        }

        public void UnregisterService(string id)
        {
            foreach (var item in servtab) {
                if (item.id.Equals(id)) {
                    servtab.Remove(item);
                    break;
                }
            }
        }

        public void RegisterFilter(string id, Service.ServiceHandlerDelegate func,
                Service.ServiceDoneDelegate done)
        {
            Service filter = new Service(id, func, done);
            filttab.Add(filter);
            Loop.default_loop.Add(filter);
        }

        public void UnregisterFilter(string id)
        {
            foreach (var item in filttab) {
                if (item.id.Equals(id)) {
                    filttab.Remove(item);
                    break;
                }
            }
        }

        public void ReplaceDefaultService(Service.ServiceHandlerDelegate func, Service.ServiceDoneDelegate done)
        {
            default_service = new Service("service.default", func, done);
        }

        public void AddServiceDone(string id, Service.ServiceDoneDelegate done)
        {
            foreach (var item in servtab) {
                if (item.id.Equals(id)) {
                    item.service_done += done;
                    break;
                }
            }
        }

        public void DelServiceDone(string id, Service.ServiceDoneDelegate done)
        {
            foreach (var item in servtab) {
                if (item.id.Equals(id)) {
                    item.service_done -= done;
                    break;
                }
            }
        }

        public void AddServiceRequest(ServiceRequest request)
        {
            foreach (var item in filttab) {
                if (item.IsMatch(request)) {
                    item.AddRequest(request);
                    return;
                }
            }
            if (default_filter != null)
                default_filter.AddRequest(request);
        }

        // Service Event ===========================================================================

        protected virtual void OnFilterDone(ServiceRequest request, ServiceResponse response)
        {
            ServiceRequest new_request = response.data as ServiceRequest;

            if (new_request != null) {
                foreach (var item in servtab) {
                    if (item.IsMatch(request)) {
                        item.AddRequest(request);
                        return;
                    }
                }
                if (default_service != null)
                    default_service.AddRequest(request);
            }
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

        protected virtual void DefaultFilter(ServiceRequest request, ref ServiceResponse response)
        {
            response.data = request;
        }

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
