using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.IO;
using System.Xml;

namespace StationConsole
{
    class Program
    {
        public static ControlLayer CLayer;
        public static MainWindow MWindow;

        [STAThread]
        static void Main(string[] args)
        {
            CLayer = new ControlLayer();
            MWindow = new MainWindow();

            App app = new App();
            app.Run(MWindow);
        }
    }
}
