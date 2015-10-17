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
using System.Configuration;
using Mnn.MnnSock;
using Mnn.MnnMisc.MnnModule;
using Mnn.MnnMisc.MnnEnv;

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
        }

        private SockSessManager sessmgr;
        // rwlock for moduleTable
        private ReaderWriterLock rwlock = new ReaderWriterLock();
        private ObservableCollection<ModuleUnit> moduleTable;
        private ObservableCollection<ClientUnit> clientTable;
        private IPEndPoint ep;

        private void init()
        {
            // 初始化变量
            sessmgr = new SockSessManager();
            sessmgr.sess_parse += new SockSessManager.SessParseDelegate(sessmgr_sess_parse);
            sessmgr.sess_delete += new SockSessManager.SessDeleteDelegate(sessmgr_sess_delete);
            moduleTable = new ObservableCollection<ModuleUnit>();
            clientTable = new ObservableCollection<ClientUnit>();
            DataContext = new { ModuleTable = moduleTable, ClientTable = clientTable };
            ep = new IPEndPoint(IPAddress.Parse(ConfigurationManager.AppSettings["ip"]),
                int.Parse(ConfigurationManager.AppSettings["port"]));

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

            // 尝试连接中心站
            foreach (var item in moduleTable)
                item.State = SockState.Opening;

            // 启动socket线程
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
            foreach (var item in moduleTable) {
                /// ** second handle sending data
                if (item.SendBuffSize != 0 && item.State == SockState.Opened) {
                    sessmgr.SendSession(item.Sock, item.SendBuff);
                    //Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                    item.SendBuffSize = 0;
                    //}));
                }

                /// ** third handle open or close
                if (item.State == SockState.Opening) {
                    if ((item.Sock = sessmgr.AddConnectSession(ep)) != null) {
                        item.State = SockState.Opened;
                        // 向中心站注册
                        byte[] data = new byte[] { 0x20, 0x20, 0x05, 0x00, Convert.ToByte(item.Type) };
                        sessmgr.SendSession(item.Sock, data);
                    }
                    else
                        item.State = SockState.Closed;
                }
                else if (item.State == SockState.Closing) {
                    sessmgr.RemoveSession(item.Sock);
                    item.State = SockState.Closed;
                }
            }
        }

        private void sessmgr_sess_parse(object sender, SockSess sess)
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
                txtBoxMsg.AppendText(sb.ToString() + "\n\n");
                txtBoxMsg.ScrollToEnd();
            }));

            // 兼容老版本字符流
            if (sess.rdata[0] == '|' && sess.rdata[1] == 'H' && sess.rdata[2] == 'T') {
                rwlock.AcquireReaderLock(-1);
                bool IsHandled = false;
                string msgstr = Encoding.Default.GetString(data);
                // 水库代码太恶心，没办法的办法
                // 除水库以外，相对正常数据处理
                foreach (var item in moduleTable) {
                    if (item.ID != "HT=" && msgstr.Contains(item.ID)) {
                        try {
                            item.Module.Invoke(SMsgProc.FullName, SMsgProc.HandleMsg, new object[] { ep, msgstr });
                        }
                        catch (Exception) { }
                        IsHandled = true;
                        break;
                    }
                }
                // 正常代码没有处理，则必是水库数据
                if (IsHandled == false) {
                    foreach (var item in moduleTable) {
                        if (item.ID == "HT=" && msgstr.Contains(item.ID)) {
                            try {
                                item.Module.Invoke(SMsgProc.FullName, SMsgProc.HandleMsg, new object[] { ep, msgstr });
                            }
                            catch (Exception) { }
                            break;
                        }
                    }
                }
                rwlock.ReleaseReaderLock();
                return;
            }

            // 根据data[1]找到对应模块，处理数据
            rwlock.AcquireReaderLock(-1);
            foreach (var item in moduleTable) {
                if (item.Type == Convert.ToInt16(data[1]) && (UInt16)data[2] == data.Length) {
                    try {
                        item.Module.Invoke(SMsgProc.FullName, SMsgProc.HandleMsgByte, new object[] { data });
                    }
                    catch (Exception) { }
                    break;
                }
            }
            rwlock.ReleaseReaderLock();
        }

        private void sessmgr_sess_delete(object sender, SockSess sess)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                foreach (var item in moduleTable) {
                    if (item.Sock == sess.sock) {
                        item.State = SockState.Closed;
                        break;
                    }
                }
            }));
        }

        // Normal methods ===============================================================

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
                module.Invoke(SModule.FullName, SModule.Init, null);
            }
            catch (Exception ex) {
                module.UnLoad();
                MessageBox.Show(ex.Message, "Error");
                return;
            }

            // 加载模块已经成功
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(filePath);
            ModuleUnit moduleUnit = new ModuleUnit();
            moduleUnit.ID = (string)module.Invoke(SModule.FullName, SModule.GetModuleID, null);
            moduleUnit.Name = fvi.ProductName;
            moduleUnit.Type = (UInt16)module.Invoke(SModule.FullName, SModule.GetModuleType, null);
            moduleUnit.State = SockState.Closed;
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
                    subset.First().Module.Invoke(SModule.FullName, SModule.Final, null);
                }
                catch (Exception) { }
                // 卸载模块
                subset.First().Module.UnLoad();
                // 移出 table
                moduleTable.Remove(subset.First());
            }

            rwlock.ReleaseWriterLock();
        }

        // Module Menu ==================================================================

        private void MenuItem_LoadModule_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();

            openFileDialog.Filter = "dll files (*.dll)|*.dll|All files (*.*)|*.*";
            openFileDialog.FileName = "";

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                AtModuleLoad(openFileDialog.FileName);
        }

        private void MenuItem_UnloadModule_Click(object sender, RoutedEventArgs e)
        {
            List<ModuleUnit> handles = new List<ModuleUnit>();

            // 保存要卸载的模块信息
            foreach (ModuleUnit item in lstViewModule.SelectedItems)
                handles.Add(item);

            // 卸载操作
            foreach (var item in handles)
                AtModuleUnload(item.FileName);
        }

        private void MenuItem_OpenModule_Click(object sender, RoutedEventArgs e)
        {
            foreach (ModuleUnit item in lstViewModule.SelectedItems) {
                if (item.State == SockState.Closed)
                    item.State = SockState.Opening;
            }
        }

        private void MenuItem_CloseModule_Click(object sender, RoutedEventArgs e)
        {
            foreach (ModuleUnit item in lstViewModule.SelectedItems) {
                if (item.State == SockState.Opened)
                    item.State = SockState.Closing;
            }
        }

    }
}
