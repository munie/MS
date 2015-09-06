using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Mnn.MnnUtil
{
    public class RegUtil
    {
        /// <summary>
        /// 经纬度正则表达式
        /// </summary>
        public static readonly Regex LonLatReg = new Regex(@"^[\d\.]+$");

        /// <summary>
        /// 气压正则表达式
        /// </summary>
        public static readonly Regex PreReg = new Regex(@"^[+-]{1}[\d]{4}\.[\d]{2}$");

        /// <summary>
        /// 是否为经纬度
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static bool IsLonLat(string str)
        {
            return LonLatReg.IsMatch(str);
        }

        /// <summary>
        /// 是否为气压
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static bool IsPre(string str)
        {
            return PreReg.IsMatch(str);
        }
    }
}
