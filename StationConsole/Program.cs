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
        public static DataLayer DLayer;
        public static ControlLayer CLayer;
        public static MainWindow MWindow;

        [STAThread]
        static void Main(string[] args)
        {
            DLayer = new DataLayer();
            CLayer = new ControlLayer();
            MWindow = new MainWindow();

            if (File.Exists(System.AppDomain.CurrentDomain.BaseDirectory + @"\config.xml") == false) {
                System.Windows.MessageBox.Show("未找到配置文件： config.xml");
                return;
            }

            /// ** Initialize DataLayer Start ====================================================
            DLayer.atCmdServerConfigTable = new List<AtCmdServerConfig>();
            DLayer.dataHandlePluginTable = new ObservableCollection<DataHandlePlugin>();
            DLayer.clientPointTable = (ClientPointTable)MWindow.Resources["clientPointTable"];

            try {
                XmlDocument xdoc = new XmlDocument();
                xdoc.Load(System.AppDomain.CurrentDomain.BaseDirectory + @"\config.xml");

                // coding Config
                XmlNode node = xdoc.SelectSingleNode("/configuration/encoding");
                DLayer.coding = Encoding.GetEncoding(node.InnerText);

                // ipAddress Config
                node = xdoc.SelectSingleNode("/configuration/ipaddress");
                DLayer.ipAddress = System.Net.IPAddress.Parse(node.InnerText);

                // AtCmdServer Config
                foreach (XmlNode item in xdoc.SelectNodes("/configuration/atcmdserver")) {
                    AtCmdServerConfig config = new AtCmdServerConfig();
                    if (item.Attributes["protocol"].Value == "udp" ||
                        item.Attributes["protocol"].Value == "tcp") {
                        config.Protocol = item.Attributes["protocol"].Value;
                        config.IpAddress = item.Attributes["ipaddress"].Value;
                        config.Port = item.Attributes["port"].Value;
                    }
                    else if (item.Attributes["protocol"].Value == "pipe") {
                        config.Protocol = "pipe";
                        config.PipeName = item.Attributes["pipename"].Value;
                    }
                    DLayer.atCmdServerConfigTable.Add(config);
                }
            }
            catch (Exception ex) {
                Mnn.MnnUtil.Logger.WriteException(ex);
                System.Windows.MessageBox.Show("配置文件读取错误： config.xml");
                return;
            }
            /// ** Initialize DataLayer End ====================================================

            CLayer.Run();

            // Data Source Binding
            //lstViewClientPoint.ItemsSource = Program.dataLayer.clientPointTable;
            MWindow.lstViewDataHandle.ItemsSource = Program.DLayer.dataHandlePluginTable;

            App app = new App();
            app.Run(MWindow);
        }
    }
}
