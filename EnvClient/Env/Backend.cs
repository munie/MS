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

            // response error
            if (dc["errcode"] != 0) {
                log4net.ILog logger = log4net.LogManager.GetLogger(typeof(Backend));
                logger.Info(dc["id"] + ": " + dc["errmsg"]);
                return;
            }

            // response fine
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                JObject jo = JObject.Parse(Encoding.UTF8.GetString(request.raw_data));
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
                        };
                        uidata.ClientTable.Add(client);
                    }
                }
            }));
        }

        private void OnSessCreate(object sender, SockSess sess) { }

        private void OnSessDelete(object sender, SockSess sess) { }

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
            string request = @"{'id':'core.sessdetail'}";
            sessctl.BeginInvoke(new Action(() => {
                sessctl.SendSession(sess, Encoding.UTF8.GetBytes(request));
            }));
        }
    }
}
