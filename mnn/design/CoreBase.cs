using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using mnn.net;
using mnn.misc.service;
using mnn.misc.module;
using Newtonsoft.Json;

namespace mnn.design {
    public class CoreBase {
        // timeout control
        protected TimeOutCtl timectl;
        // module control
        public ModuleCtl modctl;
        // service control
        public ServiceCore servctl;
        // session control
        public SessCtl sessctl;

        public CoreBase()
        {
            // init timectl
            timectl = new TimeOutCtl();

            // init modctl
            modctl = new ModuleCtl();
            modctl.module_add += new ModuleCtl.ModuleCtlEvent(OnModuleCtlAdd);

            // init servctl
            servctl = new ServiceCore();
            servctl.serv_before_do += new ServiceDoBeforeDelegate(OnServBeforeDo);
            servctl.serv_done += new ServiceDoneDelegate(OnServDone);
            servctl.RegisterDefaultService("core.default", DefaultService);
            servctl.RegisterService("core.moduleadd", ModuleAddService);
            servctl.RegisterService("core.moduledel", ModuleDelService);
            servctl.RegisterService("core.moduleload", ModuleLoadService);
            servctl.RegisterService("core.moduleunload", ModuleUnloadService);
            servctl.RegisterService("core.sessopen", SessOpenService);
            servctl.RegisterService("core.sessclose", SessCloseService);
            servctl.RegisterService("core.sesssend", SessSendService);

            // init sessctl
            sessctl = new SessCtl();
            sessctl.sess_parse += new SessCtl.SessDelegate(OnSessParse);
            sessctl.sess_create += new SessCtl.SessDelegate(OnSessCreate);
            sessctl.sess_delete += new SessCtl.SessDelegate(OnSessDelete);
        }

        public virtual void Run()
        {
            System.Threading.Thread thread = new System.Threading.Thread(() =>
            {
                RunForever();
            });
            thread.IsBackground = true;
            thread.Start();
        }

        public virtual void RunForever()
        {
            while (true) {
                try {
                    timectl.Exec();
                    sessctl.Exec(1000);
                    servctl.Exec();
                } catch (Exception ex) {
                    log4net.ILog log = log4net.LogManager.GetLogger(typeof(CoreBase));
                    log.Error("Exception thrown out by core thread.", ex);
                }
            }
        }

        // Module Event ==================================================================================

        private void OnModuleCtlAdd(object sender, Module module)
        {
            module.module_load += new Module.ModuleEvent(OnModuleLoad);
            module.module_unload += new Module.ModuleEvent(OnModuleUnload);
        }

        private void OnModuleLoad(Module module)
        {
            // get services and filters
            object[] nil_args = new object[0];
            object servtab = module.Invoke(typeof(IModuleService).FullName, IModuleServiceSymbols.GET_SERVICE_TABLE, ref nil_args);
            object filttab = module.Invoke(typeof(IModuleService).FullName, IModuleServiceSymbols.GET_FILTER_TABLE, ref nil_args);

            // register services
            foreach (var item in servtab as IDictionary<string, string>) {
                if (!module.CheckMethod(item.Value, typeof(ServiceDelegate).GetMethod("Invoke"))) {
                    log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CoreBase));
                    logger.Warn(String.Format("can't found {0} in {1}", item.Value, module.ToString()));
                    continue;
                }

