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
        public string id;
        public byte[] matchkey;
        public T func;

        public Service(string id, byte[] key, T func)
        {
            this.id = id;
            this.matchkey = key;
            this.func = func;
        }
    }

    public class ServiceCore {
        // service
        private Service<ServiceDelegate> default_service;
        private List<Service<ServiceDelegate>> servtab;
        private ReaderWriterLockSlim servtab_lock;

        // filter
        private List<Service<FilterDelegate>> filttab;
        private ReaderWriterLockSlim filttab_lock;

        // request
        private Queue<ServiceRequest> request_queue;
        public ServiceDoBeforeDelegate serv_before_do;
        public ServiceDoneDelegate serv_done;

        public ServiceCore()
        {
            default_service = null;
            servtab = new List<Service<ServiceDelegate>>();
            servtab_lock = new ReaderWriterLockSlim();

            filttab = new List<Service<FilterDelegate>>();
            filttab_lock = new ReaderWriterLockSlim();

            request_queue = new Queue<ServiceRequest>();
            serv_before_do = null;
            serv_done = null;
        }

        // Register =============================================================================

        public void RegisterDefaultService(string id, ServiceDelegate func)
        {
            default_service = new Service<ServiceDelegate>(id, new byte[] { 0 }, func);
        }

        public int RegisterService(string id, ServiceDelegate func, byte[] key)
        {
            int retval = 0;

            servtab_lock.EnterWriteLock();
            var subset = from s in servtab where s.id.Equals(id) select s;
            if (subset.Any())
                retval = 1;

            // insert new service to table sorted by length of keyname decended
            Service<ServiceDelegate> tmp = null;
            for (int i = 0; i < servtab.Count; i++) {
                if (servtab[i].matchkey.Length < key.Length) {
                    tmp = new Service<ServiceDelegate>(id, key, func);
                    servtab.Insert(i, tmp);
                    break;
                }
            }
            if (tmp == null)
                servtab.Add(new Service<ServiceDelegate>(id, key, func));
            servtab_lock.ExitWriteLock();

            return retval;
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

        public int RegisterFilter(string id, FilterDelegate func)
        {
            int retval = 0;

            filttab_lock.EnterWriteLock();
            var subset = from s in filttab where s.id.Equals(id) select s;
            if (subset.Any())
                retval = 1;

            filttab.Add(new Service<FilterDelegate>(id, new byte[] { 0 }, func));
            filttab_lock.ExitWriteLock();

            return retval;
        }

        public void DeregisterFilter(string id)
        {
            filttab_lock.EnterWriteLock();
            foreach (var item in filttab) {
                if (item.id.Equals(id)) {
                    filttab.Remove(item);
                    break;
                }
            }
            filttab_lock.ExitWriteLock();
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
                // filter return when retval is false
                filttab_lock.EnterWriteLock();
                foreach (var item in filttab) {
                    if (!item.func(ref request, response))
                        return;
                }
                filttab_lock.ExitWriteLock();

                // service return when find target service and handled request
                servtab_lock.EnterWriteLock();
                foreach (var item in servtab) {
                    if (ByteArrayCmp(item.matchkey, request.data.Take(item.matchkey.Length).ToArray())) {
                        item.func(request, ref response);
                        return;
                    }
                }
                servtab_lock.ExitWriteLock();

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
