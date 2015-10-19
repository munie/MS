using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EnvCenter
{
    class TermUnit
    {
        public string CCID { get; set; }
        public string Info { get; set; }
        public Mnn.MnnSock.SockSess Svc { get; set; }

        public TermUnit(char[] ccid, char[] info, Mnn.MnnSock.SockSess svc = null)
        {
            CCID = new string(ccid);
            Info = new string(info);
            Svc = svc;
        }
    }
}
