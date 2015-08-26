using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StationConsole
{
    public abstract class ClientPoint
    {
        public virtual string RemoteIP { get; set; }
        public virtual int LocalPort { get; set; }
        public virtual DateTime ConnectTime { get; set; }
        public virtual string CCID { get; set; }
        public virtual string Name { get; set; }
    }
}
