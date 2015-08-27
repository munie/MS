using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace StationConsole
{
    public class ClientUnit
    {
        public virtual string ID { get; set; }
        public virtual string Name { get; set; }
        public virtual IPEndPoint RemoteEP { get; set; }
        public virtual string ServerID { get; set; }
        public virtual string ServerName { get; set; }
        public virtual DateTime ConnectTime { get; set; }
    }
}
