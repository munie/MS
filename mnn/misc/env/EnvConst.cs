using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace mnn.misc.env {
    public class EnvConst {
        /// <summary>
        /// base directory
        /// </summary>
        public static readonly string BASE_DIR = System.AppDomain.CurrentDomain.BaseDirectory;

        /// <summary>
        /// module path
        /// </summary>
        public static readonly string Module_PATH = BASE_DIR + "modules";

        /// <summary>
        /// configuration file path
        /// </summary>
        public static readonly string CONF_NAME = "EnvConsole.xml";
        public static readonly string CONF_PATH = BASE_DIR + CONF_NAME;
        public static readonly string CONF_SERVER = "/configuration/server";
        public static readonly string CONF_TIMER = "/configuration/timers/timer";

        /// <summary>
        /// common msg keys
        /// </summary>
        public static readonly string CCID = "ccid";
        public static readonly string CSQ = "csq";
        public static readonly string VOLTAGE = "voltage";
        public static readonly string GPSE = "gpse";
        public static readonly string GPSN = "gpsn";
        public static readonly string TIME = "time";
        public static readonly string ALARM = "alarm";
        public static readonly string MLSTR = "mlstr";
        public static readonly string REPLY = "reply";

        /// <summary>
        /// flow msg keys
        /// </summary>
        public static readonly string FLOW_TOTAL = "flow_total";

        /// <summary>
        /// response
        /// </summary>
        public static readonly string OK = "*OK#";
    }
}
