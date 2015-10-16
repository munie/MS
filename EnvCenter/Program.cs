using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using Mnn.MnnSock;

namespace EnvCenter
{
    class Program
    {
        static void Main(string[] args)
        {
            SockSessManager sessmgr = new SockSessManager();
            Ctrler clter = new Ctrler(sessmgr);

            while(true) {
                sessmgr.Perform(1000);
            }
        }
    }
}
