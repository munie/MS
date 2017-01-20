using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EnvServer {
    class SessData {
        public int ParentPort { get; set; }
        public bool IsAdmin { get; set; }
        public System.Timers.Timer Timer { get; set; }

        public string Ccid { get; set; }
        public string Name { get; set; }
    }
}
