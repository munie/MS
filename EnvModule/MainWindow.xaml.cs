using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections.ObjectModel;
using System.Threading;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.Reflection;
using Mnn.MnnSocket;
using Mnn.MnnModule;

namespace EnvModule
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            init();
            config();
            perform_once();
        }

        private SockSessManager sessmgr;
        // rwlock for moduleTable
        private ReaderWriterLock rwlock = new ReaderWriterLock();
        private ObservableCollection<ModuleUnit> moduleTable;
        private ObservableCollection<ClientUnit> clientTable;

        private void init()
        {
            sessmgr = new SockSessManager();
            sessmgr.sess_parse += new SockSessManager.SessParseDelegate(sessmgr_sess_parse);
            moduleTable = new ObservableCollection<ModuleUnit>();
            clientTable = new ObservableCollection<ClientUnit>();
            DataContext = new { ModuleTable = moduleTable, ClientTable = clientTable };

            // 加载 DataHandles 文件夹下的所有模块
            string modulePath = System.AppDomain.CurrentDomain.BaseDirectory + @"\Modules";
            if (Directory.Exists(modulePath)) {
                string[] files = Directory.GetFiles(modulePath);

                // Load dll files one by one
                foreach (var item in files) {
                    if ((item.EndsWith(".dll") || item.EndsWith(".dll")) && item.Contains("Module_")) {
                        AtModuleLoad(item);
                    }
                }
            }
        }

        private void config()
        {
        }

        private void perform_once()
        {
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2000);
            byte[] data = new byte[] { 0x22, 0x20, 0x05, 0x00, 0x40 };
            sessmgr.AddConnectSession(ep);
            sessmgr.SendSession(ep, data);

            //// autorun socket
            //foreach (var sock in SockTable) {
            //    if (sock.Autorun == true)
            //        sock.State = SockUnitState.Opening;
            //}

            // new thread for running socket
            // from now on, we can't call motheds of sessmgr directly in this thread
            Thread thread = new Thread(() =>
            {
                while (true) {
                    perform();
                    sessmgr.Perform(1000);
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        private void perform()
        {
        }

        void sessmgr_sess_parse(object sender, SockSess sess)
        {
            byte[] data = sess.rdata.Take(sess.rdata_size).ToArray();
            sess.rdata_size = 0;

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (txtBoxMsg.Text.Length >= 20 * 1024)
                    txtBoxMsg.Clear();

                StringBuilder sb = new StringBuilder();
                foreach (var item in data) {
                    if (item >= 0x20 && item < 0x7f) {
                        sb.Append(Convert.ToChar(item));
                        continue;
                    }
                    string s = Convert.ToString(item, 16);
                    if (s.Length == 1)
                        s = "0" + s;
                    sb.Append("(" + s + ")");
                }
                sb.Replace(")(", "");

                txtBoxMsg.AppendText(DateTime.Now + " (" +
                    sess.rep.ToString() + " => " + sess.lep.ToString() + ")\n");
                txtBoxMsg.AppendText(sb.ToString() + "\n");
                txtBoxMsg.ScrollToEnd();
            }));


            rwlock.AcquireReaderLock(-1);
            foreach (var item in moduleTable) {
                if (item.Type == Convert.ToInt16(data[1]) && (UInt16)data[2] == data.Length) {
                    try {
                        item.Module.Invoke("Mnn.IDataHandle", "HandleMsgByte", new object[] { null, data });
                    }
                    catch (Exception ex) {
                        Console.Write(ex.ToString());
                    }
                    break;
                }
            }
            rwlock.ReleaseReaderLock();
        }

        public void AtModuleLoad(string filePath)
        {
            ModuleItem module = new ModuleItem();

            try {
                module.Load(filePath);
            }
            catch (Exception ex) {
                MessageBox.Show(ex.Message, "Error");
                return;
            }

            try {
                module.Invoke("Mnn.MnnModule.IModule", "Init", null);
            }
            catch (Exception ex) {
                module.UnLoad();
                MessageBox.Show(ex.Message, "Error");
                return;
            }

            // 加载模块已经成功
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(filePath);
            ModuleUnit moduleUnit = new ModuleUnit();
            moduleUnit.ID = (string)module.Invoke("Mnn.MnnModule.IModule", "GetModuleID", null);
            moduleUnit.Name = fvi.ProductName;
            moduleUnit.Type = (UInt16)module.Invoke("Mnn.MnnModule.IModule", "GetModuleType", null);
            moduleUnit.FilePath = filePath;
            moduleUnit.FileName = module.AssemblyName;
            moduleUnit.FileComment = fvi.Comments;
            moduleUnit.Module = module;

            // 加入 table
            rwlock.AcquireWriterLock(-1);
            moduleTable.Add(moduleUnit);
            rwlock.ReleaseWriterLock();
        }

        public void AtModuleUnload(string fileName)
        {
            rwlock.AcquireWriterLock(-1);

            var subset = from s in moduleTable where s.FileName.Equals(fileName) select s;
            if (subset.Count() != 0) {
                try {
                    subset.First().Module.Invoke("Mnn.MnnModule.IModule", "Final", null);
                }
                catch (Exception) { }
                // 卸载模块
                subset.First().Module.UnLoad();
                // 移出 table
                moduleTable.Remove(subset.First());
            }

            rwlock.ReleaseWriterLock();
        }

    }
}
