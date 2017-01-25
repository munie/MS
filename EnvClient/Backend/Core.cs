using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Net;
using System.Diagnostics;
using mnn.net;
using mnn.service;
using mnn.misc.glue;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using EnvClient.Unit;

namespace EnvClient.Backend {
    class Core : ServiceLayer {
        // sessctl
        public SessCtl sessctl;
        private string serverip = "127.0.0.1";
        private int serverport = 2000;
        private SockSess envserver;
        // uidata
        public UIData uidata;

        public Core()
        {
            servctl.RegisterService("notice.sesslisten", SessListenNotice);
            servctl.RegisterService("notice.sessaccept", SessAcceptNotice);
            servctl.RegisterService("notice.sessclose", SessCloseNotice);
            servctl.RegisterService("service.sessdetail", SessDetailResponse);
            servctl.RegisterService("service.sessgroupstate", SessGroupStateResponse);
            servctl.RegisterService("notice.moduleadd", ModuleAddNotice);
            servctl.RegisterService("notice.moduledelete", ModuleDeleteNotice);
            servctl.RegisterService("notice.moduleupdate", ModuleUpdateNotice);
            servctl.RegisterService("service.moduledetail", ModuleDetailResponse);

            sessctl = new SessCtl();
            sessctl.sess_parse += new SessCtl.SessDelegate(OnSessParse);
            sessctl.sess_create += new SessCtl.SessDelegate(OnSessCreate);
            sessctl.sess_delete += new SessCtl.SessDelegate(OnSessDelete);
            envserver = sessctl.AddConnect(new IPEndPoint(IPAddress.Parse(serverip), serverport));
            if (envserver == null) {
                System.Windows.MessageBox.Show("failed to connect to server.");
                System.Threading.Thread.CurrentThread.Abort();
            }

            uidata = new UIData();
        }

        public new void Run()
        {
            System.Timers.Timer timer = new System.Timers.Timer(30 * 1000);
            timer.Elapsed += new System.Timers.ElapsedEventHandler((s, ea) => {
                SessDetailRequest();
                SessGroupStateRequest();
            });
            timer.Start();

            System.Threading.Thread thread = new System.Threading.Thread(() => {
                while (true) {
                    try {
                        sessctl.Exec(1000);
                        servctl.Exec();
                    } catch (Exception ex) {
                        log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType)
                            .Error("Exception thrown out by core thread.", ex);
                    }
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        // requests ==============================================================

        public void SessLoginRequest()
        {
            object req = new {
                id = "service.sesslogin",
                admin = "true",
                name = "envclient",
                ccid = "envclient",
            };

            sessctl.BeginInvoke(new Action(() => {
                sessctl.SendSession(envserver, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(req)));
            }));
        }

        public void SessListenRequest(string ip, int port)
        {
            object req = new {
                id = "service.sesslisten",
                ip = ip,
                port = port,
            };

            sessctl.BeginInvoke(new Action(() => {
                sessctl.SendSession(envserver, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(req)));
            }));
        }

        public void SessCloseRequest(string sessid)
        {
            object req = new {
                id = "service.sessclose",
                sessid = sessid,
            };

            sessctl.BeginInvoke(new Action(() => {
                sessctl.SendSession(envserver, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(req)));
            }));
        }

        public void SessSendRequest(string sessid, string msg)
        {
            object req = new {
                id = "service.sesssend",
                sessid = sessid,
                data = msg,
            };

            sessctl.BeginInvoke(new Action(() => {
                sessctl.SendSession(envserver, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(req)));
            }));
        }

        public void SessDetailRequest()
        {
            sessctl.BeginInvoke(new Action(() => {
                sessctl.SendSession(envserver, Encoding.UTF8.GetBytes("{'id':'service.sessdetail'}"));
            }));
        }

        public void SessGroupStateRequest()
        {
            sessctl.BeginInvoke(new Action(() => {
                sessctl.SendSession(envserver, Encoding.UTF8.GetBytes("{'id':'service.sessgroupstate'}"));
            }));
        }

