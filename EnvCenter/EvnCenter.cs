using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Runtime.InteropServices;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using mnn.net;

namespace EnvCenter
{
    class EvnCenter
    {
        private SessCtl sesscer;
        private SockSess[] term_table
        {
            get
            {
                List<SockSess> list = new List<SockSess>();
                foreach (var item in sesscer.sess_table) {
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
                foreach (var item in sesscer.sess_table) {
                    if ((item.sdata as SvcUnit) != null)
                        list.Add(item);
                }
                return list.ToArray();
            }
        }
        private SockSess[] user_table
        {
            get
            {
                List<SockSess> list = new List<SockSess>();
                foreach (var item in sesscer.sess_table) {
                    if ((item.sdata as UserUnit) != null)
                        list.Add(item);
                }
                return list.ToArray();
            }
        }

        public EvnCenter(SessCtl sessctl)
        {
            this.sesscer = sessctl;
            sessctl.sess_parse += new SessCtl.SessParseDelegate(sessmgr_sess_parse);
            sessctl.sess_delete += new SessCtl.SessDeleteDelegate(sessmgr_sess_delete);
            sessctl.MakeListen(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 2000));
            sessctl.MakeListen(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 3000));
            sessctl.MakeListen(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 3002));
            sessctl.MakeListen(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 3006));
            sessctl.MakeListen(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 3008));
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
                case SockPack.PackName.cnt_info_user:
                    cnt_info_user(sess);
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
            SockPack.CntRegTerm reg_term = (SockPack.CntRegTerm)SockConvert.BytesToStruct(sess.rdata, typeof(SockPack.CntRegTerm));
            reg_term.hdr.name++;
            TermUnit term = new TermUnit(reg_term.ccid, reg_term.info);

            // 如果已有相同类型终端注册，则注册失败，关闭连接
            foreach (var item in term_table) {
                if ((item.sdata as TermUnit).CCID.Equals(term.CCID)) {
                    sess.sock.Send(StructPack(reg_term,
                        Encoding.ASCII.GetBytes("Register term failed: Similar term already registered!")));
                    sess.eof = true;
                    return;
                }
            }
            // 发送注册成功信息
            sess.sock.Send(StructPack(reg_term, Encoding.ASCII.GetBytes("Register term success!")));

            // 更新终端表，必须放在验证之后
            sess.sdata = term;

            // 获取对应的svc
            foreach (var item in svc_table) {
                if ((item.sdata as SvcUnit).TermInfo.Equals(new string(reg_term.info))) {
                    term.Svc = item;
                    break;
                }
            }
        }

        private void cnt_send_term(SockSess sess)
        {
            SockPack.CntSendTerm send_term = (SockPack.CntSendTerm)SockConvert.BytesToStruct(sess.rdata, typeof(SockPack.CntSendTerm));

            sess.rdata[0] = (byte)SockPack.PackName.term_handle;
            sess.rdata[1] = (byte)SockPack.PackName.term_handle >> 8;

            foreach (var item in term_table) {
                if ((item.sdata as TermUnit).CCID.Equals(new string(send_term.ccid))) {
                    item.sock.Send(sess.rdata, sess.rdata_size, System.Net.Sockets.SocketFlags.None);
                    break;
                }
            }
        }

        private void cnt_info_term(SockSess sess)
        {
            SockPack.CntInfoTerm info_term = (SockPack.CntInfoTerm)SockConvert.BytesToStruct(sess.rdata, typeof(SockPack.CntInfoTerm));
            info_term.hdr.name++;

            try {
                byte[] buffer = new byte[4096];
                MemoryStream memoryStream = new MemoryStream(buffer);
                XmlSerializer xmlFormat = new XmlSerializer(typeof(List<TerminalBase>));

                List<TerminalBase> list = new List<TerminalBase>();
                if (new string('0', info_term.ccid.Length).Equals(new string(info_term.ccid))) {
                    foreach (var item in term_table)
                        list.Add((item.sdata as TermUnit).ToBase());
                }
                else {
                    foreach (var item in term_table) {
                        if ((item.sdata as TermUnit).CCID.Equals(new string(info_term.ccid))) {
                            list.Add((item.sdata as TermUnit).ToBase());
                            break;
                        }
                    }
                }
                xmlFormat.Serialize(memoryStream, list);

                sess.sock.Send(StructPack(info_term, buffer.Take((int)memoryStream.Position).ToArray()));
                memoryStream.Close();
            }
            catch (Exception ex) {
                Console.Write(ex.ToString());
            }
        }

        private void cnt_reg_svc(SockSess sess)
        {
            SockPack.CntRegSvc reg_svc = (SockPack.CntRegSvc)SockConvert.BytesToStruct(sess.rdata, typeof(SockPack.CntRegSvc));
            reg_svc.hdr.name++;
            SvcUnit svc = new SvcUnit(reg_svc.term_info);

            // 如果已有相同类型模块注册，则注册失败，关闭连接
            foreach (var item in svc_table) {
                if ((item.sdata as SvcUnit).TermInfo.Equals(svc.TermInfo)) {
                    sess.sock.Send(StructPack(reg_svc,
                        Encoding.ASCII.GetBytes("Register svc failed: Similar svc already registered!")));
                    sess.eof = true;
                    return;
                }
            }
            sess.sock.Send(StructPack(reg_svc, Encoding.ASCII.GetBytes("Register svc success!")));

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
            SockPack.CntLoginUser login_user = (SockPack.CntLoginUser)SockConvert.BytesToStruct(sess.rdata, typeof(SockPack.CntLoginUser));
            login_user.hdr.name++;
            UserUnit user = new UserUnit(login_user.userid, login_user.passwd);

            // verify login


            sess.sdata = user;
        }

        private void cnt_info_user(SockSess sess)
        {
            SockPack.CntInfoUser info_user = (SockPack.CntInfoUser)SockConvert.BytesToStruct(sess.rdata, typeof(SockPack.CntInfoUser));
            info_user.hdr.name++;

            try {
                byte[] buffer = new byte[4096];
                MemoryStream memoryStream = new MemoryStream(buffer);
                XmlSerializer xmlFormat = new XmlSerializer(typeof(List<UserUnit>));

                List<UserUnit> list = new List<UserUnit>();
                if (new string('0', info_user.userid.Length).Equals(new string(info_user.userid))) {
                    foreach (var item in term_table)
                        list.Add(item.sdata as UserUnit);
                }
                else {
                    foreach (var item in term_table) {
                        if ((item.sdata as TermUnit).CCID.Equals(new string(info_user.userid))) {
                            list.Add(item.sdata as UserUnit);
                            break;
                        }
                    }
                }
                xmlFormat.Serialize(memoryStream, list);

                sess.sock.Send(StructPack(info_user, buffer.Take((int)memoryStream.Position).ToArray()));
                memoryStream.Close();
            }
            catch (Exception ex) {
                Console.Write(ex.ToString());
            }
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
