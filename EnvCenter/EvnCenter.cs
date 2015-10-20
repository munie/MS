using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Runtime.InteropServices;
using Mnn.MnnSock;

namespace EnvCenter
{
    class EvnCenter
    {
        private SockSessManager sessmgr;
        private SockSess[] term_table
        {
            get
            {
                List<SockSess> list = new List<SockSess>();
                foreach (var item in sessmgr.sess_table) {
                    if ((item.sdata as TermUnit) != null)
                        list.Add(item);
                }
                return list.ToArray();
            }
        }
        private SockSess[] svc_table
        {
            get
            {
                List<SockSess> list = new List<SockSess>();
                foreach (var item in sessmgr.sess_table) {
                    if ((item.sdata as SvcUnit) != null)
                        list.Add(item);
                }
                return list.ToArray();
            }
        }

        public EvnCenter(SockSessManager sessmgr)
        {
            this.sessmgr = sessmgr;
            sessmgr.sess_parse += new SockSessManager.SessParseDelegate(sessmgr_sess_parse);
            sessmgr.sess_delete += new SockSessManager.SessDeleteDelegate(sessmgr_sess_delete);
            sessmgr.AddListenSession(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 2000));
            sessmgr.AddListenSession(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 3000));
            sessmgr.AddListenSession(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 3002));
            sessmgr.AddListenSession(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 3006));
            sessmgr.AddListenSession(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 3008));
        }

        void sessmgr_sess_delete(object sender, SockSess sess)
        {
            // 更新终端表模块信息
            if ((sess.sdata as SvcUnit) != null) {
                foreach (var item in term_table) {
                    if ((item.sdata as TermUnit).Info.Equals((sess.sdata as SvcUnit).TermInfo))
                        (item.sdata as TermUnit).Svc = null;
                }
            }
        }

        // Parse Methods ======================================================================

        private void sessmgr_sess_parse(object sender, SockSess sess)
        {
            // 兼容老版本字符流
            if (sess.rdata[0] == '|' && sess.rdata[1] == 'H' && sess.rdata[2] == 'T') {
                IDictionary<string, string> dc = AnalyzeString(Encoding.Default.GetString(sess.rdata));

                string term_info;
                if (dc["HT"][0] == 'Z')
                    term_info = "MMMMNNNNNNNNCZLS";
                else if (dc["HT"][0] == 'Q')
                    term_info = "MMMMNNNNNNNNCCQX";
                else if (dc["HT"][0] == 'D')
                    term_info = "MMMMNNNNNNNNCDXS";
                else
                    term_info = "MMMMNNNNNNNNCCSK";

                foreach (var item in svc_table) {
                    if ((item.sdata as SvcUnit).TermInfo.Equals(term_info)) {
                        item.sock.Send(sess.rdata, sess.rdata_size, System.Net.Sockets.SocketFlags.None);
                        break;
                    }
                }

                sess.rdata_size = 0;
                return;
            }

            // 验证数据流有效性
            SockPack.PackHeader hdr = (SockPack.PackHeader)SockConvert.BytesToStruct(sess.rdata, typeof(SockPack.PackHeader));
            if (hdr.len != sess.rdata_size) {
                Console.Write("[Error]: Unknow packet from {0}.\n", sess.rep.ToString());
                sess.rdata_size = 0;
                return;
            }

            // 根据SockPack.PackName，调用合适的处理方法
            switch (hdr.name) {
                case SockPack.PackName.alive:
                    break;
                case SockPack.PackName.term_register:
                    term_register(sess);
                    break;
                case SockPack.PackName.term_request:
                    term_request(sess);
                    break;
                case SockPack.PackName.term_respond:
                    term_respond(sess);
                    break;
                case SockPack.PackName.svc_register:
                    svc_register(sess);
                    break;
                default:
                    break;
            }

            // 数据流处理完成
            sess.rdata_size = 0;
        }

        private void term_register(SockSess sess)
        {
            SockPack.TermRegister treg = (SockPack.TermRegister)SockConvert.BytesToStruct(sess.rdata, typeof(SockPack.TermRegister));
            TermUnit term = new TermUnit(treg.ccid, treg.info);
            foreach (var item in svc_table) {
                if ((item.sdata as SvcUnit).TermInfo.Equals(new string(treg.info))) {
                    term.Svc = item;
                    break;
                }
            }
            sess.sdata = term;
        }

        private void term_request(SockSess sess)
        {
            if (sess.sdata == null || sess.sdata as TermUnit == null || (sess.sdata as TermUnit).Svc == null)
                return;
            
            (sess.sdata as TermUnit).Svc.sock.Send(sess.rdata, sess.rdata_size, System.Net.Sockets.SocketFlags.None);
        }

        private void term_respond(SockSess sess)
        {
            SockPack.TermRespond tres = (SockPack.TermRespond)SockConvert.BytesToStruct(sess.rdata, typeof(SockPack.TermRespond));
            
            foreach (var item in term_table) {
                if ((item.sdata as TermUnit).CCID.Equals(new string(tres.ccid))) {
                    item.sock.Send(sess.rdata, Marshal.SizeOf(tres),
                    sess.rdata_size - Marshal.SizeOf(tres),
                    System.Net.Sockets.SocketFlags.None);
                    break;
                }
            }
        }

        private void svc_register(SockSess sess)
        {
            SockPack.SvcRegister sreg = (SockPack.SvcRegister)SockConvert.BytesToStruct(sess.rdata, typeof(SockPack.SvcRegister));
            SvcUnit svc = new SvcUnit(sreg.term_info, sess);

            // 如果已有相同类型模块注册，则注册失败，关闭连接
            foreach (var item in svc_table) {
                if ((item.sdata as SvcUnit).TermInfo.Equals(svc.TermInfo)) {
                    sess.sock.Send(Encoding.ASCII.GetBytes("svc_register failed: similar svc already registered!"));
                    sess.eof = true;
                    break;
                }
            }

            // 更新模块表，必须放在模块监测之后
            sess.sdata = svc;

            // 更新终端表模块信息
            foreach (var item in term_table) {
                if ((item.sdata as TermUnit).Info.Equals(svc.TermInfo))
                    (item.sdata as TermUnit).Svc = sess;
            }
        }

        // Self Methods =======================================================================

        private IDictionary<string, string> AnalyzeString(string mes)
        {
            string txt = mes;
            string[] fields = txt.Split("|".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            Dictionary<string, string> dict = new Dictionary<string, string>();
            string[] kv;
            bool hasTheSameKey = false;
            foreach (string field in fields) {
                kv = field.Split("=".ToCharArray());
                //mod by zxq 2013-10-01
                if (dict.ContainsKey(kv[0])) {
                    if (!hasTheSameKey) {
                        hasTheSameKey = true;
                    }
                    dict[kv[0]] = kv[1];
                }
                else {
                    dict.Add(kv[0], kv[1]);
                }

            }
            if (hasTheSameKey) {
                //Program.writeLog_WithTime(string.Format("收到的消息中键值重复。消息详情为：{0}", mes));
            }
            return dict;
        }

    }
}
