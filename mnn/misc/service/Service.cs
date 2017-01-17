using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace mnn.misc.service {
    public delegate void ServiceDelegate(ServiceRequest request, ref ServiceResponse response);
    public delegate bool FilterDelegate(ref ServiceRequest request, ServiceResponse response);

    public delegate void ServiceDoBeforeDelegate(ref ServiceRequest request);
    public delegate void ServiceDoneDelegate(ServiceRequest request, ServiceResponse response);

    public class Service<T> {
        public string name;
        public byte[] keyname;
        public T func;

        public Service(string name, byte[] key, T func)
        {
            this.name = name;
            this.keyname = key;
            this.func = func;
        }
    }

    public class ServiceCore {
        protected Service<ServiceDelegate> default_service;
        protected List<Service<ServiceDelegate>> service_table;
        protected List<Service<FilterDelegate>> filter_table;

        private Queue<ServiceRequest> request_queue;
        public ServiceDoBeforeDelegate serv_before_do;
        public ServiceDoneDelegate serv_done;

        public ServiceCore()
        {
            default_service = null;
            service_table = new List<Service<ServiceDelegate>>();
            filter_table = new List<Service<FilterDelegate>>();

            request_queue = new Queue<ServiceRequest>();
            serv_before_do = null;
            serv_done = null;

            RunDoService();
        }

        // Register =============================================================================

        public void RegisterDefaultService(string name, ServiceDelegate func)
        {
            default_service = new Service<ServiceDelegate>(name, new byte[] { 0 }, func);
        }

        public void RegisterService(string name, ServiceDelegate func, byte[] key)
        {
            var subset = from s in service_table where s.name.Equals(name) select s;
            if (subset.Any()) return;

            // insert new service to table sorted by length of keyname decended
            Service<ServiceDelegate> tmp = null;
            for (int i = 0; i < service_table.Count; i++) {
                if (service_table[i].keyname.Length < key.Length) {
                    tmp = new Service<ServiceDelegate>(name, key, func);
                    service_table.Insert(i, tmp);
                    break;
                }
            }
            if (tmp == null)
                service_table.Add(new Service<ServiceDelegate>(name, key, func));
        }

        public void DeregisterService(string name)
        {
            foreach (var item in service_table) {
                if (item.name.Equals(name)) {
                    service_table.Remove(item);
                    break;
                }
            }
        }

        public void RegisterFilter(string name, FilterDelegate func)
        {
            var subset = from s in filter_table where s.name.Equals(name) select s;
            if (subset.Any()) return;

            filter_table.Add(new Service<FilterDelegate>(name, new byte[] { 0 }, func));
        }

        public void DeregisterFilter(string name)
        {
            foreach (var item in filter_table) {
                if (item.name.Equals(name)) {
                    filter_table.Remove(item);
                    break;
                }
            }
        }

        // Request ==============================================================================

        public void AddRequest(ServiceRequest request)
        {
            if (request_queue.Count > 2048) {
                log4net.ILog log = log4net.LogManager.GetLogger(typeof(ServiceCore));
                log.Fatal("pack_queue's count is larger than 2048!");
                request_queue.Clear();
            } else {
                request_queue.Enqueue(request);
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
                // filter return when retval is false
                foreach (var item in filter_table) {
                    if (!item.func(ref request, response))
                        return;
                }

                // service return when find target service and handled request
                foreach (var item in service_table) {
                    if (ByteArrayCmp(item.keyname, request.data.Take(item.keyname.Length).ToArray())) {
                        item.func(request, ref response);
                        return;
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

        private void RunDoService()
        {
            Thread thread = new Thread(() =>
            {
                while (true) {
                    try {
                        ServiceRequest request = null;
                        lock (request_queue) {
                            if (request_queue.Count != 0) // Any() is not thread safe
                                request = request_queue.Dequeue();
                        }
                        if (request == null) {
                            Thread.Sleep(1000);
                            continue;
                        }

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
            });
            thread.IsBackground = true;
            thread.Start();
        }
    }
}
