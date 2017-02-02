using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using mnn.module;
using mnn.service;
using mnn.net;
using mnn.misc.glue;
using mnn.misc.env;
using Newtonsoft.Json;

namespace mnn.misc.glue {
    public class ModulizedServiceLayer : ServiceLayer {
        public ModuleCtl modctl;

        public ModulizedServiceLayer()
        {
            modctl = new ModuleCtl();
            modctl.module_add += new ModuleCtl.ModuleCtlEvent(OnModuleCtlAdd);
            modctl.module_delete += new ModuleCtl.ModuleCtlEvent(OnModuleCtlDelete);

            servctl.RegisterService("service.moduleadd", ModuleAddService);
            servctl.RegisterService("service.moduledel", ModuleDelService);
            servctl.RegisterService("service.moduleload", ModuleLoadService);
            servctl.RegisterService("service.moduleunload", ModuleUnloadService);
            servctl.RegisterService("service.moduledetail", ModuleDetailService);
        }

        // Module Event ============================================================================

        protected virtual void OnModuleCtlAdd(object sender, Module module)
        {
            module.module_load += new Module.ModuleEvent(OnModuleLoad);
            module.module_unload += new Module.ModuleEvent(OnModuleUnload);

            module.Load();
        }

        protected virtual void OnModuleCtlDelete(object sender, Module module) { }

        protected virtual void OnModuleLoad(Module module)
        {
            if (module.CheckInterface(new string[] { typeof(IModuleService).FullName })) {
                object[] nil_args = new object[0];
                object servtab = module.Invoke(typeof(IModuleService).FullName, IModuleServiceSymbols.GET_SERVICE_TABLE, ref nil_args);

                // register services
                foreach (var item in servtab as IDictionary<string, string>) {
                    if (!module.CheckMethod(item.Value, typeof(Service.ServiceHandlerDelegate).GetMethod("Invoke"))) {
                        log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType)
                            .Warn(String.Format("can't found {0} in {1}", item.Value, module.ToString()));
                        continue;
                    }

                    var service = item; // I dislike closure here
                    servctl.RegisterService(service.Key,
                        (ServiceRequest request, ref ServiceResponse response) => {
                            object[] args = new object[] { request, response };
                            module.Invoke(service.Value, ref args);
                            response = args[1] as ServiceResponse;
                        });
                }
            }

            if (module.CheckInterface(new string[] { typeof(IModuleFilter).FullName })) {
                object[] nil_args = new object[0];
                object filttab = module.Invoke(typeof(IModuleFilter).FullName, IModuleFilterSymbols.GET_FILTER_TABLE, ref nil_args);

                // register filters
                foreach (var item in filttab as IDictionary<string, string>) {
                    if (!module.CheckMethod(item.Value, typeof(Service.ServiceHandlerDelegate).GetMethod("Invoke"))) {
                        log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType)
                            .Warn(String.Format("can't found {0} in {1}", item.Value, module.ToString()));
                        continue;
                    }

                    var filter = item; // I dislike closure here
                    filtctl.RegisterFilter(filter.Key,
                        (ServiceRequest request, ref ServiceResponse response) => {
                            object[] args = new object[] { request, response };
                            bool retval = (bool)module.Invoke(filter.Value, ref args);
                            response = args[1] as ServiceResponse;
                        });
                }
            }
        }

        protected virtual void OnModuleUnload(Module module)
        {
            if (module.CheckInterface(new string[] { typeof(IModuleService).FullName })) {
                object[] nil_args = new object[0];
                object servtab = module.Invoke(typeof(IModuleService).FullName, IModuleServiceSymbols.GET_SERVICE_TABLE, ref nil_args);

                // unregister service
                foreach (var item in servtab as IDictionary<string, string>)
                    servctl.UnregisterService(item.Key);
            }

            if (module.CheckInterface(new string[] { typeof(IModuleFilter).FullName })) {
                object[] nil_args = new object[0];
                object filttab = module.Invoke(typeof(IModuleFilter).FullName, IModuleFilterSymbols.GET_FILTER_TABLE, ref nil_args);

                // unregister filter
                foreach (var item in filttab as IDictionary<string, string>)
                    filtctl.UnregisterFilter(item.Key);
            }
        }

        // Module Service ==========================================================================

        protected virtual void ModuleAddService(ServiceRequest request, ref ServiceResponse response)
        {
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>((string)request.data);

            Module module = modctl.Add(dc["filepath"]);

            if (module != null) {
                response.errcode = 0;
                response.errmsg = dc["filepath"] + " added";
            } else {
                response.errcode = 1;
                response.errmsg = "cannot find " + dc["filepath"];
            }
        }

        protected virtual void ModuleDelService(ServiceRequest request, ref ServiceResponse response)
        {
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>((string)request.data);

            Module module = modctl.GetModule(dc["name"]);
            if (module != null)
                modctl.Del(module);

            response.errcode = 0;
            response.errmsg = dc["name"] + " deleted";
        }

        protected virtual void ModuleLoadService(ServiceRequest request, ref ServiceResponse response)
        {
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>((string)request.data);

            Module module = modctl.GetModule(dc["name"]);
            bool loadstat = true;
            try {
                module.Load();
                loadstat = true;
            } catch (Exception) {
                loadstat = false;
            }

            if (module != null) {
                if (loadstat) {
                    response.errcode = 0;
                    response.errmsg = dc["name"] + " loaded";
                } else {
                    response.errcode = 2;
                    response.errmsg = "failed to load " + dc["name"];
                }
            } else {
                response.errcode = 1;
                response.errmsg = "cannot find " + dc["name"];
            }
        }

        protected virtual void ModuleUnloadService(ServiceRequest request, ref ServiceResponse response)
        {
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>((string)request.data);

            Module module = modctl.GetModule(dc["name"]);
            module.Unload();

            if (module != null) {
                response.errcode = 0;
                response.errmsg = dc["name"] + " loaded";
            } else {
                response.errcode = 1;
                response.errmsg = "cannot find " + dc["name"];
            }
        }

        protected virtual void ModuleDetailService(ServiceRequest request, ref ServiceResponse response)
        {
            List<object> pack = new List<object>();
            foreach (var item in modctl.GetModules()) {
                pack.Add(new {
                    name = item.AssemblyName,
                    version = item.Version,
                    state = item.State.ToString(),
                });
            }

            response.data = pack;
        }
    }
}
