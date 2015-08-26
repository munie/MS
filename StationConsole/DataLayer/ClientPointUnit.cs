using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StationConsole
{
    [Serializable]
    public class ClientPointUnit : ClientPoint, ICloneable
    {
        public override string RemoteIP { get; set; }
        public override int LocalPort { get; set; }
        public override DateTime ConnectTime { get; set; }
        public override string CCID { get; set; }
        public override string Name { get; set; }

        public object Clone()
        {
            ClientPointUnit client = new ClientPointUnit();
            client.RemoteIP = this.RemoteIP;
            client.LocalPort = this.LocalPort;
            client.ConnectTime = this.ConnectTime;
            client.CCID = this.CCID;
            client.Name = this.Name;

            return client;
        }
    }
}
