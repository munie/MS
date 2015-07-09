using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Configuration;

namespace TransferStation
{
    static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Create socket listener & Start user interface model
            MnnSocket.AsyncSocketListener sckListener = new MnnSocket.AsyncSocketListener();

            // Start data processing model
            DataProcess.DataConvertCenter center = new DataProcess.DataConvertCenter(sckListener);

            Application.Run(new MainFrom(sckListener));
        }
    }
}
