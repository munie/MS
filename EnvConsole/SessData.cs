using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EnvConsole {
    class SessData {
        public string Ccid { get; set; }
        public string Name { get; set; }
        public DateTime TimeConn { get; set; }
        public bool IsAdmin { get; set; }
        public System.Timers.Timer Timer { get; set; }
    }
}
