using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace mnn.net.deprecated {
    public class Logger {
        public static readonly string BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        public static readonly string LogDirectory = BaseDirectory + @"\logs\";
        public static readonly string ErrDirectory = BaseDirectory + @"\errors\";

        public static void Write(string log, string prefix = "")
        {
            if (!Directory.Exists(LogDirectory))
                Directory.CreateDirectory(LogDirectory);

            if (!String.IsNullOrEmpty(prefix)) prefix += "_";
            string fileName = LogDirectory + prefix + DateTime.Now.ToString("yyyy-MM-dd") + ".log";

            try {
                using (StreamWriter sw = new StreamWriter(fileName, true)) {
                    sw.WriteLine(DateTime.Now.ToString());
                    sw.WriteLine(log);
                    sw.WriteLine("---------------------------------------------------------");
                    sw.Close();
                }
            } catch (Exception) { }
        }

        public static void WriteException(Exception ex, string prefix = "")
        {
            if (!Directory.Exists(ErrDirectory))
                Directory.CreateDirectory(ErrDirectory);

            if (!String.IsNullOrEmpty(prefix)) prefix += "_";
            string fileName = ErrDirectory + prefix + DateTime.Now.ToString("yyyy-MM-dd") + ".log";

            try {
                using (StreamWriter sw = new StreamWriter(fileName, true)) {
                    sw.WriteLine(DateTime.Now.ToString());
                    sw.WriteLine(ex.ToString());
                    sw.WriteLine("---------------------------------------------------------");
                    sw.Close();
                }
            } catch (Exception) { }
        }
    }
}
