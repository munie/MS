using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using mnn.net;

namespace EnvCenter
{
    class Program
    {
        static void Main(string[] args)
        {
            SessCtl sessctl = new SessCtl();
            EvnCenter cter = new EvnCenter(sessctl);

            while(true) {
                sessctl.Perform(1000);
            }
        }
    }
}