        public void ModuleAddRequest(string filepath)
        {
            object req = new {
                id = "service.moduleadd",
                filepath = filepath,
            };

            sessctl.BeginInvoke(new Action(() => {
                sessctl.SendSession(envserver, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(req)));
            }));
        }

        public void ModuleDelRequest(string name)
        {
            object req = new {
                id = "service.moduledel",
                name = name,
            };

            sessctl.BeginInvoke(new Action(() => {
                sessctl.SendSession(envserver, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(req)));
            }));
        }

        public void ModuleLoadRequest(string name)
        {
            object req = new {
                id = "service.moduleload",
                name = name,
            };

            sessctl.BeginInvoke(new Action(() => {
                sessctl.SendSession(envserver, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(req)));
            }));
        }

        public void ModuleUnloadRequest(string name)
        {
            object req = new {
                id = "service.moduleunload",
                name = name,
            };

            sessctl.BeginInvoke(new Action(() => {
                sessctl.SendSession(envserver, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(req)));
            }));
        }

        public void ModuleDetailRequest()
        {
            sessctl.BeginInvoke(new Action(() => {
                sessctl.SendSession(envserver, Encoding.UTF8.GetBytes("{'id':'service.moduledetail'}"));
            }));
        }

        // session events =======================================================

        private void OnSessParse(object sender, SockSess sess)
        {
            // init request
            ServiceRequest request = ServiceRequest.Parse(sess.RfifoTake());
            request.user_data = sess;

            // rfifo skip
            sess.RfifoSkip(request.packlen);

            // add request to service core
            servctl.AddRequest(request);
        }

        private void OnSessCreate(object sender, SockSess sess) { }

        private void OnSessDelete(object sender, SockSess sess) { }

        // services =============================================================

        private void SessListenNotice(ServiceRequest request, ref ServiceResponse response)
        {
            JObject jo = JObject.Parse((string)request.data);

            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                var msg = jo["data"];
                string sessid = (string)msg["sessid"];
                string type = (string)msg["type"];
                string laddress = ((string)msg["localip"]).Split(':')[0];
                int lport = Int32.Parse(((string)msg["localip"]).Split(':')[1]);
                string raddress = ((string)msg["remoteip"]).Split(':')[0];
                int rport = Int32.Parse(((string)msg["remoteip"]).Split(':')[1]);
                string tick = (string)msg["tick"];
                string conntime = (string)msg["conntime"];

                ListenUnit server = new ListenUnit() {
                    ID = sessid,
                    IpAddress = laddress,
                    Port = lport,
                };
                uidata.ListenTable.Add(server);
            }));
        }

        private void SessAcceptNotice(ServiceRequest request, ref ServiceResponse response)
        {
            JObject jo = JObject.Parse((string)request.data);

            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                var msg = jo["data"];
                string sessid = (string)msg["sessid"];
                string type = (string)msg["type"];
                string laddress = ((string)msg["localip"]).Split(':')[0];
                int lport = Int32.Parse(((string)msg["localip"]).Split(':')[1]);
                string raddress = ((string)msg["remoteip"]).Split(':')[0];
                int rport = Int32.Parse(((string)msg["remoteip"]).Split(':')[1]);
                string tick = (string)msg["tick"];
                string conntime = (string)msg["conntime"];

                AcceptUnit client = new AcceptUnit() {
                    ID = sessid,
                    LocalEP = new IPEndPoint(IPAddress.Parse(laddress), lport),
                    RemoteEP = new IPEndPoint(IPAddress.Parse(raddress), rport),
                    TickTime = DateTime.Parse(tick),
                    ConnTime = DateTime.Parse(conntime),
                };
                uidata.AcceptTable.Add(client);
            }));
        }

        private void SessCloseNotice(ServiceRequest request, ref ServiceResponse response)
        {
            JObject jo = JObject.Parse((string)request.data);

            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                var msg = jo["data"];
                string sessid = (string)msg["sessid"];

                foreach (var item in uidata.ListenTable) {
                    if (item.ID.Equals(sessid)) {
                        uidata.ListenTable.Remove(item);
                        return;
                    }
                }

                foreach (var item in uidata.AcceptTable) {
                    if (item.ID.Equals(sessid)) {
                        uidata.AcceptTable.Remove(item);
                        return;
                    }
                }
            }));
        }

        private void SessDetailResponse(ServiceRequest request, ref ServiceResponse response)
        {
            JObject jo = JObject.Parse((string)request.data);

            if ((int)jo["errcode"] != 0) {
                log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType)
                    .Info((string)jo["id"] + ": " + (string)jo["errmsg"]);
                return;
            }

            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                uidata.ListenTable.Clear();
                uidata.AcceptTable.Clear();
                foreach (var item in jo["data"]) {
                    string sessid = (string)item["sessid"];
                    string type = (string)item["type"];
                    string laddress = ((string)item["localip"]).Split(':')[0];
                    int lport = Convert.ToInt32(((string)item["localip"]).Split(':')[1]);
                    string raddress = ((string)item["remoteip"]).Split(':')[0];
                    int rport = Convert.ToInt32(((string)item["remoteip"]).Split(':')[1]);
                    string tick = (string)item["tick"];
                    string conntime = (string)item["conntime"];
                    string ccid = (string)item["ccid"];
                    string name = (string)item["name"];
                    bool admin = Convert.ToBoolean((string)item["admin"]);

                    if (type == "SockSessServer") {
                        ListenUnit server = new ListenUnit() {
                            ID = sessid,
                            IpAddress = laddress,
                            Port = lport,
                            Name = name,
                        };
                        if (serverport == server.Port)
                            server.Name = "EnvServer";
                        uidata.ListenTable.Add(server);
                    }

                    if (type == "SockSessAccept") {
                        AcceptUnit client = new AcceptUnit() {
                            ID = sessid,
                            LocalEP = new IPEndPoint(IPAddress.Parse(laddress), lport),
                            RemoteEP = new IPEndPoint(IPAddress.Parse(raddress), rport),
                            TickTime = DateTime.Parse(tick),
                            ConnTime = DateTime.Parse(conntime),
                            CCID = ccid,
                            Name = name,
                            Admin = admin,
                        };
                        uidata.AcceptTable.Add(client);
                    }
                }
            }));
        }

        private void SessGroupStateResponse(ServiceRequest request, ref ServiceResponse response)
        {
            JObject jo = JObject.Parse((string)request.data);

            if ((int)jo["errcode"] != 0) {
                log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType)
                    .Info((string)jo["id"] + ": " + (string)jo["errmsg"]);
                return;
            }

            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                var msg = jo["data"];

                uidata.CurrentAcceptCount = Convert.ToInt32((string)msg["CurrentAcceptCount"]);
                uidata.HistoryAcceptOpenCount = Convert.ToInt32((string)msg["HistoryAcceptOpenCount"]);
                uidata.HistoryAcceptCloseCount = Convert.ToInt32((string)msg["HistoryAcceptCloseCount"]);
                uidata.CurrentPackCount = Convert.ToInt32((string)msg["CurrentPackCount"]);
                uidata.HistoryPackFetchedCount = Convert.ToInt32((string)msg["HistoryPackFetchedCount"]);
                uidata.HistoryPackParsedCount = Convert.ToInt32((string)msg["HistoryPackParsedCount"]);
            }));
        }

        private void ModuleAddNotice(ServiceRequest request, ref ServiceResponse response)
        {
            JObject jo = JObject.Parse((string)request.data);

            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                var msg = jo["data"];

                ModuleUnit unit = new ModuleUnit();
                unit.FileName = (string)msg["name"];
                unit.FileVersion = (string)msg["version"];
                unit.ModuleState = (string)msg["state"];
                uidata.ModuleTable.Add(unit);
            }));
        }

        private void ModuleDeleteNotice(ServiceRequest request, ref ServiceResponse response)
        {
            JObject jo = JObject.Parse((string)request.data);

            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                var msg = jo["data"];

                foreach (var item in uidata.ModuleTable) {
                    if (item.FileName.Equals((string)msg["name"])) {
                        uidata.ModuleTable.Remove(item);
                        break;
                    }
                }
            }));
        }

        private void ModuleUpdateNotice(ServiceRequest request, ref ServiceResponse response)
        {
            JObject jo = JObject.Parse((string)request.data);

            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                var msg = jo["data"];

                foreach (var item in uidata.ModuleTable) {
                    if (item.FileName.Equals((string)msg["name"])) {
                        item.ModuleState = (string)msg["state"];
                        break;
                    }
                }
            }));
        }

        private void ModuleDetailResponse(ServiceRequest request, ref ServiceResponse response)
        {
            JObject jo = JObject.Parse((string)request.data);

            if ((int)jo["errcode"] != 0) {
                log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType)
                    .Info((string)jo["id"] + ": " + (string)jo["errmsg"]);
                return;
            }

            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                uidata.ModuleTable.Clear();
                foreach (var item in jo["data"]) {
                    ModuleUnit unit = new ModuleUnit();
                    unit.FileName = (string)item["name"];
                    unit.FileVersion = (string)item["version"];
                    unit.ModuleState = (string)item["state"];
                    uidata.ModuleTable.Add(unit);
                }
            }));
        }
    }
}
