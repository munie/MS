using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace mnn.service {
    public class SimpleFilterCore {
        private List<Service> filters;
        private ReaderWriterLockSlim filters_lock;
        private Queue<Service> matched_filters;
        private Queue<ServiceRequest> request_queue;

        public Service.ServiceDoneDelegate filter_done; 

        public SimpleFilterCore()
        {
            filters = new List<Service>();
            filters_lock = new ReaderWriterLockSlim();
            matched_filters = new Queue<Service>();
            request_queue = new Queue<ServiceRequest>();

            filter_done = null;
        }

        // Register =============================================================================

        public int RegisterFilter(string id, Service.ServiceHandlerDelegate func,
            Service.ServiceDoneDelegate done)
        {
            try {
                filters_lock.EnterWriteLock();
                var subset = from s in filters where s.id.Equals(id) select s;

                if (subset.Any()) {
                    return 1;
                } else {
                    filters.Add(new RegexService(id, func, done));
                    return 0;
                }
            } finally {
                filters_lock.ExitWriteLock();
            }
        }

        public void UnregisterFilter(string id)
        {
            filters_lock.EnterWriteLock();
            foreach (var item in filters) {
                if (item.id.Equals(id)) {
                    filters.Remove(item);
                    break;
                }
            }
            filters_lock.ExitWriteLock();
        }

        // Request ==============================================================================

        public void AddRequest(ServiceRequest request)
        {
            lock (request_queue) {
                if (request_queue.Count > 2048) {
                    log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType)
                        .Fatal("pack_queue's count is larger than 2048!");
                    request_queue.Clear();
                } else {
                    request_queue.Enqueue(request);
                }
            }
        }

        private void DoFilter(ServiceRequest request, ref ServiceResponse response)
        {
            // find matched filters
            matched_filters.Clear();
            foreach (var item in filters) {
                if (item.IsMatch(request))
                    matched_filters.Enqueue(item);
            }

            // no matched filters, allow through
            if (matched_filters.Count == 0) {
                response.data = request;
                return;
            }

            // call all funcs with matched filters
            while (matched_filters.Count != 0) {
                var filter = matched_filters.Dequeue();

                filter.DoServiceDirect(request, ref response);
                request = response.data as ServiceRequest;
            }
        }

        public int Exec()
        {
            lock (request_queue) {
                int retval = request_queue.Count;

                while (request_queue.Count != 0) {
                    var request = request_queue.Dequeue();

                    ServiceResponse response = new ServiceResponse() {
                        id = request.id,
                        errcode = 0,
                        errmsg = "",
                        data = "",
                    };

                    DoFilter(request, ref response);
                    if (filter_done != null)
                        filter_done(request, response);
                }

                return retval;
            }
        }
    }
}
