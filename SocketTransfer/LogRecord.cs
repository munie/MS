using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace SocketTransfer
{
    public class LogRecord
    {
        public static string baseDirectory = baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

        /// <summary>
        /// 写文件(错误日志)
        /// </summary>
        /// <param name="str"></param>
        public static void writeLog(string str)
        {
            if (!Directory.Exists(baseDirectory + @"\ErrLog")) {
                Directory.CreateDirectory(baseDirectory + @"\ErrLog");
            }

            using (StreamWriter sw = new StreamWriter(baseDirectory + @"\ErrLog\ErrLog" + DateTime.Now.ToString("_yyyy-MM-dd") + ".txt", true)) {
                sw.WriteLine(str);
                sw.WriteLine("---------------------------------------------------------");
                sw.Close();
            }
        }

        public static void WriteInfoLog(string str)
        {
            if (!Directory.Exists(baseDirectory + @"\Log")) {
                Directory.CreateDirectory(baseDirectory + @"\Log");
            }

            using (StreamWriter sw = new StreamWriter(baseDirectory + @"\Log\Log" + DateTime.Now.ToString("_yyyy-MM-dd") + ".txt", true)) {
                sw.WriteLine(str);
                sw.WriteLine("---------------------------------------------------------");
                sw.Close();
            }
        }

        public static void writeLog(Exception ex)
        {
            StringBuilder buf = new StringBuilder("出现异常：" + DateTime.Now.ToString());
            buf.AppendLine("异常类型：" + ex.GetType().Name);
            buf.AppendLine("异常消息：" + ex.Message);
            buf.AppendLine("异常信息：" + ex.StackTrace);
            writeLog(buf.ToString());
        }

        public static string syspath = @"XML\SystemPZ.xml";

        /// <summary>
        /// 写错误日志（自动添加时间）
        /// </summary>
        /// <param name="str"></param>
        public static void writeLog_WithTime(string msg)
        {
            writeLog(DateTime.Now.ToString() + "\r\n" + msg);
        }
    }
}
