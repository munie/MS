using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StationConsole.ControlLayer
{
    public class ServerUnit : MnnUnit
    {
        public virtual string ServerType { get; set; }
        public virtual string Protocol { get; set; }
        public virtual string IpAddress { get; set; }
        public virtual int Port { get; set; }
        public virtual string PipeName { get; set; }
        public virtual bool AutoRun { get; set; }
        public virtual bool CanStop { get; set; }

        public Mnn.MnnSocket.SockServer Server { get; set; }
        public System.Timers.Timer Timer { get; set; }
    }
}
