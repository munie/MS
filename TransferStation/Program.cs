using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

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
            // Create socket listener
            MnnSocket.AsyncSocketListener sckListener = new MnnSocket.AsyncSocketListener();
            Application.Run(new MainFrom(sckListener));
        }
    }
}
