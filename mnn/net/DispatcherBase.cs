using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace mnn.net {
    public class Service {
        public delegate void ServiceDelegate(SockRequest request, SockResponse response);

        public byte[] keyname;
        public ServiceDelegate func;

        public Service(byte[] key, ServiceDelegate func)
        {
            this.keyname = key;
            this.func = func;
        }
    }

    public class DispatcherBase {
        protected Dictionary<string, Service> service_table;
        protected Service default_service;

        public DispatcherBase()
        {
            service_table = new Dictionary<string, Service>();
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
                if (request.type == SockRequestType.unknown && default_service != null) {
                    default_service.func(request, response);
                    return;
                }
                foreach (var item in service_table) {
                    if (byteArrayCmp(item.Value.keyname, request.data.Take(item.Value.keyname.Length).ToArray())) {
                        item.Value.func(request, response);
                        return;
                    }
                }
                if (default_service != null)
                    default_service.func(request, response);
            } catch (Exception) { }
        }

        public void Register(string name, Service.ServiceDelegate func, byte[] key)
        {
            if (!service_table.ContainsKey(name))
                service_table.Add(name, new Service(key, func));
        }

        public void RegisterDefaultService(string name, Service.ServiceDelegate func)
        {
            default_service = new Service(new byte[] {0}, func);
        }

        public void Deregister(string name)
        {
            if (service_table.ContainsKey(name))
                service_table.Remove(name);
        }
    }
}
