using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DataProcess
{
    public class DataHandleSuccessEventArgs : EventArgs
    {
        public DataHandleSuccessEventArgs(System.Net.IPEndPoint ep, string ccid, string name)
        {
            EP = ep;
            CCID = ccid;
            Name = name;
        }

        public System.Net.IPEndPoint EP { get; set; }
        public string CCID { get; set; }
        public string Name { get; set; }
    }
}
