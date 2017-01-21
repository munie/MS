using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using mnn.net;
using mnn.service;
using mnn.module;
using Newtonsoft.Json;

namespace mnn.misc.glue {
    public class CoreBase {
        // timeout control
        protected TimeOutCtl timectl;
        // session control
        public SessCtl sessctl;
        // module control
        public ModuleCtl modctl;
        // service control
        public ServiceCore filtctl;
        public ServiceCore servctl;

        public CoreBase()
        {
            // init timectl
            timectl = new TimeOutCtl();

            // init sessctl
            sessctl = new SessCtl();
            sessctl.sess_parse += new SessCtl.SessDelegate(OnSessParse);
            sessctl.sess_create += new SessCtl.SessDelegate(OnSessCreate);
            sessctl.sess_delete += new SessCtl.SessDelegate(OnSessDelete);

            // init modctl
            modctl = new ModuleCtl();
            modctl.module_add += new ModuleCtl.ModuleCtlEvent(OnModuleCtlAdd);
            modctl.module_delete += new ModuleCtl.ModuleCtlEvent(OnModuleCtlDelete);

            // init filtctl
            filtctl = new ServiceCore();
            filtctl.RegisterDefaultService("filter.default", DefaultFilter);

            // init servctl
            servctl = new ServiceCore();
            servctl.serv_before_do += new ServiceDoBeforeDelegate(OnServBeforeDo);
            servctl.serv_done += new ServiceDoneDelegate(OnServDone);
            servctl.RegisterDefaultService("service.default", DefaultService);
            servctl.RegisterService("service.moduleadd", ModuleAddService);
            servctl.RegisterService("service.moduledel", ModuleDelService);
            servctl.RegisterService("service.moduleload", ModuleLoadService);
            servctl.RegisterService("service.moduleunload", ModuleUnloadService);
            servctl.RegisterService("service.moduledetail", ModuleDetailService);
            servctl.RegisterService("service.sessopen", SessOpenService);
            servctl.RegisterService("service.sessclose", SessCloseService);
            servctl.RegisterService("service.sesssend", SessSendService);
            servctl.RegisterService("service.sessdetail", SessDetailService);
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
                    filtctl.Exec();
                    servctl.Exec();
                } catch (Exception ex) {
                    log4net.ILog log = log4net.LogManager.GetLogger(typeof(CoreBase));
                    log.Error("Exception thrown out by core thread.", ex);
                }
            }
        }

        // Module Event ==================================================================================

        protected virtual void OnModuleCtlAdd(object sender, Module module)
        {
            module.module_load += new Module.ModuleEvent(OnModuleLoad);
            module.module_unload += new Module.ModuleEvent(OnModuleUnload);
        }

        protected virtual void OnModuleCtlDelete(object sender, Module module) { }

        protected virtual void OnModuleLoad(Module module)
        {
            // get services and filters
            object[] nil_args = new object[0];
            object filttab = module.Invoke(typeof(IModuleService).FullName, IModuleServiceSymbols.GET_FILTER_TABLE, ref nil_args);
            object servtab = module.Invoke(typeof(IModuleService).FullName, IModuleServiceSymbols.GET_SERVICE_TABLE, ref nil_args);

            // register filters
            foreach (var item in filttab as IDictionary<string, string>) {
                if (!module.CheckMethod(item.Value, typeof(ServiceDelegate).GetMethod("Invoke"))) {
                    log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CoreBase));
                    logger.Warn(String.Format("can't found {0} in {1}", item.Value, module.ToString()));
                    continue;
                }

                var filter = item; // I dislike closure here
                filtctl.RegisterService(filter.Key,
                    (ServiceRequest request, ref ServiceResponse response) => {
                        // backup user_data as it may not serializable
                        object swap = request.user_data;
                        request.user_data = null;

                        object[] args = new object[] { request, response };
                        bool retval = (bool)module.Invoke(filter.Value, ref args);

                        ServiceRequest newrep = response.data as ServiceRequest;
                        if (newrep != null) {
                            // recover user_data
                            newrep.user_data = swap;
                            servctl.AddRequest(newrep);
                        }
                    });
            }

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
                        // backup user_data as it may not serializable
                        object swap = request.user_data;
                        request.user_data = null;

                        object[] args = new object[] { request, response };
                        module.Invoke(service.Value, ref args);
                        response = args[1] as ServiceResponse;

                        // recover user_data
                        request.user_data = swap;

                        string logmsg = DateTime.Now + " (" + (request.user_data as SockSess).rep.ToString()
                            + " => " + (request.user_data as SockSess).lep.ToString() + ")" + Environment.NewLine;
                        logmsg += Encoding.UTF8.GetString(request.raw_data);
                        log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CoreBase));
                        logger.Info(logmsg);
                    });
            }
        }

        protected virtual void OnModuleUnload(Module module)
        {
            // get services and filters
            object[] nil_args = new object[0];
            object filttab = module.Invoke(typeof(IModuleService).FullName, IModuleServiceSymbols.GET_FILTER_TABLE, ref nil_args);
            object servtab = module.Invoke(typeof(IModuleService).FullName, IModuleServiceSymbols.GET_SERVICE_TABLE, ref nil_args);

            // deregister filter
            foreach (var item in filttab as IDictionary<string, string>)
                filtctl.DeregisterService(item.Key);

            // deregister service
            foreach (var item in servtab as IDictionary<string, string>)
                servctl.DeregisterService(item.Key);
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
            sessctl.BeginInvoke(new Action(() =>
            {
                SockSess sess = request.user_data as SockSess;
                if (sess != null)
                    sessctl.SendSession(sess, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response)));
            }));
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
            filtctl.AddRequest(request);
        }

        protected virtual void OnSessCreate(object sender, SockSess sess) { }

        protected virtual void OnSessDelete(object sender, SockSess sess) { }

        // Service =========================================================================

        protected virtual void DefaultFilter(ServiceRequest request, ref ServiceResponse response)
        {
            servctl.AddRequest(request);
        }

        protected virtual void DefaultService(ServiceRequest request, ref ServiceResponse response)
        {
            response.id = "unknown";
            response.errcode = 10024;
            response.errmsg = "unknown request";
        }

        protected virtual void ModuleAddService(ServiceRequest request, ref ServiceResponse response)
        {
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>(Encoding.UTF8.GetString(request.raw_data));

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
                <Dictionary<string, dynamic>>(Encoding.UTF8.GetString(request.raw_data));

            Module module = modctl.GetModule(dc["name"]);
            if (module != null)
                modctl.Del(module);

            response.errcode = 0;
            response.errmsg = dc["name"] + " deleted";
        }

        protected virtual void ModuleLoadService(ServiceRequest request, ref ServiceResponse response)
        {
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>(Encoding.UTF8.GetString(request.raw_data));

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
                <Dictionary<string, dynamic>>(Encoding.UTF8.GetString(request.raw_data));

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

        protected virtual void SessOpenService(ServiceRequest request, ref ServiceResponse response)
        {
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>(Encoding.UTF8.GetString(request.raw_data));

            // find session and open
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), Convert.ToInt32(dc["port"]));
            SockSess sess = null;
            if (dc["type"] == SockType.listen.ToString() && sessctl.FindSession(SockType.listen, ep, null) == null)
                sess = sessctl.MakeListen(ep);
            else if (dc["type"] == SockType.connect.ToString())
                sess = sessctl.AddConnect(ep);
            else
                sess = null;

            if (sess != null) {
                response.errcode = 0;
                response.errmsg = dc["type"] + " " + ep.ToString();
            } else {
                response.errcode = 1;
                response.errmsg = "cannot find " + ep.ToString();
            }
        }

        protected virtual void SessCloseService(ServiceRequest request, ref ServiceResponse response)
        {
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>(Encoding.UTF8.GetString(request.raw_data));

            // find session
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), Convert.ToInt32(dc["port"]));
            SockSess sess = null;
            if (dc["type"] == SockType.listen.ToString())
                sess = sessctl.FindSession(SockType.listen, ep, null);
            else if (dc["type"] == SockType.connect.ToString())
                sess = sessctl.FindSession(SockType.connect, ep, null);
            else// if (dc["type"] == SockType.accept.ToString())
                sess = sessctl.FindSession(SockType.accept, null, ep);

            // close session
            if (sess != null)
                sessctl.DelSession(sess);

            if (sess != null) {
                response.errcode = 0;
                response.errmsg = "shutdown " + ep.ToString();
            } else {
                response.errcode = 1;
                response.errmsg = "cannot find " + ep.ToString();
            }
        }

        protected virtual void SessSendService(ServiceRequest request, ref ServiceResponse response)
        {
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>(Encoding.UTF8.GetString(request.raw_data));

            // find session
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), Convert.ToInt32(dc["port"]));
            SockSess sess = null;
            if (dc["type"] == SockType.listen.ToString())
                sess = sessctl.FindSession(SockType.listen, ep, null);
            else if (dc["type"] == SockType.connect.ToString())
                sess = sessctl.FindSession(SockType.connect, ep, null);
            else// if (dc["type"] == SockType.accept.ToString())
                sess = sessctl.FindSession(SockType.accept, null, ep);

            // send message
            if (sess != null)
                sessctl.SendSession(sess, Encoding.UTF8.GetBytes(dc["data"]));

            if (sess != null) {
                response.errcode = 0;
                response.errmsg = "send to " + ep.ToString();
            } else {
                response.errcode = 1;
                response.errmsg = "cannot find " + ep.ToString();
            }
        }

        protected virtual void SessDetailService(ServiceRequest request, ref ServiceResponse response)
        {
            List<object> pack = new List<object>();
            foreach (var item in sessctl.GetSessionTable()) {
                pack.Add(new {
                    type = item.type.ToString(),
                    localip = item.lep.ToString(),
                    remoteip = item.rep == null ? "" : item.rep.ToString(),
                });
            }

            response.data = pack;
        }
    }
}
