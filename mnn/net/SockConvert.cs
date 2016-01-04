using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace mnn.net {
    public static class SockConvert {

        #region C/C++ struct convert

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

        #endregion C/C++ struct convert

        #region SockMaster's command parse and message display convert

        /// <summary>
        /// 0x3020050040 => new byte[] { 30, 20, 05, 00, 40 }
        /// </summary>
        /// <param name="hexstr"></param>
        /// <returns></returns>
        public static byte[] ParseHexstrToBytes(string hexstr)
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
        /// <param name = "separator"></param>
        /// <returns></returns>
        public static byte[] ParseCmdstrToBytes(string cmd, char separator = ' ')
        {
            cmd = cmd.Replace(@"\" + separator, separator + "0x" + Convert.ToString(separator, 16) + separator);

            string[] str = cmd.Split(separator.ToString().ToArray(), StringSplitOptions.RemoveEmptyEntries);
            //string[] str = cmd.Split(separator);
            byte[] retval = new byte[0];

            foreach (var item in str) {
                if (item.Contains("0x"))
                    retval = retval.Concat(ParseHexstrToBytes(item)).ToArray();
                else
                    retval = retval.Concat(Encoding.UTF8.GetBytes(item)).ToArray();
            }

            //byte[] retval = cmd.Split(' ').Select(
            //    t => Convert.ToInt32(t) / 10 * 16 + Convert.ToInt32(t) % 10
            //    ).Select(t => Convert.ToByte(t)).ToArray();

            return retval;
        }

        /// <summary>
        /// Bytes to String
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static String ParseBytesToString(byte[] data)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var item in data) {
                if (item >= 0x20 && item < 0x7f) {
                    sb.Append(Convert.ToChar(item));
                    continue;
                }
                string s = Convert.ToString(item, 16);
                if (s.Length == 1)
                    s = "0" + s;
                sb.Append("[" + s + "]");
            }
            sb.Replace("][", "");

            return sb.ToString();
        }

        #endregion SockMaster's command parse and message display convert

        public static void InsertSockHeader(SockRequestContentMode mode, ref byte[] buffer)
        {
            if (buffer == null || !Enum.IsDefined(typeof(SockRequestContentMode), mode))
                return;

            short tmp = (short)mode;
            buffer = new byte[] { (byte)(tmp & 0xff), (byte)(tmp >> 8 & 0xff),
                (byte)(0x04 + buffer.Length & 0xff), (byte)(0x04 + buffer.Length >> 8 & 0xff) }
                .Concat(buffer).ToArray();
        }

        public static IDictionary<string, string> ParseUrlQueryParam(string query)
        {
            Dictionary<string, string> dc = new Dictionary<string, string>();

            string[] values = query.Replace("\r\n", "").Split('&');
            foreach (var item in values) {
                int index = item.IndexOf('=');
                if (index == -1) continue;
                dc.Add(item.Substring(0, index), item.Substring(index+1));
                //string[] tmp = item.Split('=');
                //dc.Add(tmp[0], tmp[1]);
            }

            return dc;
        }
    }
}
