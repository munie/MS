using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace mnn.misc.env
{
    public class MsgCmd
    {
        /// <summary>
        /// 回复心跳指令
        /// </summary>
        public static readonly string ReplyHeartBeat = "*OK#";

        /// <summary>
        /// 测试指令
        /// </summary>
        public static readonly string Test = "!A0#";

        /// <summary>
        /// 取数据
        /// </summary>
        public static readonly string FetchData = "!A1?";

        /// <summary>
        /// 关机指令
        /// </summary>
        public static readonly string PowerOff = "!OFF";

    }
}
