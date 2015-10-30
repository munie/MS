using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;

namespace mnn.misc.module {
    public class ModuleCall {
        public string iface;
        public string method;
        public object[] args;
        public object retval;
    }

    public class ModuleNode {
        private AppDomain domain;
        private AppDomainProxy proxy;
        private string AssemblyPath;
        public string AssemblyName { get; private set; }
        public string ModuleID { get; set; }
        public List<ModuleCall> ModuleCallTable { get; set; }

        public ModuleNode()
        {
            ModuleCallTable = new List<ModuleCall>();
        }

        // Methods ========================================================================

        public void Load(string filePath)
        {
            AssemblyPath = filePath;
            AssemblyName = Path.GetFileName(filePath);

            domain = AppDomain.CreateDomain(AssemblyName);
            proxy = (AppDomainProxy)domain.CreateInstanceFromAndUnwrap(
                Assembly.GetExecutingAssembly().CodeBase, typeof(AppDomainProxy).FullName);

            try {
                proxy.LoadAssembly(filePath);
            } catch (Exception) {
                AppDomain.Unload(domain);
                throw;
            }
        }

        public void UnLoad()
        {
            AppDomain.Unload(domain);
        }

        public bool CheckInterface(string[] interfaceNames)
        {
            return proxy.CheckInterface(interfaceNames);
        }

        public object Invoke(string interfaceName, string methodName, params object[] args)
        {
            return proxy.Invoke(interfaceName, methodName, args);
        }
    }
}
