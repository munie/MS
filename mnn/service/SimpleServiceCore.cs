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

        public int current_request_count { get; set; }
        public int handled_request_count { get; set; }

        public SimpleServiceCore(Service.ServiceHandlerDelegate func, Service.ServiceDoneDelegate done)
        {
            default_service = new Service("service.default", func, done);
            services = new List<Service>();
            services_lock = new ReaderWriterLockSlim();
            current_request_count = 0;
            handled_request_count = 0;
        }

        // Register =============================================================================

        public void ReplaceDefaultService(Service.ServiceHandlerDelegate func, Service.ServiceDoneDelegate done)
        {
            lock (this) {
                default_service = new Service("service.default", func, done);
            }
        }

        public int RegisterService(string id, Service.ServiceHandlerDelegate func,
            Service.ServiceDoneDelegate done)
        {
            try {
                services_lock.EnterWriteLock();
                var subset = from s in services where s.id.Equals(id) select s;

                if (subset.Any()) {
                    return 1;
                } else {
                    Service serv = new Service(id, func, done);
                    services.Add(serv);
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

        public void AddServiceDone(string id, Service.ServiceDoneDelegate done)
        {
            services_lock.EnterWriteLock();
            foreach (var item in services) {
                if (item.id.Equals(id)) {
                    item.service_done += done;
                    break;
                }
            }
            services_lock.ExitWriteLock();
        }

        public void DelServiceDone(string id, Service.ServiceDoneDelegate done)
        {
            services_lock.EnterWriteLock();
            foreach (var item in services) {
                if (item.id.Equals(id)) {
                    item.service_done -= done;
                    break;
                }
            }
            services_lock.ExitWriteLock();
        }

        // Request ==============================================================================

        public void AddServiceRequest(ServiceRequest request)
        {
            current_request_count++;

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

            request_amount += default_service.request_count;
            default_service.DoService();

            handled_request_count += request_amount;
            return request_amount;
        }
    }
}
