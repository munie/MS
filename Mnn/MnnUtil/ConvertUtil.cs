using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace Mnn.MnnUtil
{
    public class ConvertUtil
    {
        /// 将结构转换为字节数组
        /// 结构对象
        /// 字节数组
        public static byte[] StructToBytes(object obj)
        {
            //得到结构体的大小
            int size = Marshal.SizeOf(obj);
            //创建byte数组
            byte[] bytes = new byte[size];
            //分配结构体大小的内存空间
            IntPtr structPtr = Marshal.AllocHGlobal(size);
            //将结构体拷到分配好的内存空间
            Marshal.StructureToPtr(obj, structPtr, false);
            //从内存空间拷到byte数组
            Marshal.Copy(structPtr, bytes, 0, size);
            //释放内存空间
            Marshal.FreeHGlobal(structPtr);
            //返回byte数组
            return bytes;
        }

        /// byte数组
        /// 结构类型
        /// 转换后的结构
        public static object BytesToStruct(byte[] bytes, Type type)
        {
            //得到结构的大小
            int size = Marshal.SizeOf(type);
            //byte数组长度小于结构的大小
            if (size > bytes.Length) {
                //返回空
                return null;
            }
            //分配结构大小的内存空间
            IntPtr structPtr = Marshal.AllocHGlobal(size);
            //将byte数组拷到分配好的内存空间
            Marshal.Copy(bytes, 0, structPtr, size);
            //将内存空间转换为目标结构
            object obj = Marshal.PtrToStructure(structPtr, type);
            //释放内存空间
            Marshal.FreeHGlobal(structPtr);
            //返回结构
            return obj;
        }

        /// <summary>
        /// 0x3020050040 => new byte[] { 30, 20, 05, 00, 40 }
        /// </summary>
        /// <param name="hexstr"></param>
        /// <returns></returns>
        public static byte[] HexstrToBytes(string hexstr)
        {
            List<byte> list = new List<byte>();

            if (hexstr.Length % 2 != 0)
                hexstr += "0";

            for (int i = 2; i < hexstr.Length; i += 2)
                //list.Add(byte.Parse(hexstr.Substring(i, 2), System.Globalization.NumberStyles.HexNumber));
                list.Add(Convert.ToByte(hexstr.Substring(i, 2), 16));

            return list.ToArray();
        }

        /// <summary>
        /// 0x3020050040 string 0x16 => new byte[] { 30, 20, 05, 00, 40, 73, 74, 72, 69, 6e, 67, 16 }
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public static byte[] CmdstrToBytes(string cmd)
        {
            string[] str = cmd.Split(' ');
            byte[] retval = new byte[0];

            foreach (var item in str) {
                if (item.Contains("0x"))
                    retval = retval.Concat(HexstrToBytes(item)).ToArray();
                else
                    retval = retval.Concat(Encoding.Default.GetBytes(item)).ToArray();
            }

            //byte[] retval = cmd.Split(' ').Select(
            //    t => Convert.ToInt32(t) / 10 * 16 + Convert.ToInt32(t) % 10
            //    ).Select(t => Convert.ToByte(t)).ToArray();

            return retval;
        }

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
