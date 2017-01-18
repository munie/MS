using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace mnn.misc.module {
    public class ModuleCtl {
        private List<ModuleNode> module_table;
        public delegate void ModuleCallReturn(ModuleCall call);
        public ModuleCallReturn FuncModuleCallReturn;
        private int module_call_count;

        public ModuleCtl()
        {
            module_table = new List<ModuleNode>();
            FuncModuleCallReturn = null;
            module_call_count = 0;
        }

        // Methods ==========================================================

        public void Exec(int next)
        {
            if (module_call_count == 0) {
                System.Threading.Thread.Sleep(next);
                return;
            }

            lock (module_table) {
                foreach (var item in module_table) {
                    foreach (var call in item.ModuleCallTable.ToArray()) {
                        try {
                            call.retval = item.Invoke(call.iface, call.method, ref call.args);
                            if (FuncModuleCallReturn != null)
                                FuncModuleCallReturn(call);
                        } catch (Exception ex) {
                            log4net.ILog log = log4net.LogManager.GetLogger(typeof(ModuleCtl));
                            log.Error("Exception of invoking assembly method.", ex);
                        } finally {
                            item.ModuleCallTable.Remove(call);
                            module_call_count--;
                        }
                    }
                }
            }
        }

        public ModuleNode Add(string filePath)
        {
            ModuleNode module = new ModuleNode();
            module.Load(filePath);

            if (!module.CheckInterface(new string[] { typeof(IModule).FullName })) {
                module.UnLoad();
                throw new ApplicationException(
                    String.Format("Can't find {0} in specified assembly {1}", typeof(IModule).FullName, filePath));
            }

            try {
                object[] tmp = new object[] {};
                module.Invoke(typeof(IModule).FullName, IModuleSymbols.INIT, ref tmp);
            } catch (Exception) {
                module.UnLoad();
                throw;
            }

            module_table.Add(module);
            return module;
        }

        public void Del(ModuleNode module)
        {
            try {
                object[] tmp = new object[] { };
                module.Invoke(typeof(IModule).FullName, IModuleSymbols.FINAL, ref tmp);
            } catch (Exception) {
                //log4net.ILog log = log4net.LogManager.GetLogger(typeof(ModuleCtl));
                //log.Error("Exception of invoking module final method.", ex);
            } finally {
                module.UnLoad();
                module_table.Remove(module);
            }
        }

        public ModuleNode FindModule(string filePath)
        {
            var subset = from s in module_table where s.AssemblyName.Equals(filePath) select s;
            if (subset.Any())
                return subset.First();
            else
                return null;
        }

        public void AppendModuleCall(ModuleNode module, ModuleCall call)
        {
            lock (module_table) {
                module.ModuleCallTable.Add(call);
                module_call_count++;
            }
        }
    }
}
