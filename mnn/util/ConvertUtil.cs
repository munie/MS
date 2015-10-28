using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace mnn.util
{
    public class ConvertUtil
    {
        public static double CDouble(String numStr, int digits)
        {
            try
            {
                return Math.Round(double.Parse(numStr), digits, MidpointRounding.AwayFromZero);
            }
            catch (Exception)
            {
                //Program.writeLog(e);
                //Program.writeLog_WithTime("numStr:" + numStr);
                return 0;
            }
        }

        public static double CDouble(double num, int digits)
        {
            try
            {
                return Math.Round(num, digits, MidpointRounding.AwayFromZero);
            }
            catch (Exception)
            {
                //Program.writeLog(e);
                return 0;
            }
        }

        public static string CDoubleStr(String numStr, int digits)
        {
            return PadDigitZero("" + CDouble(numStr, digits), digits);
        }

        public static string PadDigitZero(String numStr, int digits)
        {
            if (numStr == null)
            {
                numStr = "";
            }

            int dotIdx = numStr.IndexOf(".");
            if (dotIdx == -1)
            {
                numStr = numStr + ".";
            }
            int curDigits = 0;
            dotIdx = numStr.IndexOf(".");
            StringBuilder buf = new StringBuilder(numStr);
            if (dotIdx < numStr.Length - 1)
            {
                curDigits = numStr.Length - 1 - dotIdx;
            }

            while (curDigits < digits)
            {
                buf.Append("0");
                curDigits += 1;
            }
            return buf.ToString();
        }

        public static string CDoubleStr(double num, int digits)
        {
            return "" + CDouble(num, digits);
        }

        public static byte[] StrToToHexByte(string hexString)
        {

            hexString = hexString.Replace(" ", "");

            if ((hexString.Length % 2) != 0)
            {
                hexString += " ";
            }
            byte[] returnBytes = new byte[hexString.Length / 2];

            for (int i = 0; i < returnBytes.Length; i++)
            {
                returnBytes[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
            }
            return returnBytes;
        }

        /// <summary>
        /// 取得相减后的绝对值
        /// </summary>
        /// <param name="val1"></param>
        /// <param name="val2"></param>
        /// <returns></returns>
        public static double GetMinusAbs(string val1, string val2)
        {
            return Math.Abs((double.Parse(val1) - double.Parse(val2)));
        }

        public static int CInt(object val)
        {
            if (val == null)
            {
                return 0;
            }

            try
            {
                return int.Parse(val.ToString());
            }
            catch (Exception ex)
            {
                //Program.writeLog_WithTime(string.Format("字符串{0}转换为int类型时出错！", val.ToString()));
                throw ex;
            }
        }

        public static string CStr(object obj)
        {
            if (obj == null)
            {
                return string.Empty;
            }
            return obj.ToString();
        }

        public static float CFloat(object val)
        {
            if (val == null)
            {
                return 0;
            }

            try
            {
                return float.Parse(val.ToString());
            }
            catch (Exception)
            {
                //Program.writeLog(ex);
                return 0;
            }
        }

        public static double CDouble(object val)
        {
            if (val == null)
            {
                return 0;
            }

            try
            {
                return double.Parse(val.ToString());
            }
            catch (Exception)
            {
                //Program.writeLog(ex);
                return 0;
            }
        }

        /// <summary>
        /// 十六进制转十进制
        /// </summary>
        /// <param name="hex"></param>
        /// <returns></returns>
        public static int HexTo10(string hex)
        {
            return Convert.ToInt32(hex, 16);
        }
    }
}
