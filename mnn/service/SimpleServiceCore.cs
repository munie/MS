using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace mnn.service {
    public class SimpleServiceCore {
        private Service default_service;
        private List<Service> services;
        private ReaderWriterLockSlim services_lock;

        public Service.ServiceBeforeDelegate service_before;
        public Service.ServiceDoneDelegate service_done;

        public SimpleServiceCore()
        {
            default_service = null;
            services = new List<Service>();
            services_lock = new ReaderWriterLockSlim();

            service_before = null;
            service_done = null;
        }

        // Service Event ==================================================================================

        protected virtual void OnServiceBefore(ref ServiceRequest request)
        {
            if (service_before != null)
                service_before(ref request);
        }

        protected virtual void OnServiceDone(ServiceRequest request, ServiceResponse response)
        {
            if (service_done != null)
                service_done(request, response);
        }

        // Register =============================================================================

        public void RegisterDefaultService(string id, Service.ServiceHandlerDelegate func)
        {
            lock (this) {
                default_service = new Service(id, func);
                default_service.service_before += new Service.ServiceBeforeDelegate(OnServiceBefore);
                default_service.service_done += new Service.ServiceDoneDelegate(OnServiceDone);
            }
        }

        public int RegisterService(string id, Service.ServiceHandlerDelegate func)
        {
            try {
                services_lock.EnterWriteLock();
                var subset = from s in services where s.id.Equals(id) select s;

                if (subset.Any()) {
                    return 1;
                } else {
                    Service serv = new Service(id, func);
                    services.Add(serv);
                    serv.service_before += new Service.ServiceBeforeDelegate(OnServiceBefore);
                    serv.service_done += new Service.ServiceDoneDelegate(OnServiceDone);
                    return 0;
                }
            } finally {
                services_lock.ExitWriteLock();
            }
        }

        public void UnregisterService(string id)
        {
            services_lock.EnterWriteLock();
            foreach (var item in services) {
                if (item.id.Equals(id)) {
                    services.Remove(item);
                    break;
                }
            }
            services_lock.ExitWriteLock();
        }

        // Request ==============================================================================

        public void AddRequest(ServiceRequest request)
        {
            try {
                services_lock.EnterWriteLock();
                foreach (var item in services) {
                    if (item.IsMatch(request)) {
                        item.AddRequest(request);
                        return;
                    }
                }
                if (default_service != null)
                    default_service.AddRequest(request);
            } finally {
                services_lock.ExitWriteLock();
            }
        }

        public int Exec()
        {
            services_lock.EnterReadLock();
            var tmpservtab = services.ToList();
            services_lock.ExitReadLock();

            int request_amount = 0;

            foreach (var item in tmpservtab) {
                request_amount += item.request_count;
                item.DoService();
            }

            default_service.DoService();
            request_amount += default_service.request_count;

            return request_amount;
        }
    }
}
