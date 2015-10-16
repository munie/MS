using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using Mnn.MnnSock;

namespace EnvCenter
{
    class SessData
    {
        public string ccid;
        public int type;
    }

    class Ctrler
    {
        public Ctrler(SockSessManager sessmgr)
        {
            this.sessmgr = sessmgr;
            sessmgr.sess_parse += new SockSessManager.SessParseDelegate(sessmgr_sess_parse);
            sessmgr.AddListenSession(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 2000));
            sessmgr.AddListenSession(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 3002));
        }

        SockSessManager sessmgr;

        private void sessmgr_sess_parse(object sender, SockSess sess)
        {
            SockMsg.msghdr hdr = (SockMsg.msghdr)SockConvert.BytesToStruct(sess.rdata, typeof(SockMsg.msghdr));

            if (hdr.len != sess.rdata_size) {
                Console.Write("[Error]: Unknow packet from {0}.\n", sess.rep.ToString());
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
                case 0x20:
                case 0x21: module_parse(sess, hdr); break;

                case 0x40:
                case 0x41: 
                case 0x42:
                case 0x43: term_parse(sess); break;

                default: break;
            }

            sess.rdata_size = 0;
        }

        private void term_parse(SockSess sess)
        {
            SockMsg.termhdr thdr = (SockMsg.termhdr)SockConvert.BytesToStruct(sess.rdata, typeof(SockMsg.termhdr));

            // 保存ccid
            if (sess.sdata == null)
                sess.sdata = new SessData();
            SessData sd = sess.sdata as SessData;
            sd.ccid = new string(thdr.ccid);

            // 迭代sess_table，找到对应的处理模块，发送数据
            foreach (var item in sessmgr.sess_table) {
                if (item.sdata != null && (item.sdata as SessData).type == sess.rdata[1]) {
                    item.sock.Send(sess.rdata, sess.rdata_size, System.Net.Sockets.SocketFlags.None);
                    break;
                }
            }
        }

        private void module_parse(SockSess sess, SockMsg.msghdr hdr)
        {
            if (sess.sdata == null)
                sess.sdata = new SessData();
            SessData sd = sess.sdata as SessData;

            switch (hdr.msg_type) {
                case 0x0C: break;
                case 0x22: // 注册目标term类型
                    sd.type = sess.rdata[4];
                    break;
            }
        }
    }
}
