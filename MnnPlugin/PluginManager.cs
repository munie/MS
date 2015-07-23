using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;

namespace MnnPlugin
{
    public partial class PluginManager
    {
        class PluginState
        {
            public string AssemblyName { get; set; }
            public string AssemblyPath { get; set; }
            public AppDomain Domain { get; set; }
            public AppDomainProxy Proxy { get; set; }
        }
    }

    public partial class PluginManager
    {
        private List<PluginState> pluginTable = new List<PluginState>();

        public string LoadPlugin(string filePath)
        {
            string fileName = Path.GetFileName(filePath);

            lock (pluginTable) {
                foreach (var item in pluginTable) {
                    if (item.AssemblyName.Equals(fileName))
                        throw new ApplicationException("Specified plugin is already loaded.");
                }
            }

            AppDomain ad = AppDomain.CreateDomain(fileName);
            AppDomainProxy proxy = (AppDomainProxy)ad.CreateInstanceFromAndUnwrap(
                Assembly.GetExecutingAssembly().CodeBase,
                typeof(AppDomainProxy).FullName);

            try {
                proxy.LoadAssembly(filePath);
            }
            catch (Exception) {
                AppDomain.Unload(ad);
                throw;
            }

            lock (pluginTable) {
                pluginTable.Add(new PluginState()
                {
                    AssemblyName = fileName,
                    AssemblyPath = filePath,
                    Domain = ad,
                    Proxy = proxy
                });
            }

            return fileName;
        }

        public void UnLoadPlugin(string assemblyName)
        {
            lock (pluginTable) {
                var subset = from s in pluginTable where s.AssemblyName.Equals(assemblyName) select s;

                if (subset.Count() == 0)
                    throw new ApplicationException("Specified Plugin hasn't loaded.");

                AppDomain domain = subset.First().Domain;

                pluginTable.Remove(subset.First());

                AppDomain.Unload(domain);
            }
            /*
            lock(obj) {
                    //执行代码

            }

            在编译器进行编译时，会自动生成并插入以下代码：

            Monitor.Enter(obj);
            try {
                    //执行代码
            }
            finally {
                    Monitor.Exit(obj);
            }
             */
        }

        public object Invoke(string assemblyName, string interfaceName, string methodName, params object[] args)
        {
            object retValue = null;

            lock (pluginTable) {
                var subset = from s in pluginTable where s.AssemblyName.Equals(assemblyName) select s;
                if (subset.Count() != 0) {
                    retValue = subset.First().Proxy.Invoke(interfaceName, methodName, args);
                }
            }

            return retValue;
        }

        public Dictionary<string, string> GetPluginStatus()
        {
            Dictionary<string, string> dc = new Dictionary<string, string>();

            lock (pluginTable) {
                foreach (var item in pluginTable)
                    dc.Add(item.AssemblyName, item.AssemblyPath);
            }

            return dc;
        }
    }
}
