using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Mnn.MnnUtil
{
    public class Logger
    {
        public static readonly string BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        public static readonly string LogDirectory = BaseDirectory + @"\Log\";
        public static readonly string ErrorDirectory = BaseDirectory + @"\ErrLog\";

        public static void Write(string log, string prefix = @"Log")
        {
            if (!Directory.Exists(LogDirectory))
                Directory.CreateDirectory(LogDirectory);

            string fileName = LogDirectory + prefix + "_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt";

            try {
                using (StreamWriter sw = new StreamWriter(fileName, true)) {
                    sw.WriteLine(DateTime.Now.ToString());
                    sw.WriteLine(log);
                    sw.WriteLine("---------------------------------------------------------");
                    sw.Close();
                }
            }
            catch (Exception) { }
        }

        public static void WriteException(Exception ex, string prefix = @"ErrLog")
        {
            if (!Directory.Exists(ErrorDirectory))
                Directory.CreateDirectory(ErrorDirectory);

            string fileName = ErrorDirectory + prefix + "_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt";

            try {
                using (StreamWriter sw = new StreamWriter(fileName, true)) {
                    sw.WriteLine(DateTime.Now.ToString());
                    sw.WriteLine(ex.ToString());
                    sw.WriteLine("---------------------------------------------------------");
                    sw.Close();
                }
            }
            catch (Exception) { }
        }
    }
}
