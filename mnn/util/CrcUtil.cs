using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace mnn.util
{
    public class CrcUtil {
        #region Myu CRC16
        //public static byte[] MyuCrc16(byte[] target, ushort poly)
        //{
        //    int tmp = 0;
        //    int _poly = poly << 8;

        //    target = target.Concat(new byte[] { 0, 0 }).ToArray();
        //    foreach (var item in target) {
        //        tmp += item;
        //        for (int i = 0; i < 8; i++) {
        //            tmp <<= 1;
        //            if ((tmp & 0x1000000) == 0)
        //                continue;
        //            tmp ^= _poly;
        //        }
        //    }
        //    tmp >>= 8;

        //    return new byte[] { (byte)((tmp & 0xff00) >> 8), (byte)(tmp & 0xff) };
        //}

        public static byte[] MyuCrc16(byte[] target)
        {
            int crc = 0xffff;
            ushort poly = 0xa001;
            int cbit = 0;

            foreach (var item in target) {
                crc ^= item;
                for (int i = 0; i < 8; i++) {
                    cbit = crc & 0x1;
                    crc >>= 1;
                    if (cbit == 1)
                        crc ^= poly;
                }
            }

            return new byte[] { (byte)(crc & 0xff), (byte)((crc & 0xff00) >> 8) };
        }
        #endregion

        #region CRC16校验
        /// <summary>
        /// CRC16校验算法,低字节在前，高字节在后
        /// </summary>
        /// <param name="data">要校验的数组</param>
        /// <returns>返回校验结果，低字节在前，高字节在后</returns>
        public static int[] Crc16(int[] data)
        {
            if (data.Length == 0)
                throw new Exception("调用CRC16校验算法,（低字节在前，高字节在后）时发生异常，异常信息：被校验的数组长度为0。");
            int[] temdata = new int[data.Length + 2];
            int xda, xdapoly;
            int i, j, xdabit;
            xda = 0xFFFF;
            xdapoly = 0xA001;
            for (i = 0; i < data.Length; i++)
            {
                xda ^= data[i];
                for (j = 0; j < 8; j++)
                {
                    xdabit = (int)(xda & 0x01);
                    xda >>= 1;
                    if (xdabit == 1)
                        xda ^= xdapoly;
                }
            }
            temdata = new int[2] { (int)(xda & 0xFF), (int)(xda >> 8) };
            return temdata;
        }

        /// <summary>
        ///CRC16校验算法,（低字节在前，高字节在后）
        /// </summary>
        /// <param name="data">要校验的数组</param>
        /// <returns>返回校验结果，低字节在前，高字节在后</returns>
        public static byte[] Crc16(byte[] data)
        {
            if (data.Length == 0)
            {
                throw new Exception("调用CRC16校验算法,（低字节在前，高字节在后）时发生异常，异常信息：被校验的数组长度为0。");
            }
            byte[] temdata = new byte[data.Length + 2];
            int xda, xdapoly;
            int i, j, xdabit;
            xda = 0xFFFF;
            xdapoly = 0xA001;
            for (i = 0; i < data.Length; i++)
            {
                xda ^= data[i];
                for (j = 0; j < 8; j++)
                {
                    xdabit = (byte)(xda & 0x01);
                    xda >>= 1;
                    if (xdabit == 1)
                        xda ^= xdapoly;
                }
            }
            temdata = new byte[2] { (byte)(xda & 0xFF), (byte)(xda >> 8) };
            return temdata;
        }

        /// <summary>
        /// 
        /// 测试hexStr： "010438" + "03FC00DD037F04F0000000000025000000230000002700006993000200000019FFEB033303A8001500000001000000080000000000000000";
        /// 结果：D7D0
        /// </summary>
        /// <param name="hexStr"></param>
        /// <returns></returns>
        public static string GetCrc16Str(string hexStr)
        {
            byte[] byteArray = ConvertUtil.StrToToHexByte(hexStr);
            byte[] crcByteArr = Crc16(byteArray);
            return ((padZero(Convert.ToString(crcByteArr[0], 16)) + padZero(Convert.ToString(crcByteArr[1], 16)))).ToUpper();
        }
        #endregion

        public static string padZero(string src)
        {
            if (src.Length == 1)
            {
                return "0" + src;
            }
            return src;
        }

        /// <summary>
        /// Crc16校验是否通过
        /// </summary>
        /// <param name="hexStr">16进制待校验字符串</param>
        /// <returns></returns>
        public static bool IsCrc16CheckPass(string hexStr)
        {
            //总长度验证
            if (hexStr == null || hexStr.Length < 5)
            {
                return false;
            }

            return hexStr.Substring(hexStr.Length - 4) == CrcUtil.GetCrc16Str(hexStr.Substring(0, hexStr.Length - 4));
        }
    }
}
