using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Runtime.InteropServices;
using Mnn.MnnSock;

namespace EnvCenter
{
    enum EnvSessType : byte
    {
        admin = 0x00,
        module = 0x20,
        term = 0x40,
        term_sk = 0x40,
        term_zls = 0x42,
        term_qx = 0x46,
        term_dxs = 0x48,
    }

    class EnvSessData
    {
        public EnvSessType Type { get; set; }
        public EnvSessType ServeType { get; set; }
        public string CCID { get; set; }

        public EnvSessData(EnvSessType type)
        {
            this.Type = type;
            ServeType = 0;
            CCID = null;
        }
    }

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
            sessmgr.AddListenSession(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 2000));
            sessmgr.AddListenSession(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 3000));
            sessmgr.AddListenSession(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 3002));
            sessmgr.AddListenSession(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 3006));
            sessmgr.AddListenSession(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 3008));
        }

        // Parse Methods ======================================================================

        private void sessmgr_sess_parse(object sender, SockSess sess)
        {
            // 兼容老版本字符流
            if (sess.rdata[0] == '|' && sess.rdata[1] == 'H' && sess.rdata[2] == 'T') {
                IDictionary<string, string> dc = AnalyzeString(Encoding.Default.GetString(sess.rdata));

                EnvSessType type;
                if (dc["HT"][0] == 'Z')
                    type = EnvSessType.term_zls;
                else if (dc["HT"][0] == 'Q')
                    type = EnvSessType.term_qx;
                else if (dc["HT"][0] == 'D')
                    type = EnvSessType.term_dxs;
                else
                    type = EnvSessType.term_sk;

                if (sess.sdata == null)
                    sess.sdata = new EnvSessData(type);
                if ((sess.sdata as EnvSessData).CCID == null)
                    (sess.sdata as EnvSessData).CCID = dc["CCID"];

                foreach (var item in FindModuleSession(type))
                    item.sock.Send(sess.rdata, sess.rdata_size, System.Net.Sockets.SocketFlags.None);
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

            switch (hdr.name) {
                case SockPack.PackName.alive:
                    break;
                case SockPack.PackName.term_register:
                    SockPack.TermRegister tr = (SockPack.TermRegister)SockConvert.BytesToStruct(sess.rdata, typeof(SockPack.TermRegister));
                    TermUnit term = new TermUnit(tr.ccid, tr.info, null);
                    foreach (var item in svc_table) {
                        if ((item.sdata as SvcUnit).TermInfo.Equals(new string(tr.info))) {
                            term.Svc = item;
                            break;
                        }
                    }
                    sess.sdata = term;
                    break;
                case SockPack.PackName.term_request:
                    if (sess.sdata == null || sess.sdata as TermUnit == null || (sess.sdata as TermUnit).Svc == null)
                        break;
                    (sess.sdata as TermUnit).Svc.sock.Send(sess.rdata, sess.rdata_size, System.Net.Sockets.SocketFlags.None);
                    break;
                case SockPack.PackName.svc_register:
                    SockPack.SvcRegister sr = (SockPack.SvcRegister)SockConvert.BytesToStruct(sess.rdata, typeof(SockPack.SvcRegister));
                    sess.sdata = new SvcUnit(sr.term_info, sess);
                    break;
                case SockPack.PackName.svc_trans:
                    break;
                default:
                    break;
            }

            //// 初始化SessData
            //if (sess.sdata == null)
            //    sess.sdata = new EnvSessData((EnvSessType)hdr.id_type);
            //EnvSessData sd = sess.sdata as EnvSessData;

            //// 根据SessType，调用合适的处理方法
            //switch (sd.Type) {
            //    case EnvSessType.admin: AdminParse(sess); break;
            //    case EnvSessType.module: ModuleParse(sess, hdr); break;
            //    case EnvSessType.term_sk:
            //    case EnvSessType.term_zls: 
            //    case EnvSessType.term_qx:
            //    case EnvSessType.term_dxs: TermParse(sess); break;
            //    default: break;
            //}

            // 数据流处理完成
            sess.rdata_size = 0;
        }

        /// <summary>
        /// 1.登录EnvCenter
        /// 2.获取终端列表，能自动更新
        /// 3.获取终端发送的数据
        /// 4.向终端发送命令
        /// 5.关闭终端
        /// </summary>
        /// <param name="sess"></param>
        private void AdminParse(SockSess sess)
        {
        }

        /// <summary>
        /// 1.向EnvCenter进行模块注册
        /// 2.通过EnvCenter转发回复、命令到终端
        /// </summary>
        /// <param name="sess"></param>
        /// <param name="hdr"></param>
        private void ModuleParse(SockSess sess, SockPack.PackHeader hdr)
        {
            EnvSessData sd = sess.sdata as EnvSessData;

            switch (hdr.name) {
                // 心跳
                case SockPack.PackName.alive: break;
                // 注册目标term类型
                case SockPack.PackName.svc_register: sd.ServeType = (EnvSessType)sess.rdata[4]; break;
                // 转发
                case SockPack.PackName.svc_trans:
                    SockPack.TermRequest thdr = (SockPack.TermRequest)SockConvert.BytesToStruct(sess.rdata, typeof(SockPack.TermRequest));
                    foreach (var item in FindTermSession(new string(thdr.ccid)))
                        item.sock.Send(sess.rdata, Marshal.SizeOf(thdr),
                            sess.rdata_size - Marshal.SizeOf(thdr),
                            System.Net.Sockets.SocketFlags.None);
                    break;
            }
        }

        /// <summary>
        /// 将终端数据请求到对应的模块服务程序，通过EnvSessData中的ServeType决定
        /// </summary>
        /// <param name="sess"></param>
        private void TermParse(SockSess sess)
        {
            // 数据流转换
            SockPack.TermRequest thdr = (SockPack.TermRequest)SockConvert.BytesToStruct(sess.rdata, typeof(SockPack.TermRequest));

            // 保存ccid
            if ((sess.sdata as EnvSessData).CCID == null)
                (sess.sdata as EnvSessData).CCID = new string(thdr.ccid);

            // 迭代sess_table，找到对应的处理模块，发送数据
            foreach (var item in FindModuleSession((EnvSessType)thdr.hdr.id_type))
                item.sock.Send(sess.rdata, sess.rdata_size, System.Net.Sockets.SocketFlags.None);
        }

        // Self Methods =================================================================

        private SockSess[] FindModuleSession(EnvSessType type)
        {
            List<SockSess> retval = new List<SockSess>();

            var subset = from s in sessmgr.sess_table
                         where s.sdata != null && (s.sdata as EnvSessData).ServeType == type
                         select s;
            foreach (var item in subset)
                retval.Add(item);

            return retval.ToArray();
        }

        private SockSess[] FindTermSession(string ccid)
        {
            List<SockSess> retval = new List<SockSess>();

            var subset = from s in sessmgr.sess_table
                         where s.sdata != null && (s.sdata as EnvSessData).CCID != null
                         && (s.sdata as EnvSessData).CCID.Equals(ccid)
                         select s;
            foreach (var item in subset)
                retval.Add(item);

            return retval.ToArray();
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
