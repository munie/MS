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
            SessCenter sesscer = new SessCenter();
            EvnCenter cter = new EvnCenter(sesscer);

            while(true) {
                sesscer.Perform(1000);
            }
        }
    }
}
