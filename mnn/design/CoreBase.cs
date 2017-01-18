using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using mnn.net;
using mnn.misc.service;
using mnn.misc.module;

namespace mnn.design {
    public class CoreBase {
        // timeout control
        protected TimeOutCtl timectl;
        // module control
        protected ModuleCtl modctl;
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

            // init servctl
            servctl = new ServiceCore();
            servctl.serv_before_do += new ServiceDoBeforeDelegate(OnServBeforeDo);
            servctl.serv_done += new ServiceDoneDelegate(OnServDone);

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

        // Service Event ==================================================================================

        protected virtual void OnServBeforeDo(ref ServiceRequest request)
        {
            if (request.content_mode != ServiceRequestContentMode.unknown) {
                try {
                    byte[] result = Convert.FromBase64String(Encoding.UTF8.GetString(request.data));
                    result = EncryptSym.AESDecrypt(result);
                    if (result != null)
                        request.data = result;
                } catch (Exception) { }
            }
        }

        protected virtual void OnServDone(ServiceRequest request, ServiceResponse response)
        {
            if (response.data != null && response.data.Length != 0) {
                sessctl.BeginInvoke(new Action(() =>
                {
                    SockSess result = sessctl.FindSession(SockType.accept, null, (request.user_data as SockSess).rep);
                    if (result != null)
                        sessctl.SendSession(result, response.data);
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
            response.data = Encoding.UTF8.GetBytes("Failure: unknown request\r\n");
        }

        protected virtual void SessOpenService(ServiceRequest request, ref ServiceResponse response)
        {
            // parse to dictionary
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>(Encoding.UTF8.GetString(request.data));

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
            StringBuilder sb = new StringBuilder();
            sb.Append("{");
            sb.Append(String.Format("'id':'{0}'", dc["id"]));
            if (result == null) {
                sb.Append(String.Format("'err':'failure'"));
                sb.Append(String.Format("'result':'cannot find {0}'", ep.ToString()));
            } else {
                sb.Append(String.Format("'err':'success'"));
                sb.Append(String.Format("'result':'{0} {1}'", dc["type"], ep.ToString()));
            }
            sb.Append("}\r\n");
            response.data = Encoding.UTF8.GetBytes(sb.ToString());
        }

        protected virtual void SessCloseService(ServiceRequest request, ref ServiceResponse response)
        {
            // parse to dictionary
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>(Encoding.UTF8.GetString(request.data));

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
            StringBuilder sb = new StringBuilder();
            sb.Append("{");
            sb.Append(String.Format("'id':'{0}'", dc["id"]));
            if (result == null) {
                sb.Append(String.Format("'err':'failure'"));
                sb.Append(String.Format("'result':'cannot find {0}'", ep.ToString()));
            } else {
                sb.Append(String.Format("'err':'success'"));
                sb.Append(String.Format("'result':'shutdowm {0}'", ep.ToString()));
            }
            sb.Append("}\r\n");
            response.data = Encoding.UTF8.GetBytes(sb.ToString());
        }

        protected virtual void SessSendService(ServiceRequest request, ref ServiceResponse response)
        {
            // parse to dictionary
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>(Encoding.UTF8.GetString(request.data));

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
            StringBuilder sb = new StringBuilder();
            sb.Append("{");
            sb.Append(String.Format("'id':'{0}'", dc["id"]));
            if (result == null) {
                sb.Append(String.Format("'err':'failure'"));
                sb.Append(String.Format("'result':'cannot find {0}'", ep.ToString()));
            } else {
                sb.Append(String.Format("'err':'success'"));
                sb.Append(String.Format("'result':'send to {0}'", ep.ToString()));
            }
            sb.Append("}\r\n");
            response.data = Encoding.UTF8.GetBytes(sb.ToString());
        }
    }
}
