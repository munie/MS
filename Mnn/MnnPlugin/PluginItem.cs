using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;

namespace Mnn.MnnPlugin
{
    public class PluginItem
    {
        public Guid AssemblyGuid { get; set; }
        public string AssemblyName { get; set; }
        private string AssemblyPath { get; set; }
        private AppDomain Domain { get; set; }
        private AppDomainProxy Proxy { get; set; }


        public void Load(string filePath)
        {
            AssemblyGuid = Guid.NewGuid();
            AssemblyName = Path.GetFileName(filePath);
            AssemblyPath = filePath;

            Domain = AppDomain.CreateDomain(AssemblyName);
            Proxy = (AppDomainProxy)Domain.CreateInstanceFromAndUnwrap(Assembly.GetExecutingAssembly().CodeBase,
                                                                       typeof(AppDomainProxy).FullName);

            try {
                Proxy.LoadAssembly(filePath);
            }
            catch (Exception) {
                AppDomain.Unload(Domain);
                throw;
            }
        }

        public void UnLoad()
        {
            AppDomain.Unload(Domain);
        }

        public object Invoke(string interfaceName, string methodName, params object[] args)
        {
            return Proxy.Invoke(interfaceName, methodName, args);
        }
    }
}
