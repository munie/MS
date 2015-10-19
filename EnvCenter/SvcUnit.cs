using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EnvCenter
{
    class SvcUnit
    {
        public string TermInfo { get; set; }
        public Mnn.MnnSock.SockSess Sess { get; set; }

        public SvcUnit(char[] info, Mnn.MnnSock.SockSess sess)
        {
            TermInfo = new string(info);
            Sess = sess;
        }
    }
}
