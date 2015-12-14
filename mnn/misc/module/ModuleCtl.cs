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

        public void Perform(int next)
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
                            log.Error("Exception of invoding assembly method.", ex);
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

            try {
                module.Load(filePath);
            } catch (Exception ex) {
                log4net.ILog log = log4net.LogManager.GetLogger(typeof(ModuleCtl));
                log.Error(String.Format("Exception of loading assembly {0}.", filePath), ex);
                return null;
            }

            if (!module.CheckInterface(new string[] { typeof(IModule).FullName })) {
                module.UnLoad();
                return null;
            }

            try {
                object[] tmp = new object[] {};
                module.Invoke(typeof(IModule).FullName, SModule.INIT, ref tmp);
                module.ModuleID = (string)module.Invoke(typeof(IModule).FullName, SModule.GET_MODULE_ID, ref tmp);
            } catch (Exception ex) {
                log4net.ILog log = log4net.LogManager.GetLogger(typeof(ModuleCtl));
                log.Error("Exception of invoking module init method.", ex);
                module.UnLoad();
                return null;
            }

            module_table.Add(module);
            return module;
        }

        public void Del(ModuleNode module)
        {
            try {
                object[] tmp = new object[] { };
                module.Invoke(typeof(IModule).FullName, SModule.FINAL, ref tmp);
            } catch (Exception ex) {
                log4net.ILog log = log4net.LogManager.GetLogger(typeof(ModuleCtl));
                log.Error("Exception of invoking module final method.", ex);
            }
            // 卸载模块
            module.UnLoad();
            module_table.Remove(module);
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
