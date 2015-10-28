using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace mnn.util
{
    public class DegreeUtil
    {
        /// <summary>
        /// 取得经纬度（xxx.xxxxx）
        /// </summary>
        /// <param name="degrGPS"></param>
        /// <param name="headLen"></param>
        /// <returns></returns>
        public static string GetDegree(string degrGPS, int headLen)
        {
            return (decimal.Parse(degrGPS.Substring(0, headLen)) + decimal.Round((decimal.Parse(degrGPS.Substring(headLen)) / 60), 6)).ToString();
        }

        /// <summary>
        /// 计算经度 中文
        /// </summary>
        /// <param name="GPS_E"></param>
        /// <returns></returns>
        public static string GetLongitude(string GPS_E)
        {
            try
            {
                double a = double.Parse(GPS_E) * 1000;
                int all = Convert.ToInt32(a);
                int du = all / 100000;
                int feng = (all % 100000) / 1000;
                int miao = (((all % 100000) % 1000) * 60) / 1000;
                return du + "度" + feng + "分" + miao + "秒";
            }
            catch (Exception ex)
            {
                //Program.writeLog_WithTime("经度计算出错，GPS_E=" + GPS_E);
                throw ex;
            }
        }

        /// <summary>
        /// 计算纬度 中文
        /// </summary>
        /// <param name="GPS_E"></param>
        /// <returns></returns>
        public static string GetLatitude(string GPS_N)
        {
            return GetLongitude(GPS_N);
        }
    }
}
