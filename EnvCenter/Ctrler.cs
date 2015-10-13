using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using Mnn.MnnSocket;
using Mnn.MnnUtil;

namespace EnvCenter
{
    class client_sdata
    {
    }

    class module_sdata
    {
        public int type;
    }

    class admin_sdata
    {
    }

    class Ctrler
    {
        public SockSessManager sessmgr;

        public Ctrler()
        {
            sessmgr = new SockSessManager();
            sessmgr.sess_create += new SockSessManager.SessCreateDelegate(sessmgr_sess_create);
            sessmgr.sess_delete += new SockSessManager.SessDeleteDelegate(sessmgr_sess_delete);
            sessmgr.sess_parse += new SockSessManager.SessParseDelegate(sessmgr_sess_parse);
            sessmgr.AddListenSession(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 2000));
            sessmgr.AddListenSession(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 3000));
            sessmgr.AddListenSession(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 3002));
            sessmgr.AddListenSession(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 3006));
            sessmgr.AddListenSession(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 3008));
        }

        private void sessmgr_sess_create(object sender, SockSess sess)
        {
        }

        private void sessmgr_sess_delete(object sender, SockSess sess)
        {
        }

        private void sessmgr_sess_parse(object sender, SockSess sess)
        {
            SockMsg.msghdr hdr = (SockMsg.msghdr)ConvertUtil.BytesToStruct(sess.rdata, typeof(SockMsg.msghdr));

            //if ((UInt16)sess.rdata[2]+((UInt16)sess.rdata[3]<<8) != sess.rdata_size) {
            if (hdr.len != sess.rdata_size) {
                Console.WriteLine("[Error]: Unknow packet from {0}.\n", sess.sock.RemoteEndPoint.ToString());
                sess.rdata_size = 0;
                return;
            }

            /*
             * 0x00 ~ 0x1f : amin
             * 0x20 ~ 0x3f : module
             * 0x40 ~ 0x7f : client
             * 0x80 ~ 0xff : reserved
             */
            switch (hdr.id_type) {
                case 0x00:
                case 0x01:
                    break;

                case 0x20:
                case 0x21:
                    module_parse(sess, hdr);
                    break;

                case 0x40:
                case 0x41: 
                case 0x42:
                case 0x43:
                    client_parse(sess);
                    break;

                default: break;
            }

            sess.rdata_size = 0;
        }

        private void client_parse(SockSess sess)
        {
            var subset = from s in sessmgr.sess_table
                         where s.sdata != null && (s.sdata as module_sdata).type == sess.rdata[1]
                         select s;

            foreach (var item in subset)
                item.sock.Send(sess.rdata, sess.rdata_size, System.Net.Sockets.SocketFlags.None);

            //foreach (var item in sessmgr.sess_table) {
            //    if (item.sdata != null && (item.sdata as module_sdata).type == sess.rdata[1]) {
            //        item.sock.Send(sess.rdata, sess.rdata_size, System.Net.Sockets.SocketFlags.None);
            //        break;
            //    }
            //}
        }

        private void module_parse(SockSess sess, SockMsg.msghdr hdr)
        {
            if (sess.sdata == null)
                sess.sdata = new module_sdata();

            module_sdata sd = sess.sdata as module_sdata;

            switch (hdr.msg_type) {
                case 0x0C: break;
                case 0x22:
                    sd.type = sess.rdata[4];
                    break;
            }
        }
    }
}
