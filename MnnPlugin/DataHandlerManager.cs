using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace MnnPlugin
{
    public partial class DataHandlerManager
    {
        class DataHandleState
        {
            public string AssemblyName { get; set; }
            public int Port { get; set; }
            public AppDomain Domain { get; set; }
            public AppDomainProxy Proxy { get; set; }
        }
    }

    public partial class DataHandlerManager
    {
        private List<DataHandleState> dataHandleTable = new List<DataHandleState>();

        public void LoadPlugin(string assemblyName)
        {
            AppDomain ad = AppDomain.CreateDomain(assemblyName);
            AppDomainProxy proxy = (AppDomainProxy)ad.CreateInstanceFromAndUnwrap(
                @"MnnPlugin.dll",
                typeof(AppDomainProxy).FullName);

            proxy.LoadAssembly(assemblyName);

            lock (dataHandleTable) {
                dataHandleTable.Add(new DataHandleState()
                {
                    AssemblyName = assemblyName,
                    Domain = ad,
                    Proxy = proxy,
                    Port = (int)proxy.Invoke("IDataHandle", "GetIdentity", null),
                });
            }
        }

        public void UnLoadPlugin(string assemblyName)
        {
            var subset = from s in dataHandleTable where s.AssemblyName.Equals(assemblyName) select s;

            if (subset.Count() == 0)
                throw new ApplicationException("Specified Plugin hasn't loaded."); 

            AppDomain domain = subset.First().Domain;

            lock (dataHandleTable) {
                dataHandleTable.Remove(subset.First());
            }
            AppDomain.Unload(domain);
        }

        public void UnLoadPlugin(int port)
        {
            var subset = from s in dataHandleTable where s.Port == port select s;

            if (subset.Count() == 0)
                throw new ApplicationException("Specified Plugin hasn't loaded.");

            AppDomain domain = subset.First().Domain;

            lock (dataHandleTable) {
                dataHandleTable.Remove(subset.First());
            }
            AppDomain.Unload(domain);
        }

        public object Invoke(int port, string interfaceName, string methodName, params object[] args)
        {
            object retValue = null;

            lock (dataHandleTable) {
                var subset = from s in dataHandleTable where s.Port.Equals(port) select s;
                if (subset.Count() != 0) {
                    retValue = subset.First().Proxy.Invoke(interfaceName, methodName, args);
                }
            }

            return retValue;
        }

        public Dictionary<int, string> GetDataHandleStatus()
        {
            Dictionary<int, string> dc = new Dictionary<int, string>();

            foreach (var item in dataHandleTable) {
                dc.Add(item.Port, item.AssemblyName);
            }

            return dc;
        }
    }
}
