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
            servctl.RegisterService("notice.sesscreate", SessCreateNotice);
            servctl.RegisterService("notice.sessdelete", SessDeleteNotice);
            servctl.RegisterService("service.sessdetail", SessDetailResponse);
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

        public void SessOpenRequest(string type, int port)
        {
            object req = new {
                id = "service.sessopen",
                type = type,
                ip = "0",
                port = port,
            };

            sessctl.BeginInvoke(new Action(() => {
                sessctl.SendSession(envserver, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(req)));
            }));
        }

        public void SessCloseRequest(string type, string ip, int port)
        {
            object req = new {
                id = "service.sessclose",
                type = type,
                ip = ip,
                port = port,
            };

            sessctl.BeginInvoke(new Action(() => {
                sessctl.SendSession(envserver, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(req)));
            }));
        }

        public void SessSendRequest(string type, string ip, int port, string msg)
        {
            object req = new {
                id = "service.sesssend",
                type = type,
                ip = ip,
                port = port,
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
            // init request & response
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

        private void SessCreateNotice(ServiceRequest request, ref ServiceResponse response)
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
                        TickTime = DateTime.Parse((string)tmp["tick"]),
                        ConnectTime = DateTime.Parse((string)tmp["conntime"]),
                    };
                    uidata.ClientTable.Add(client);
                }
            }));
        }

        private void SessDeleteNotice(ServiceRequest request, ref ServiceResponse response)
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

        private void SessDetailResponse(ServiceRequest request, ref ServiceResponse response)
        {
            JObject jo = JObject.Parse(Encoding.UTF8.GetString(request.raw_data));

            if ((int)jo["errcode"] != 0) {
                log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType)
                    .Info((string)jo["id"] + ": " + (string)jo["errmsg"]);
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
                            Name = (string)item["name"],
                        };
                        if (serverport == server.Port)
                            server.Name = "core basic";
                        uidata.ServerTable.Add(server);
                    }

                    if ((string)item["type"] == "accept") {
                        ClientUnit client = new ClientUnit() {
                            RemoteEP = new IPEndPoint(IPAddress.Parse(((string)item["remoteip"]).Split(':')[0]),
                                Int32.Parse(((string)item["remoteip"]).Split(':')[1])),
                            TickTime = DateTime.Parse((string)item["tick"]),
                            ConnectTime = DateTime.Parse((string)item["conntime"]),
                            ID = (string)item["ccid"],
                            Name = (string)item["name"],
                            ServerPort = (int)item["parentport"],
                        };
                        uidata.ClientTable.Add(client);
                    }
                }
            }));
        }

        private void ModuleAddNotice(ServiceRequest request, ref ServiceResponse response)
        {
            JObject jo = JObject.Parse(Encoding.UTF8.GetString(request.raw_data));

            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                var tmp = jo["data"];

                ModuleUnit unit = new ModuleUnit();
                unit.FileName = (string)tmp["name"];
                unit.FileVersion = (string)tmp["version"];
                unit.ModuleState = (string)tmp["state"];
                uidata.ModuleTable.Add(unit);
            }));
        }

        private void ModuleDeleteNotice(ServiceRequest request, ref ServiceResponse response)
        {
            JObject jo = JObject.Parse(Encoding.UTF8.GetString(request.raw_data));

            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                var tmp = jo["data"];

                foreach (var item in uidata.ModuleTable) {
                    if (item.FileName.Equals((string)tmp["name"])) {
                        uidata.ModuleTable.Remove(item);
                        break;
                    }
                }
            }));
        }

        private void ModuleUpdateNotice(ServiceRequest request, ref ServiceResponse response)
        {
            JObject jo = JObject.Parse(Encoding.UTF8.GetString(request.raw_data));

            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                var tmp = jo["data"];

                foreach (var item in uidata.ModuleTable) {
                    if (item.FileName.Equals((string)tmp["name"])) {
                        item.ModuleState = (string)tmp["state"];
                        break;
                    }
                }
            }));
        }

        private void ModuleDetailResponse(ServiceRequest request, ref ServiceResponse response)
        {
            JObject jo = JObject.Parse(Encoding.UTF8.GetString(request.raw_data));

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