                var service = item; // I dislike closure here
                servctl.RegisterService(service.Key,
                    (ServiceRequest request, ref ServiceResponse response) => {
                        object swap = request.user_data;
                        request.user_data = null;

                        object[] args = new object[] { request, response };
                        module.Invoke(service.Value, ref args);
                        response.raw_data = (args[1] as ServiceResponse).raw_data;

                        request.user_data = swap;

                        // log
                        string logmsg = DateTime.Now + " (" + (request.user_data as SockSess).rep.ToString()
                            + " => " + (request.user_data as SockSess).lep.ToString() + ")" + Environment.NewLine;
                        logmsg += Encoding.UTF8.GetString(request.raw_data);
                        log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CoreBase));
                        logger.Info(logmsg);
                    });
            }

            // register filters
            foreach (var item in filttab as IDictionary<string, string>) {
                if (!module.CheckMethod(item.Value, typeof(FilterDelegate).GetMethod("Invoke"))) {
                    log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CoreBase));
                    logger.Warn(String.Format("can't found {0} in {1}", item.Value, module.ToString()));
                    continue;
                }

                var filter = item; // I dislike closure here
                servctl.RegisterFilter(filter.Key,
                    (ref ServiceRequest request) => {
                        object swap = request.user_data;
                        request.user_data = null;

                        object[] args = new object[] { request };
                        bool retval = (bool)module.Invoke(filter.Value, ref args);
                        request.raw_data = (args[0] as ServiceRequest).raw_data;

                        request.user_data = swap;
                        return retval;
                    });
            }
        }

        private void OnModuleUnload(Module module)
        {
            // get services and filters
            object[] nil_args = new object[0];
            object servtab = module.Invoke(typeof(IModuleService).FullName, IModuleServiceSymbols.GET_SERVICE_TABLE, ref nil_args);
            object filttab = module.Invoke(typeof(IModuleService).FullName, IModuleServiceSymbols.GET_FILTER_TABLE, ref nil_args);

            // deregister service
            foreach (var item in servtab as IDictionary<string, string>)
                servctl.DeregisterService(item.Key);

            // deregister filters
            foreach (var item in filttab as IDictionary<string, string>)
                servctl.DeregisterFilter(item.Key);
        }

        // Service Event ==================================================================================

        protected virtual void OnServBeforeDo(ref ServiceRequest request)
        {
            if (request.content_mode != ServiceRequestContentMode.unknown) {
                try {
                    byte[] result = Convert.FromBase64String(Encoding.UTF8.GetString(request.raw_data));
                    result = EncryptSym.AESDecrypt(result);
                    if (result != null)
                        request.raw_data = result;
                } catch (Exception) { }
            }
        }

        protected virtual void OnServDone(ServiceRequest request, ServiceResponse response)
        {
            if (response.raw_data != null && response.raw_data.Length != 0) {
                sessctl.BeginInvoke(new Action(() =>
                {
                    SockSess result = sessctl.FindSession(SockType.accept, null, (request.user_data as SockSess).rep);
                    if (result != null)
                        sessctl.SendSession(result, response.raw_data);
                }));
            }
        }

        // Session Event ==================================================================================

        protected virtual void OnSessParse(object sender, SockSess sess)
        {
            // init request & response
            ServiceRequest request = ServiceRequest.Parse(sess.RfifoTake());
            request.user_data = sess;

            // rfifo skip
            sess.RfifoSkip(request.packlen);

            // add request to service core
            servctl.AddRequest(request);
        }

        protected virtual void OnSessCreate(object sender, SockSess sess) { }

        protected virtual void OnSessDelete(object sender, SockSess sess) { }

        // Center Service =========================================================================

        protected virtual void DefaultService(ServiceRequest request, ref ServiceResponse response)
        {
            // write response
            response.content = new BaseContent() {
                id = "unknown",
                errcode = 10024,
                errmsg = "unknown request",
            };
            response.raw_data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response.content));
        }

        protected virtual void ModuleAddService(ServiceRequest request, ref ServiceResponse response)
        {
            // parse to dictionary
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>(Encoding.UTF8.GetString(request.raw_data));

            Module module = modctl.Add(dc["filepath"]);

            // write response
            response.content = new BaseContent() { id = dc["id"] };
            if (module != null) {
                response.content.errcode = 0;
                response.content.errmsg = dc["filepath"] + " added";
            } else {
                response.content.errcode = 1;
                response.content.errmsg = "cannot find " + dc["filepath"];
            }
            response.raw_data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response.content));
        }

        protected virtual void ModuleDelService(ServiceRequest request, ref ServiceResponse response)
        {
            // parse to dictionary
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>(Encoding.UTF8.GetString(request.raw_data));

            modctl.Del(dc["modname"]);

            // write response
            response.content = new BaseContent() { id = dc["id"] };
            response.content.errcode = 0;
            response.content.errmsg = dc["modname"] + " deleted";
            response.raw_data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response.content));
        }

        protected virtual void ModuleLoadService(ServiceRequest request, ref ServiceResponse response)
        {
            // parse to dictionary
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>(Encoding.UTF8.GetString(request.raw_data));

            Module module = modctl.GetModule(dc["modname"]);
            bool loadstat = true;
            try {
                module.Load();
                loadstat = true;
            } catch (Exception) {
                loadstat = false;
            }

            // write response
            response.content = new BaseContent() { id = dc["id"] };
            if (module != null) {
                if (loadstat) {
                    response.content.errcode = 0;
                    response.content.errmsg = dc["modname"] + " loaded";
                } else {
                    response.content.errcode = 2;
                    response.content.errmsg = "failed to load " + dc["modname"];
                }
            } else {
                response.content.errcode = 1;
                response.content.errmsg = "cannot find " + dc["modname"];
            }
            response.raw_data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response.content));
        }

        protected virtual void ModuleUnloadService(ServiceRequest request, ref ServiceResponse response)
        {
            // parse to dictionary
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>(Encoding.UTF8.GetString(request.raw_data));

            Module module = modctl.GetModule(dc["modname"]);
            module.Unload();

            // write response
            response.content = new BaseContent() { id = dc["id"] };
            if (module != null) {
                response.content.errcode = 0;
                response.content.errmsg = dc["modname"] + " loaded";
            } else {
                response.content.errcode = 1;
                response.content.errmsg = "cannot find " + dc["modname"];
            }
            response.raw_data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response.content));
        }

        protected virtual void SessOpenService(ServiceRequest request, ref ServiceResponse response)
        {
            // parse to dictionary
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>(Encoding.UTF8.GetString(request.raw_data));

            // find session and open
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), Convert.ToInt32(dc["port"]));
            SockSess result = null;
            if (dc["type"] == SockType.listen.ToString() && sessctl.FindSession(SockType.listen, ep, null) == null)
                result = sessctl.MakeListen(ep);
            else if (dc["type"] == SockType.connect.ToString())
                result = sessctl.AddConnect(ep);
            else
                result = null;

            // write response
            response.content = new BaseContent() { id = dc["id"] };
            if (result != null) {
                response.content.errcode = 0;
                response.content.errmsg = dc["type"] + " " + ep.ToString();
            } else {
                response.content.errcode = 1;
                response.content.errmsg = "cannot find " + ep.ToString();
            }
            response.raw_data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response.content));
        }

        protected virtual void SessCloseService(ServiceRequest request, ref ServiceResponse response)
        {
            // parse to dictionary
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>(Encoding.UTF8.GetString(request.raw_data));

            // find session
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), Convert.ToInt32(dc["port"]));
            SockSess result = null;
            if (dc["type"] == SockType.listen.ToString())
                result = sessctl.FindSession(SockType.listen, ep, null);
            else if (dc["type"] == SockType.connect.ToString())
                result = sessctl.FindSession(SockType.connect, ep, null);
            else// if (dc["type"] == SockType.accept.ToString())
                result = sessctl.FindSession(SockType.accept, null, ep);

            // close session
            if (result != null)
                sessctl.DelSession(result);

            // write response
            response.content = new BaseContent() { id = dc["id"] };
            if (result != null) {
                response.content.errcode = 0;
                response.content.errmsg = "shutdown " + ep.ToString();
            } else {
                response.content.errcode = 1;
                response.content.errmsg = "cannot find " + ep.ToString();
            }
            response.raw_data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response.content));
        }

        protected virtual void SessSendService(ServiceRequest request, ref ServiceResponse response)
        {
            // parse to dictionary
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>(Encoding.UTF8.GetString(request.raw_data));

            // find session
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), Convert.ToInt32(dc["port"]));
            SockSess result = null;
            if (dc["type"] == SockType.listen.ToString())
                result = sessctl.FindSession(SockType.listen, ep, null);
            else if (dc["type"] == SockType.connect.ToString())
                result = sessctl.FindSession(SockType.connect, ep, null);
            else// if (dc["type"] == SockType.accept.ToString())
                result = sessctl.FindSession(SockType.accept, null, ep);

            // send message
            if (result != null)
                sessctl.SendSession(result, Encoding.UTF8.GetBytes(dc["data"]));

            // write response
            response.content = new BaseContent() { id = dc["id"] };
            if (result != null) {
                response.content.errcode = 0;
                response.content.errmsg = "send to " + ep.ToString();
            } else {
                response.content.errcode = 1;
                response.content.errmsg = "cannot find " + ep.ToString();
            }
            response.raw_data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response.content));
        }
    }
}
