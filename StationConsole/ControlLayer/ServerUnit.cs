using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StationConsole
{
    public class ServerUnit
    {
        public virtual string ID { get; set; }
        public virtual string Name { get; set; }
        public virtual string Type { get; set; }
        public virtual string Protocol { get; set; }
        public virtual string IpAddress { get; set; }
        public virtual int Port { get; set; }
        public virtual string PipeName { get; set; }
        public virtual bool AutoRun { get; set; }
        public virtual bool CanStop { get; set; }

        public Mnn.MnnSocket.SockServer Server { get; set; }
    }
}
