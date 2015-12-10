using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace mnn.misc.env {
    public class EnvConst {
        public static readonly string BASE_DIR = System.AppDomain.CurrentDomain.BaseDirectory;

        public static readonly string Module_PATH = BASE_DIR + "Modules";

        public static readonly string CONF_NAME = "EnvConsole.xml";
        public static readonly string CONF_PATH = BASE_DIR + CONF_NAME;
        public static readonly string CONF_ENCODING = "/configuration/encoding";
        public static readonly string CONF_SERVER = "/configuration/servers/server";
        public static readonly string CONF_TIMER = "/configuration/timers/timer";
    }
}
