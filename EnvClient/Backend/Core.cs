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
        private string serverip = "127.0.0.1";
        private int serverport = 2000;
        public SockSessClient envclient;

        public UIData uidata;

        public Core()
        {
            servctl.RegisterService("notice.sesslisten", SessListenNotice, OnServiceDone);
            servctl.RegisterService("notice.sessaccept", SessAcceptNotice, OnServiceDone);
            servctl.RegisterService("notice.sessclose", SessCloseNotice, OnServiceDone);
            servctl.RegisterService("service.sessdetail", SessDetailResponse, OnServiceDone);
            servctl.RegisterService("service.sessgroupstate", SessGroupStateResponse, OnServiceDone);
            servctl.RegisterService("notice.moduleadd", ModuleAddNotice, OnServiceDone);
            servctl.RegisterService("notice.moduledelete", ModuleDeleteNotice, OnServiceDone);
            servctl.RegisterService("notice.moduleupdate", ModuleUpdateNotice, OnServiceDone);
            servctl.RegisterService("service.moduledetail", ModuleDetailResponse, OnServiceDone);

            try {
                envclient = new SockSessClient();
                envclient.recv_event += new SockSess.SockSessDelegate(OnRecvEvent);
                envclient.Connect(new IPEndPoint(IPAddress.Parse(serverip), serverport));
            } catch (Exception ex) {
                System.Windows.MessageBox.Show("failed to connect to server." + Environment.NewLine + ex.ToString());
                System.Threading.Thread.CurrentThread.Abort();
            }

            uidata = new UIData();

            System.Timers.Timer timer = new System.Timers.Timer(30 * 1000);
            timer.Elapsed += new System.Timers.ElapsedEventHandler((s, ea) => {
                SessDetailRequest();
                SessGroupStateRequest();
            });
            timer.Start();
        }

        protected override void Exec()
        {
            lock (envclient)
                envclient.DoSocket(1000);
            filtctl.Exec();
            servctl.Exec();
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

            lock (envclient)
                envclient.wfifo.Append(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(req)));
        }

        public void SessListenRequest(string ip, int port)
        {
            object req = new {
                id = "service.sesslisten",
                ip = ip,
                port = port,
            };

            lock (envclient)
                envclient.wfifo.Append(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(req)));
        }

        public void SessCloseRequest(string sessid)
        {
            object req = new {
                id = "service.sessclose",
                sessid = sessid,
            };

            lock (envclient)
                envclient.wfifo.Append(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(req)));
        }

        public void SessSendRequest(string sessid, string msg)
        {
            object req = new {
                id = "service.sesssend",
                sessid = sessid,
                data = msg,
            };

            lock (envclient)
                envclient.wfifo.Append(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(req)));
        }

        public void SessDetailRequest()
        {
            lock (envclient)
                envclient.wfifo.Append(Encoding.UTF8.GetBytes("{'id':'service.sessdetail'}"));
        }

        public void SessGroupStateRequest()
        {
            lock (envclient)
                envclient.wfifo.Append(Encoding.UTF8.GetBytes("{'id':'service.sessgroupstate'}"));
        }

        public void ModuleAddRequest(string filepath)
        {
            object req = new {
                id = "service.moduleadd",
                filepath = filepath,
            };

            lock (envclient)
                envclient.wfifo.Append(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(req)));
        }

        public void ModuleDelRequest(string name)
        {
            object req = new {
                id = "service.moduledel",
                name = name,
            };

            lock (envclient)
                envclient.wfifo.Append(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(req)));
        }

        public void ModuleLoadRequest(string name)
        {
            object req = new {
                id = "service.moduleload",
                name = name,
            };

            lock (envclient)
                envclient.wfifo.Append(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(req)));
        }

        public void ModuleUnloadRequest(string name)
        {
            object req = new {
                id = "service.moduleunload",
                name = name,
            };

            lock (envclient)
                envclient.wfifo.Append(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(req)));
        }

        public void ModuleDetailRequest()
        {
            lock (envclient)
                envclient.wfifo.Append(Encoding.UTF8.GetBytes("{'id':'service.moduledetail'}"));
        }

        // session events =======================================================

        protected virtual void OnRecvEvent(object sender)
        {
            SockSess sess = sender as SockSess;

            while (sess.rfifo.Size() != 0) {
                ServiceRequest request = ServiceRequest.Parse(sess.rfifo.Peek());
                if (request.packlen == 0)
                    break;

                sess.rfifo.Skip(request.packlen);
                servctl.AddRequest(request);
            }
        }

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

                uidata.AcceptOpenCount = Convert.ToInt32((string)msg["AcceptOpenCount"]);
                uidata.AcceptCloseCount = Convert.ToInt32((string)msg["AcceptCloseCount"]);
                uidata.AcceptTotalCount = Convert.ToInt32((string)msg["AcceptTotalCount"]);
                uidata.PackFetchedCount = Convert.ToInt32((string)msg["PackFetchedCount"]);
                uidata.PackParsedCount = Convert.ToInt32((string)msg["PackParsedCount"]);
                uidata.PackTotalCount = Convert.ToInt32((string)msg["PackTotalCount"]);
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
