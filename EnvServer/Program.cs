using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Net;

namespace EnvServer {
    class Program {
        private static readonly string CONF_PATH = AppDomain.CurrentDomain.BaseDirectory + "EnvServer.xml";
        private static readonly string SERVER_CONF_NODE = "/configuration/server";

        private static string serverip = "127.0.0.1";
        private static int serverport = 2000;

        static void Main(string[] args)
        {
            InitLog4net();
            InitCore();
        }

        private static void InitLog4net()
        {
            var config = new System.IO.FileInfo(CONF_PATH);
            log4net.Config.XmlConfigurator.Configure(config);
        }

        private static void InitCore()
        {
            Core core = new Core();

            if (System.IO.File.Exists(CONF_PATH)) {
                try {
                    XmlDocument xdoc = new XmlDocument();
                    xdoc.Load(CONF_PATH);

                    XmlNode node = xdoc.SelectSingleNode(SERVER_CONF_NODE);
                    serverip = node.Attributes["ipaddress"].Value;
                    serverport = int.Parse(node.Attributes["port"].Value);
                } catch (Exception ex) {
                    log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType)
                        .Error("error when parsing config file.", ex);
                }
            }

            core.MakeListen(new IPEndPoint(IPAddress.Parse(serverip), serverport));
            core.Run();
        }
    }
}
