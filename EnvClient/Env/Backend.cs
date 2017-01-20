using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Net;
using mnn.net;
using mnn.service;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using EnvClient.Unit;

namespace EnvClient.Env {
    class Backend {
        public SessCtl sessctl;
        private string serverip = "127.0.0.1";
        private int serverport = 2000;
        private SockSess sess;

        public UIData uidata;

        public Backend()
        {
            sessctl = new SessCtl();
            sessctl.sess_parse += new SessCtl.SessDelegate(OnSessParse);
            sessctl.sess_create += new SessCtl.SessDelegate(OnSessCreate);
            sessctl.sess_delete += new SessCtl.SessDelegate(OnSessDelete);
            sess = sessctl.AddConnect(new IPEndPoint(IPAddress.Parse(serverip), serverport));

            uidata = new UIData();
        }

        // session events =======================================================

        private void OnSessParse(object sender, SockSess sess)
        {
            // init request & response
            ServiceRequest request = ServiceRequest.Parse(sess.RfifoTake());
            request.user_data = sess;

            // rfifo skip
            sess.RfifoSkip(request.packlen);

            // parse to dictionary
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>(Encoding.UTF8.GetString(request.raw_data));

            if (((string)dc["id"]).StartsWith("notice"))
                ParseNotice(dc["id"], request);
            else
                ParseResponse(dc["id"], request);
        }

        private void OnSessCreate(object sender, SockSess sess) { }

        private void OnSessDelete(object sender, SockSess sess) { }

        // responses =============================================================

        private void ParseResponse(string id, ServiceRequest request)
        {
            switch (id) {
                case "core.sessdetail":
                    SessDetailResponse(request);
                    break;

                default:
                    break;
            }
        }

        private void SessDetailResponse(ServiceRequest request)
        {
            JObject jo = JObject.Parse(Encoding.UTF8.GetString(request.raw_data));

            if ((int)jo["errcode"] != 0) {
                log4net.ILog logger = log4net.LogManager.GetLogger(typeof(Backend));
                logger.Info((string)jo["id"] + ": " + (string)jo["errmsg"]);
                return;
            }

            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => {

                uidata.ServerTable.Clear();
                uidata.ClientTable.Clear();
                foreach (var item in jo["data"]) {
                    if ((string)item["type"] == "listen") {
                        ServerUnit server = new ServerUnit() {
                            IpAddress = ((string)item["localip"]).Split(':')[0],
                            Port = Int32.Parse(((string)item["localip"]).Split(':')[1]),
                        };
                        uidata.ServerTable.Add(server);
                    }

                    if ((string)item["type"] == "accept") {
                        ClientUnit client = new ClientUnit() {
                            RemoteEP = new IPEndPoint(IPAddress.Parse(((string)item["remoteip"]).Split(':')[0]),
                                Int32.Parse(((string)item["remoteip"]).Split(':')[1])),
                            ConnectTime = DateTime.Parse((string)item["conntime"]),
                        };
                        uidata.ClientTable.Add(client);
                    }
                }
            }));
        }

        // notices ===============================================================

        private void ParseNotice(string id, ServiceRequest request)
        {
            switch (id) {
                case "notice.core.sesscreate":
                    SessCreateNotice(request);
                    break;

                case "notice.core.sessdelete":
                    SessDeleteNotice(request);
                    break;

                default:
                    break;
            }
        }

        private void SessCreateNotice(ServiceRequest request)
        {
            JObject jo = JObject.Parse(Encoding.UTF8.GetString(request.raw_data));

            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                var tmp = jo["data"];
                if ((string)tmp["type"] == "listen") {
                    ServerUnit server = new ServerUnit() {
                        IpAddress = ((string)tmp["localip"]).Split(':')[0],
                        Port = Int32.Parse(((string)tmp["localip"]).Split(':')[1]),
                    };
                    uidata.ServerTable.Add(server);
                } else if ((string)tmp["type"] == "accept") {
                    ClientUnit client = new ClientUnit() {
                        RemoteEP = new IPEndPoint(IPAddress.Parse(((string)tmp["remoteip"]).Split(':')[0]),
                            Int32.Parse(((string)tmp["remoteip"]).Split(':')[1])),
                        ConnectTime = DateTime.Parse((string)tmp["conntime"]),
                    };
                    uidata.ClientTable.Add(client);
                }
            }));
        }

        private void SessDeleteNotice(ServiceRequest request)
        {
            JObject jo = JObject.Parse(Encoding.UTF8.GetString(request.raw_data));

            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                var tmp = jo["data"];
                if ((string)tmp["type"] == "listen") {
                    foreach (var item in uidata.ServerTable) {
                        if (item.IpAddress.Equals(((string)tmp["localip"]).Split(':')[0])
                            && item.Port == Int32.Parse(((string)tmp["localip"]).Split(':')[1])) {
                            uidata.ServerTable.Remove(item);
                            break;
                        }
                    }
                } else if ((string)tmp["type"] == "accept") {
                    foreach (var item in uidata.ClientTable) {
                        if (item.RemoteEP.Equals(new IPEndPoint(IPAddress.Parse(((string)tmp["remoteip"]).Split(':')[0]),
                            Int32.Parse(((string)tmp["remoteip"]).Split(':')[1])))) {
                            uidata.ClientTable.Remove(item);
                            break;
                        }
                    }
                }
            }));
        }

        // methods ==============================================================

        public void Run()
        {
            System.Threading.Thread thread = new System.Threading.Thread(() => {
                while (true) {
                    try {
                        sessctl.Exec(1000);
                    } catch (Exception ex) {
                        log4net.ILog log = log4net.LogManager.GetLogger(typeof(Backend));
                        log.Error("Exception thrown out by core thread.", ex);
                    }
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        public void Login()
        {
            sessctl.BeginInvoke(new Action(() => {
                sessctl.SendSession(sess, Encoding.UTF8.GetBytes("{'id':'core.sesslogin', 'admin':'true'}"));
            }));

            sessctl.BeginInvoke(new Action(() => {
                sessctl.SendSession(sess, Encoding.UTF8.GetBytes("{'id':'core.sessdetail'}"));
            }));
        }
    }
}
