using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace mnn.misc.module
{
    public class ModuleCenter
    {
        private List<ModuleNode> module_table;
        private int unhandled_message_count;
        public delegate void ModuleCallReturn(ModuleCall call);
        public ModuleCallReturn FuncModuleCallReturn;

        public ModuleCenter()
        {
            module_table = new List<ModuleNode>();
            unhandled_message_count = 0;
        }

        // Methods ==========================================================

        public void Perform()
        {
            if (unhandled_message_count == 0) return;

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
                            unhandled_message_count--;
                        }
                    }
                }
            }
        }

        public ModuleNode AddModule(string filePath)
        {
            ModuleNode module = new ModuleNode();

            try {
                module.Load(filePath);
            } catch (Exception) {
                return null;
            }

            if (!module.CheckInterface(new string[] { SModule.FullName })) {
                module.UnLoad();
                return null;
            }

            try {
                module.Invoke(SModule.FullName, SModule.Init, null);
                module.ModuleID = (string)module.Invoke(SModule.FullName, SModule.GetModuleID, null);
            } catch (Exception) {
                module.UnLoad();
                return null;
            }

            lock (module_table) {
                module_table.Add(module);
            }

            return module;
        }

        public void DelModule(ModuleNode module)
        {
            try {
                module.Invoke(SModule.FullName, SModule.Final, null);
            } catch (Exception) { }
            // 卸载模块
            module.UnLoad();
            module_table.Remove(module);
        }

        public void AppendModuleCall(ModuleNode module, ModuleCall call)
        {
            module.ModuleCallTable.Add(call);
            unhandled_message_count++;
        }
    }
}
