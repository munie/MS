using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mnn.MnnMisc.MnnEnv
{
    /// <summary>
    /// Socket数据的key值
    /// </summary>
    public class MsgKey
    {
        /// <summary>
        /// 数据处理类型判断标志
        /// </summary>
        public static string HT = "HT";

        /// <summary>
        /// CCID
        /// </summary>
        public static string CCID = "CCID";

        /// <summary>
        /// 信号强度
        /// </summary>
        public static readonly string CSQ = "csq";

        /// <summary>
        /// 电压
        /// </summary>
        public static readonly string VOLTAGE = "voltage";

        /// <summary>
        /// 经度
        /// </summary>
        public static readonly string GPS_E = "GPS_E";

        /// <summary>
        /// 纬度
        /// </summary>
        public static readonly string GPS_N = "GPS_N";

        /// <summary>
        /// 警报
        /// </summary>
        public static readonly string ALARM = "ALARM";
    }
}
