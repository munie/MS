using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace mnn.module {
    public class ModuleCtl {
        private List<Module> modtab;

        public delegate void ModuleCtlEvent(object sender, Module module);
        public ModuleCtlEvent module_add;
        public ModuleCtlEvent module_delete;

        public delegate void ModuleCallReturn(ModuleCall call);
        public ModuleCallReturn FuncModuleCallReturn;
        private int module_call_count;

        public ModuleCtl()
        {
            modtab = new List<Module>();

            module_add = null;
            module_delete = null;

            FuncModuleCallReturn = null;
            module_call_count = 0;
        }

        // CURD ==========================================================

        public void Exec(int next)
        {
            if (module_call_count == 0) {
                System.Threading.Thread.Sleep(next);
                return;
            }

            lock (modtab) {
                foreach (var item in modtab) {
                    foreach (var call in item.ModuleCallTable.ToArray()) {
                        try {
                            call.retval = item.Invoke(call.iface, call.method, ref call.args);
                            if (FuncModuleCallReturn != null)
                                FuncModuleCallReturn(call);
                        } catch (Exception ex) {
                            log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType)
                                .Error("Exception of invoking assembly method.", ex);
                        } finally {
                            item.ModuleCallTable.Remove(call);
                            module_call_count--;
                        }
                    }
                }
            }
        }

        public Module Add(string filepath, bool loadAfterAdded = false)
        {
            try {
                Module module = new Module(filepath);
                modtab.Add(module);
                if (module_add != null)
                    module_add(this, module);

                if (loadAfterAdded)
                    module.Load();
                return module;
            } catch (Exception ex) {
                log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType)
                    .Error("load " + filepath + " failed", ex);
                return null;
            }
        }

        public void Del(Module module)
        {
            if (module.State == ModuleState.Loaded)
                module.Unload();
            modtab.Remove(module);

            if (module_delete != null)
                module_delete(this, module);
        }

        public Module GetModule(string modname)
        {
            foreach (var item in modtab) {
                if (item.AssemblyName.Equals(modname))
                    return item;
            }

            return null;
        }

        public Module[] GetModules()
        {
            return modtab.ToArray();
        }

        public void AppendModuleCall(Module module, ModuleCall call)
        {
            lock (modtab) {
                module.ModuleCallTable.Add(call);
                module_call_count++;
            }
        }
    }
}
