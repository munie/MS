using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EnvCenter
{
    [Serializable]
    public class TerminalBase
    {
        public virtual string CCID { get; set; }
        public virtual string Info { get; set; }
    }

    public class TermUnit : TerminalBase
    {
        public override string CCID { get; set; }
        public override string Info { get; set; }
        public mnn.net.SockSess Svc;

        public TermUnit(char[] ccid, char[] info, mnn.net.SockSess svc = null)
        {
            CCID = new string(ccid);
            Info = new string(info);
            Svc = svc;
        }

        public TerminalBase ToBase()
        {
            return new TerminalBase() { CCID = CCID, Info = Info };
        }
    }
}
