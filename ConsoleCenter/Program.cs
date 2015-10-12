using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace ConsoleCenter
{
    class Program
    {
        static void Main(string[] args)
        {
            Mnn.MnnSocket.SockSessManager sessmgr = new Mnn.MnnSocket.SockSessManager(Parse);
            sessmgr.sess_create += new Mnn.MnnSocket.SockSessManager.SessCreateDelegate(sessmgr_sess_create);
            sessmgr.sess_delete += new Mnn.MnnSocket.SockSessManager.SessDeleteDelegate(sessmgr_sess_delete);
            sessmgr.AddListenSession(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5964));

            while(true) {
                sessmgr.Perform(1000);
            }
        }

        static void sessmgr_sess_create(object sender, Mnn.MnnSocket.SockSess sess)
        {
            Console.WriteLine("sess_create");
        }

        static void sessmgr_sess_delete(object sender, Mnn.MnnSocket.SockSess sess)
        {
            Console.WriteLine("sess_delete");
        }

        static void Parse(Mnn.MnnSocket.SockSess sess)
        {
            Console.WriteLine(Encoding.Default.GetString(sess.rdata.Take((int)sess.rdata_size).ToArray()));
            sess.rdata_size = 0;
        }


    }
}
