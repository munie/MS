using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using mnn.net;

namespace mnn.design {
    public delegate void ServiceDelegate(SockRequest request, ref SockResponse response);
    public delegate bool FilterDelegate(ref SockRequest request, SockResponse response);

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

    public class DispatcherBase {
        protected Service<ServiceDelegate> default_service;
        protected List<Service<ServiceDelegate>> service_table;
        protected List<Service<FilterDelegate>> filter_table;

        public DispatcherBase()
        {
            default_service = null;
            service_table = new List<Service<ServiceDelegate>>();
            filter_table = new List<Service<FilterDelegate>>();
        }

        // Methods =============================================================================

        private bool byteArrayCmp(byte[] first, byte[] second)
        {
            if (first.Length != second.Length)
                return false;

            for (int i = 0; i < first.Length; i++) {
                if (first[i] != second[i])
                    return false;
            }

            return true;
        }

        public virtual void handle(SockRequest request, ref SockResponse response)
        {
            try {
                // filter return when retval is false
                foreach (var item in filter_table) {
                    if (!item.func(ref request, response))
                        return;
                }

                // service return when find target service and handled request
                foreach (var item in service_table) {
                    if (byteArrayCmp(item.keyname, request.data.Take(item.keyname.Length).ToArray())) {
                        item.func(request, ref response);
                        return;
                    }
                }

                // do default service
                if (default_service != null)
                    default_service.func(request, ref response);
            } catch (Exception ex) {
                log4net.ILog log = log4net.LogManager.GetLogger(typeof(DispatcherBase));
                log.Warn("Exception of invoking service.", ex);
            }
        }

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
    }
}
