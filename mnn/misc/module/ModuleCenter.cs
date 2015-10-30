using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace mnn.misc.module
{
    public class ModuleCenter
    {
        private List<ModuleNode> module_table;
        public delegate void ModuleCallReturn(ModuleCall call);
        public ModuleCallReturn FuncModuleCallReturn;
        private int module_call_count;

        public ModuleCenter()
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
                            call.retval = item.Invoke(call.iface, call.method, call.args);
                            if (FuncModuleCallReturn != null)
                                FuncModuleCallReturn(call);
                        } catch (Exception) {
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
            } catch (Exception) {
                return null;
            }

            if (!module.CheckInterface(new string[] { SModule.FULL_NAME })) {
                module.UnLoad();
                return null;
            }

            try {
                module.Invoke(SModule.FULL_NAME, SModule.INIT, null);
                module.ModuleID = (string)module.Invoke(SModule.FULL_NAME, SModule.GET_MODULE_ID, null);
            } catch (Exception) {
                module.UnLoad();
                return null;
            }

            module_table.Add(module);
            return module;
        }

        public void Del(ModuleNode module)
        {
            try {
                module.Invoke(SModule.FULL_NAME, SModule.FINAL, null);
            } catch (Exception) { }
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
