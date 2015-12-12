using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace mnn.net {
    public class Service {
        public delegate void ServiceDelegate(SockRequest request, SockResponse response);

        public string name;
        public byte[] keyname;
        public ServiceDelegate func;

        public Service(string name, byte[] key, ServiceDelegate func)
        {
            this.name = name;
            this.keyname = key;
            this.func = func;
        }
    }

    public class DispatcherBase {
        protected List<Service> service_table;
        protected Service default_service;

        public DispatcherBase()
        {
            service_table = new List<Service>();
            default_service = null;
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

        public virtual void handle(SockRequest request, SockResponse response)
        {
            try {
                if (request.type == SockRequestType.none && default_service != null) {
                    default_service.func(request, response);
                    return;
                }
                foreach (var item in service_table) {
                    if (byteArrayCmp(item.keyname, request.data.Take(item.keyname.Length).ToArray())) {
                        item.func(request, response);
                        return;
                    }
                }
                if (default_service != null)
                    default_service.func(request, response);
            } catch (Exception) { }
        }

        public void Register(string name, Service.ServiceDelegate func, byte[] key)
        {
            var subset = from s in service_table where s.name.Equals(name) select s;
            if (subset.Count() != 0) return;

            // insert new service to table sorted by length of keyname decended
            Service tmp = null;
            for (int i = 0; i < service_table.Count; i++) {
                if (service_table[i].keyname.Length < key.Length) {
                    tmp = new Service(name, key, func);
                    service_table.Insert(i, tmp);
                    break;
                }
            }
            if (tmp == null)
                service_table.Add(new Service(name, key, func));
        }

        public void RegisterDefaultService(string name, Service.ServiceDelegate func)
        {
            default_service = new Service(name, new byte[] {0}, func);
        }

        public void Deregister(string name)
        {
            foreach (var item in service_table) {
                if (item.name.Equals(name)) {
                    service_table.Remove(item);
                    break;
                }
            }
        }
    }
}
