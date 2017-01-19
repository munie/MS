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

    public enum ModuleState {
        Loaded = 0,
        Unloaded = 1,
    }

    public class Module {
        private AppDomain domain;
        private AppDomainProxy proxy;
        public string AssemblyPath { get; private set; }
        public string AssemblyName { get; private set; }
        public ModuleState State { get; private set; }
        public List<ModuleCall> ModuleCallTable { get; private set; }

        public delegate void ModuleUpdatedEvent(Module module);
        public ModuleUpdatedEvent module_load;
        public ModuleUpdatedEvent module_unload;

        public Module(string filepath)
        {
            AssemblyPath = filepath;
            AssemblyName = Path.GetFileName(filepath);
            State = ModuleState.Unloaded;
            ModuleCallTable = new List<ModuleCall>();
            module_load = null;
            module_unload = null;
        }

        // Methods ========================================================================

        public void Load()
        {
            domain = AppDomain.CreateDomain(AssemblyName);
            proxy = (AppDomainProxy)domain.CreateInstanceFromAndUnwrap(
                Assembly.GetExecutingAssembly().CodeBase, typeof(AppDomainProxy).FullName);

            try {
                // load assembly
                proxy.LoadAssembly(AssemblyPath);

                // check IModule
                if (!CheckInterface(new string[] { typeof(IModule).FullName })) {
                    throw new ApplicationException(String.Format("Can't find {0} in specified assembly {1}",
                        typeof(IModule).FullName, AssemblyPath));
                }

                // invoke Init of IModule
                object[] args = new object[] { };
                Invoke(typeof(IModule).FullName, IModuleSymbols.INIT, ref args);

                // update state as load successed
                State = ModuleState.Loaded;
                if (module_load != null)
                    module_load(this);
            } catch (Exception) {
                AppDomain.Unload(domain);
                throw;
            }
        }

        public void UnLoad()
        {
            try {
                object[] args = new object[] { };
                Invoke(typeof(IModule).FullName, IModuleSymbols.FINAL, ref args);
            } catch (Exception){
                // do nothing
            }  finally {
                AppDomain.Unload(domain);
                State = ModuleState.Unloaded;
                if (module_unload != null)
                    module_unload(this);
            }
        }

        public bool CheckInterface(string[] interfaceNames)
        {
            return proxy.CheckInterface(interfaceNames);
        }

        public object Invoke(string interfaceName, string methodName, ref /*params*/ object[] args)
        {
            return proxy.Invoke(interfaceName, methodName, ref args);
        }

        public object Invoke(string methodName, ref /*params*/ object[] args)
        {
            return proxy.Invoke(methodName, ref args);
        }

        public bool CheckMethod(string methodName, System.Reflection.MethodInfo method)
        {
            return proxy.CheckMethod(methodName, method);
        }

        public string[] GetMethods(System.Reflection.MethodInfo method)
        {
            return proxy.GetSameMethods(method);
        }
    }
}
