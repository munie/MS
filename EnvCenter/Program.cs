using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace EnvCenter
{
    class Program
    {
        static void Main(string[] args)
        {
            Ctrler clter = new Ctrler();

            while(true) {
                clter.sessmgr.Perform(1000);
            }
        }
    }
}
