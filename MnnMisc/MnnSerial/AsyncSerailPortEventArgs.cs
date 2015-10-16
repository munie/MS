using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mnn.MnnMisc.MnnSerial
{
    public class AsyncSerailPortEventArgs : EventArgs
    {
        public string PortName { get; set; }
        public int BaudRate { get; set; }
        public int DataBits { get; set; }
        public int StopBits { get; set; }
        public int Parity { get; set; }
        public string Data { get; set; }
    }
}
