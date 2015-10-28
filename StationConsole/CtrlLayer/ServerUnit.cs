using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StationConsole.CtrlLayer
{
    public class ServerUnit
    {
        public virtual string ID { get; set; }
        public virtual string Name { get; set; }

        public virtual string ServerType { get; set; }
        public virtual string Protocol { get; set; }
        public virtual string IpAddress { get; set; }
        public virtual int Port { get; set; }
        public virtual string PipeName { get; set; }
        public virtual bool AutoRun { get; set; }
        public virtual bool CanStop { get; set; }

        public mnn.net.deprecated.SockServer Server { get; set; }
        public System.Timers.Timer Timer { get; set; }
    }
}
