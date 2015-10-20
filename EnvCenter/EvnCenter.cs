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

        // Parse Methods ======================================================================

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
                case SockPack.PackName.cnt_irq:
                    cnt_irq(sess);
                    break;
                case SockPack.PackName.cnt_reg_term:
                    cnt_reg_term(sess);
                    break;
                case SockPack.PackName.cnt_send_term:
                    cnt_send_term(sess);
                    break;
                case SockPack.PackName.cnt_reg_svc:
                    cnt_reg_svc(sess);
                    break;
                case SockPack.PackName.cnt_login_user:
                    cnt_login_user(sess);
                    break;
                default:
                    break;
            }

            // 数据流处理完成
            sess.rdata_size = 0;
        }

        private void cnt_irq(SockSess sess)
        {
            if (sess.sdata == null || sess.sdata as TermUnit == null || (sess.sdata as TermUnit).Svc == null)
                return;

            sess.rdata[0] = (byte)SockPack.PackName.svc_handle;
            sess.rdata[1] = (byte)SockPack.PackName.svc_handle >> 8;
            (sess.sdata as TermUnit).Svc.sock.Send(sess.rdata, sess.rdata_size, System.Net.Sockets.SocketFlags.None);
        }

        private void cnt_reg_term(SockSess sess)
        {
            SockPack.CntRegTerm rterm = (SockPack.CntRegTerm)SockConvert.BytesToStruct(sess.rdata, typeof(SockPack.CntRegTerm));
            TermUnit term = new TermUnit(rterm.ccid, rterm.info);
            sess.sdata = term;

            // 获取对应的svc
            foreach (var item in svc_table) {
                if ((item.sdata as SvcUnit).TermInfo.Equals(new string(rterm.info))) {
                    term.Svc = item;
                    break;
                }
            }

            // 发送注册成功信息
            SockPack.TermHandle thandle = new SockPack.TermHandle();
            thandle.hdr.name = SockPack.PackName.term_handle;
            thandle.ccid = rterm.ccid;
            sess.sock.Send(StructPack(thandle, Encoding.ASCII.GetBytes("Register term success!")));
        }

        private void cnt_send_term(SockSess sess)
        {
            SockPack.CntSendTerm sterm = (SockPack.CntSendTerm)SockConvert.BytesToStruct(sess.rdata, typeof(SockPack.CntSendTerm));

            sess.rdata[0] = (byte)SockPack.PackName.term_handle;
            sess.rdata[1] = (byte)SockPack.PackName.term_handle >> 8;

            foreach (var item in term_table) {
                if ((item.sdata as TermUnit).CCID.Equals(new string(sterm.ccid))) {
                    item.sock.Send(sess.rdata, sess.rdata_size, System.Net.Sockets.SocketFlags.None);
                    break;
                }
            }
        }

        private void cnt_reg_svc(SockSess sess)
        {
            SockPack.CntRegSvc rsvc = (SockPack.CntRegSvc)SockConvert.BytesToStruct(sess.rdata, typeof(SockPack.CntRegSvc));
            SvcUnit svc = new SvcUnit(rsvc.term_info);

            // 如果已有相同类型模块注册，则注册失败，关闭连接
            SockPack.SvcHandle shandle = new SockPack.SvcHandle();
            foreach (var item in svc_table) {
                if ((item.sdata as SvcUnit).TermInfo.Equals(svc.TermInfo)) {
                    shandle.hdr.name = SockPack.PackName.svc_handle;
                    sess.sock.Send(StructPack(shandle,
                        Encoding.ASCII.GetBytes("Register svc failed: Similar svc already registered!")));
                    sess.eof = true;
                    return;
                }
            }
            shandle.hdr.name = SockPack.PackName.svc_handle;
            sess.sock.Send(StructPack(shandle, Encoding.ASCII.GetBytes("Register svc success!")));

            // 更新模块表，必须放在模块监测之后
            sess.sdata = svc;

            // 更新终端表模块信息
            foreach (var item in term_table) {
                if ((item.sdata as TermUnit).Info.Equals(svc.TermInfo))
                    (item.sdata as TermUnit).Svc = sess;
            }
        }

        private void cnt_login_user(SockSess sess)
        {
            SockPack.CntLoginUser luser = (SockPack.CntLoginUser)SockConvert.BytesToStruct(sess.rdata, typeof(SockPack.CntLoginUser));
            UserUnit user = new UserUnit(luser.userid, luser.passwd);

            // verify login


            sess.sdata = user;
        }

        // Self Methods =======================================================================

        private byte[] StructPack(object obj, byte[] data, bool update_len = true)
        {
            byte[] retval = SockConvert.StructToBytes(obj).Concat(data).ToArray();

            if (update_len) {
                retval[2] = (byte)(Marshal.SizeOf(obj) + data.Length);
                retval[3] = (byte)((Marshal.SizeOf(obj) + data.Length) >> 8);
            }

            return retval;
        }

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
