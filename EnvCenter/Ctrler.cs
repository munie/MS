using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Runtime.InteropServices;
using Mnn.MnnSock;

namespace EnvCenter
{
    enum SessType
    {
        admin = 0x00,
        module = 0x20,
        term = 0x40,
        term_sk = 0x40,
        term_zls = 0x42,
        term_qx = 0x46,
        term_dxs = 0x48,
    }

    class SessData
    {
        public SessType Type { get; set; }
        public SessType HandleType { get; set; }
        public string CCID { get; set; }

        public SessData(SessType type)
        {
            this.Type = type;
            HandleType = 0;
            CCID = null;
        }
    }

    class Ctrler
    {
        SockSessManager sessmgr;

        public Ctrler(SockSessManager sessmgr)
        {
            this.sessmgr = sessmgr;
            sessmgr.sess_parse += new SockSessManager.SessParseDelegate(sessmgr_sess_parse);
            sessmgr.AddListenSession(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 2000));
            sessmgr.AddListenSession(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 3002));
        }

        // Methods ======================================================================

        private void sessmgr_sess_parse(object sender, SockSess sess)
        {
            // 兼容老版本字符流
            if (sess.rdata[0] == '|' && sess.rdata[1] == 'H' && sess.rdata[2] == 'T') {
                IDictionary<string, string> dc = AnalyzeString(Encoding.Default.GetString(sess.rdata));

                SessType type;
                if (dc["HT"][0] == 'Z')
                    type = SessType.term_zls;
                else if (dc["HT"][0] == 'Q')
                    type = SessType.term_qx;
                else if (dc["HT"][0] == 'D')
                    type = SessType.term_dxs;
                else
                    type = SessType.term_sk;

                if (sess.sdata == null)
                    sess.sdata = new SessData(type);
                if ((sess.sdata as SessData).CCID == null)
                    (sess.sdata as SessData).CCID = dc["CCID"];

                foreach (var item in FindModuleSession(type))
                    item.sock.Send(sess.rdata, sess.rdata_size, System.Net.Sockets.SocketFlags.None);
                sess.rdata_size = 0;
                return;
            }

            // 验证数据流有效性
            SockMsg.msghdr hdr = (SockMsg.msghdr)SockConvert.BytesToStruct(sess.rdata, typeof(SockMsg.msghdr));
            if (hdr.len != sess.rdata_size) {
                Console.Write("[Error]: Unknow packet from {0}.\n", sess.rep.ToString());
                sess.rdata_size = 0;
                return;
            }

            // 初始化SessData
            if (sess.sdata == null)
                sess.sdata = new SessData((SessType)hdr.id_type);
            SessData sd = sess.sdata as SessData;

            // 根据SessType，调用合适的处理方法
            switch (sd.Type) {
                case SessType.admin: AdminParse(sess); break;
                case SessType.module: ModuleParse(sess, hdr); break;
                case SessType.term_sk:
                case SessType.term_zls: 
                case SessType.term_qx:
                case SessType.term_dxs: TermParse(sess); break;
                default: break;
            }

            // 数据流处理完成
            sess.rdata_size = 0;
        }

        private void AdminParse(SockSess sess)
        {
        }

        private void ModuleParse(SockSess sess, SockMsg.msghdr hdr)
        {
            SessData sd = sess.sdata as SessData;

            switch (hdr.msg_type) {
                // 心跳
                case (byte)SockMsg.MsgType.alive: break;
                // 注册目标term类型
                case (byte)SockMsg.MsgType.register: sd.HandleType = (SessType)sess.rdata[4]; break;
                // 转发
                case (byte)SockMsg.MsgType.trans:
                    SockMsg.termhdr thdr = (SockMsg.termhdr)SockConvert.BytesToStruct(sess.rdata, typeof(SockMsg.termhdr));
                    foreach (var item in FindTermSession(new string(thdr.ccid)))
                        item.sock.Send(sess.rdata, Marshal.SizeOf(thdr),
                            sess.rdata_size - Marshal.SizeOf(thdr),
                            System.Net.Sockets.SocketFlags.None);
                    break;
            }
        }

        private void TermParse(SockSess sess)
        {
            // 数据流转换
            SockMsg.termhdr thdr = (SockMsg.termhdr)SockConvert.BytesToStruct(sess.rdata, typeof(SockMsg.termhdr));

            // 保存ccid
            if ((sess.sdata as SessData).CCID == null)
                (sess.sdata as SessData).CCID = new string(thdr.ccid);

            // 迭代sess_table，找到对应的处理模块，发送数据
            foreach (var item in FindModuleSession((SessType)thdr.hdr.id_type))
                item.sock.Send(sess.rdata, sess.rdata_size, System.Net.Sockets.SocketFlags.None);
        }

        private SockSess[] FindModuleSession(SessType type)
        {
            List<SockSess> retval = new List<SockSess>();

            var subset = from s in sessmgr.sess_table
                         where s.sdata != null && (s.sdata as SessData).HandleType == type
                         select s;
            foreach (var item in subset)
                retval.Add(item);

            return retval.ToArray();
        }

        private SockSess[] FindTermSession(string ccid)
        {
            List<SockSess> retval = new List<SockSess>();

            var subset = from s in sessmgr.sess_table
                         where s.sdata != null && (s.sdata as SessData).CCID != null
                         && (s.sdata as SessData).CCID.Equals(ccid)
                         select s;
            foreach (var item in subset)
                retval.Add(item);

            return retval.ToArray();
        }

        /// <summary>
        /// 字典：字符与值对应
        /// </summary>
        /// <param name="mes"></param>
        /// <returns></returns>
        public static IDictionary<string, string> AnalyzeString(string mes)
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
