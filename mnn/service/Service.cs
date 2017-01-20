using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace mnn.service {
    public delegate void ServiceDelegate(ServiceRequest request, ref ServiceResponse response);
    public delegate void ServiceDoBeforeDelegate(ref ServiceRequest request);
    public delegate void ServiceDoneDelegate(ServiceRequest request, ServiceResponse response);

    public class Service {
        public string id;
        public ServiceDelegate func;

        public Service(string id, ServiceDelegate func)
        {
            this.id = id;
            this.func = func;
        }
    }

    public class ServiceCore {
        // service
        private Service default_service;
        private List<Service> servtab;
        private ReaderWriterLockSlim servtab_lock;

        // request
        private Queue<ServiceRequest> request_queue;
        public ServiceDoBeforeDelegate serv_before_do;
        public ServiceDoneDelegate serv_done;

        public ServiceCore()
        {
            default_service = null;
            servtab = new List<Service>();
            servtab_lock = new ReaderWriterLockSlim();

            request_queue = new Queue<ServiceRequest>();
            serv_before_do = null;
            serv_done = null;
        }

        // Register =============================================================================

        public void RegisterDefaultService(string id, ServiceDelegate func)
        {
            default_service = new Service(id, func);
        }

        public int RegisterService(string id, ServiceDelegate func)
        {
            try {
                servtab_lock.EnterWriteLock();
                var subset = from s in servtab where s.id.Equals(id) select s;

                if (subset.Any()) {
                    return 1;
                } else {
                    servtab.Add(new Service(id, func));
                    return 0;
                }
            } finally {
                servtab_lock.ExitWriteLock();
            }
        }

        public void DeregisterService(string id)
        {
            servtab_lock.EnterWriteLock();
            foreach (var item in servtab) {
                if (item.id.Equals(id)) {
                    servtab.Remove(item);
                    break;
                }
            }
            servtab_lock.ExitWriteLock();
        }

        // Request ==============================================================================

        public void AddRequest(ServiceRequest request)
        {
            lock (request) {
                if (request_queue.Count > 2048) {
                    log4net.ILog log = log4net.LogManager.GetLogger(typeof(ServiceCore));
                    log.Fatal("pack_queue's count is larger than 2048!");
                    request_queue.Clear();
                } else {
                    request_queue.Enqueue(request);
                }
            }
        }

        private bool ByteArrayCmp(byte[] first, byte[] second)
        {
            if (first.Length != second.Length)
                return false;

            for (int i = 0; i < first.Length; i++) {
                if (first[i] != second[i])
                    return false;
            }

            return true;
        }

        // export for synchronous call
        public void DoService(ServiceRequest request, ref ServiceResponse response)
        {
            try {
                servtab_lock.EnterReadLock();
                var tmpservtab = servtab.ToList();
                servtab_lock.ExitReadLock();
                if (request.content_mode == ServiceRequestContentMode.json) {
                    IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                        <Dictionary<string, dynamic>>(Encoding.UTF8.GetString(request.raw_data));

                    foreach (var item in tmpservtab) {
                        if (item.id.Equals(dc["id"])) {
                            response.id = dc["id"];
                            response.errcode = 0;
                            response.errmsg = "";
                            response.data = "";
                            item.func(request, ref response);
                            return;
                        }
                    }
                } else {
                    foreach (var item in tmpservtab) {
                        if (ByteArrayCmp(Encoding.UTF8.GetBytes(item.id),
                            request.raw_data.Take(item.id.Length).ToArray())) {
                            item.func(request, ref response);
                            return;
                        }
                    }
                }

                // do default service
                if (default_service != null)
                    default_service.func(request, ref response);
            } catch (Exception ex) {
                log4net.ILog log = log4net.LogManager.GetLogger(typeof(ServiceCore));
                log.Warn("Exception of invoking service.", ex);
            }
        }

        public void Exec()
        {
            try {
                ServiceRequest request = null;
                lock (request_queue) {
                    if (request_queue.Count != 0) // Any() is not thread safe
                        request = request_queue.Dequeue();
                }
                if (request == null)
                    return;

                ServiceResponse response = new ServiceResponse();
                if (serv_before_do != null)
                    serv_before_do(ref request);
                DoService(request, ref response);
                if (serv_done != null)
                    serv_done(request, response);
            } catch (Exception ex) {
                log4net.ILog log = log4net.LogManager.GetLogger(typeof(ServiceCore));
                log.Warn("Exception of handling request to modules.", ex);
            }
        }
    }
}
