using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StationConsole
{
    class TimerUnit
    {
        public string ID { get; set; }
        public string Type { get; set; }
        public string Command { get; set; }

        public System.Timers.Timer timer;
    }
}
