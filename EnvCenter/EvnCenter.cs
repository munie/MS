using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Runtime.InteropServices;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
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
                case SockPack.PackName.cnt_info_term:
                    cnt_info_term(sess);
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
            SockPack.CntRegTerm regterm = (SockPack.CntRegTerm)SockConvert.BytesToStruct(sess.rdata, typeof(SockPack.CntRegTerm));
            TermUnit term = new TermUnit(regterm.ccid, regterm.info);

            // 如果已有相同类型终端注册，则注册失败，关闭连接
            regterm.hdr.name++;
            foreach (var item in term_table) {
                if ((item.sdata as TermUnit).CCID.Equals(term.CCID)) {
                    sess.sock.Send(StructPack(regterm,
                        Encoding.ASCII.GetBytes("Register term failed: Similar term already registered!")));
                    sess.eof = true;
                    return;
                }
            }
            // 发送注册成功信息
            sess.sock.Send(StructPack(regterm, Encoding.ASCII.GetBytes("Register term success!")));

            // 更新终端表，必须放在验证之后
            sess.sdata = term;

            // 获取对应的svc
            foreach (var item in svc_table) {
                if ((item.sdata as SvcUnit).TermInfo.Equals(new string(regterm.info))) {
                    term.Svc = item;
                    break;
                }
            }
        }

        private void cnt_send_term(SockSess sess)
        {
            SockPack.CntSendTerm sendterm = (SockPack.CntSendTerm)SockConvert.BytesToStruct(sess.rdata, typeof(SockPack.CntSendTerm));

            sess.rdata[0] = (byte)SockPack.PackName.term_handle;
            sess.rdata[1] = (byte)SockPack.PackName.term_handle >> 8;

            foreach (var item in term_table) {
                if ((item.sdata as TermUnit).CCID.Equals(new string(sendterm.ccid))) {
                    item.sock.Send(sess.rdata, sess.rdata_size, System.Net.Sockets.SocketFlags.None);
                    break;
                }
            }
        }

        private void cnt_info_term(SockSess sess)
        {
            SockPack.CntInfoTerm infoterm = (SockPack.CntInfoTerm)SockConvert.BytesToStruct(sess.rdata, typeof(SockPack.CntInfoTerm));
            infoterm.hdr.name++;

            try {
                byte[] buffer = new byte[2048];
                MemoryStream memoryStream = new MemoryStream(buffer);
                XmlSerializer xmlFormat = new XmlSerializer(typeof(SerializableTermList));

                SerializableTermList list = new SerializableTermList();
                if ("00000000000000000000".Equals(new string(infoterm.ccid))) {
                    foreach (var item in term_table)
                        list.terminals.Add((item.sdata as TermUnit).ToBase());
                }
                else {
                    foreach (var item in term_table) {
                        if ((item.sdata as TermUnit).CCID.Equals(new string(infoterm.ccid))) {
                            list.terminals.Add((item.sdata as TermUnit).ToBase());
                            break;
                        }
                    }
                }
                xmlFormat.Serialize(memoryStream, list);

                sess.sock.Send(StructPack(infoterm, buffer.Take((int)memoryStream.Position).ToArray()));
                memoryStream.Close();
            }
            catch (Exception ex) {
                Console.Write(ex.ToString());
            }
        }

        private void cnt_reg_svc(SockSess sess)
        {
            SockPack.CntRegSvc regsvc = (SockPack.CntRegSvc)SockConvert.BytesToStruct(sess.rdata, typeof(SockPack.CntRegSvc));
            SvcUnit svc = new SvcUnit(regsvc.term_info);

            // 如果已有相同类型模块注册，则注册失败，关闭连接
            regsvc.hdr.name++;
            foreach (var item in svc_table) {
                if ((item.sdata as SvcUnit).TermInfo.Equals(svc.TermInfo)) {
                    sess.sock.Send(StructPack(regsvc,
                        Encoding.ASCII.GetBytes("Register svc failed: Similar svc already registered!")));
                    sess.eof = true;
                    return;
                }
            }
            sess.sock.Send(StructPack(regsvc, Encoding.ASCII.GetBytes("Register svc success!")));

            // 更新模块表，必须放在验证之后
            sess.sdata = svc;

            // 更新终端表模块信息
            foreach (var item in term_table) {
                if ((item.sdata as TermUnit).Info.Equals(svc.TermInfo))
                    (item.sdata as TermUnit).Svc = sess;
            }
        }

        private void cnt_login_user(SockSess sess)
        {
            SockPack.CntLoginUser loginuser = (SockPack.CntLoginUser)SockConvert.BytesToStruct(sess.rdata, typeof(SockPack.CntLoginUser));
            UserUnit user = new UserUnit(loginuser.userid, loginuser.passwd);

            // verify login


            sess.sdata = user;
        }

        // Self Methods =======================================================================

        private byte[] StructPack(object obj, byte[] data, bool update_len = true)
        {
            byte[] retval = SockConvert.StructToBytes(obj).Concat(data).ToArray();

            if (update_len) {
                retval[2] = (byte)retval.Length;
                retval[3] = (byte)(retval.Length >> 8);
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
