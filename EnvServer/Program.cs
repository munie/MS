using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EnvServer {
    class Program {
        static void Main(string[] args)
        {
            InitLog4net();
            InitCore();
        }

        static private void InitLog4net()
        {
            var config = new System.IO.FileInfo(AppDomain.CurrentDomain.BaseDirectory + "EnvServer.xml");
            log4net.Config.XmlConfigurator.Configure(config);
        }

        static private void InitCore()
        {
            Core core = new Core();
            core.sessctl.MakeListen(new System.Net.IPEndPoint(0, 2000));
            core.RunForever();
        }
    }
}
