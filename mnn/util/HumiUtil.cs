using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace mnn.util
{
    public class HumiUtil
    {
        public static readonly double C1 = -4.0;               // for 12 Bit 湿度修正公式
        public static readonly double C2 = +0.0405;           // for 12 Bit 湿度修正公式
        public static readonly double C3 = -0.0000028;        // for 12 Bit 湿度修正公式
        public static readonly double T1 = +0.01;             // for 14 Bit @ 5V 温度修正公式
        public static readonly double T2 = +0.00008;

        /// <summary>
        /// 取得相对湿度
        /// 	相对湿度：是绝对湿度与最高湿度之间的比，它的值显示水蒸气的饱和度有多高。
        /// </summary>
        /// <param name="temp"></param>
        /// <param name="humi"></param>
        /// <returns></returns>
        public static double GetXdHumi(double temp, double humi)
        {
            double rh_lin = C3 * humi * humi + C2 * humi + C1;     //相对湿度非线性补偿
            double rh_true = (temp - 25) * (T1 + T2 * humi) + rh_lin;   //相对湿度对于温度依赖性补偿
            if (rh_true > 100)
            {
                rh_true = 100;       //湿度最大修正
            }
            if (rh_true < 0.1)
            {
                rh_true = 0.1;       //湿度最小修正
            }
            return rh_true;
        }

        /// <summary>
        /// 取得绝对湿度值
        /// 	绝对湿度：是一定体积的空气中含有的水蒸气的质量，一般其单位是克/立方米。
        /// </summary>
        /// <param name="h">相对湿度</param>
        /// <param name="t">外部温度</param>
        /// <returns></returns>
        public static double GetJdHumi(double h, double t)
        {
            double logEx = 0.66077 + 7.5 * t / (237.3 + t) + (Math.Log10(h) - 2);
            double dew_point = (logEx - 0.66077) * 237.3 / (0.66077 + 7.5 - logEx);
            return dew_point;
        }
    }
}
