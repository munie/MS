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
using System.ComponentModel;
using System.Net;
using System.Reflection;
using System.Diagnostics;
using System.IO;
using System.Collections.ObjectModel;

namespace StationConsole
{
    class ClientPointTable : ObservableCollection<ClientPointState> { }

    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            InitailizeWindowName();
            InitailizeStatusBar();
        }

        // Methods ============================================================================

        private void InitailizeWindowName()
        {
            // Format Main Form's Name
            Assembly asm = Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(asm.Location);
            this.Title = string.Format("{0} {1}.{2}.{3}-{4} - Powered By {5}",
                fvi.ProductName,
                fvi.ProductMajorPart,
                fvi.ProductMinorPart,
                fvi.ProductBuildPart,
                fvi.ProductPrivatePart,
                fvi.CompanyName);
        }

        private void InitailizeStatusBar()
        {
            // Display TimeRun
            DateTime startTime = DateTime.Now;
            System.Windows.Threading.DispatcherTimer timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = new TimeSpan(0, 0, 1);
            timer.Tick += new EventHandler((s, ea) =>
            {
                blockTimeRun.Text = "运行时间 " + DateTime.Now.Subtract(startTime).ToString(@"dd\-hh\:mm\:ss");

                long memory = GC.GetTotalMemory(false) / 1000;
                long diff = memory - Convert.ToInt32(blockMemory.Text);
                blockMemory.Text = memory.ToString();
                if (diff >= 0)
                    blockMemoryDiff.Text = "+" + diff;
                else
                    blockMemoryDiff.Text = "-" + diff;
            });
            timer.Start();
        }

        public void AddDataHandle(DataHandlePlugin dataHandle)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                lock (Program.DLayer.dataHandlePluginTable) {
                    Program.DLayer.dataHandlePluginTable.Add(dataHandle);
                }
            }));
        }

        public void RemoveDataHandle(DataHandlePlugin dataHandle)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                lock (Program.DLayer.dataHandlePluginTable) {
                    Program.DLayer.dataHandlePluginTable.Remove(dataHandle);
                }
            }));
        }

        public void AddClientPoint(ClientPoint client)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                // set value for ObservableCollection object 
                currentClientCount.Text = (Convert.ToInt32(currentClientCount.Text) + 1).ToString();
                historyClientOpenCount.Text = (Convert.ToInt32(historyClientOpenCount.Text) + 1).ToString();

                ClientPointState state = new ClientPointState(client);
                lock (Program.DLayer.dataHandlePluginTable) {
                    var subset = from s in Program.DLayer.dataHandlePluginTable
                                 where s.ListenPort == client.LocalPort
                                 select s.ChineseName;
                    if (subset.Count() != 0)
                        state.LocalName = subset.First();
                }

                lock (Program.DLayer.clientPointTable) {
                    Program.DLayer.clientPointTable.Add(state);
                }
            }));
        }

        public void RemoveClientPoint(ClientPoint client)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                // set value for ObservableCollection object 
                currentClientCount.Text = (Convert.ToInt32(currentClientCount.Text) - 1).ToString();
                historyClientCloseCount.Text = (Convert.ToInt32(historyClientCloseCount.Text) + 1).ToString();

                lock (Program.DLayer.clientPointTable) {
                    var subset = from s in Program.DLayer.clientPointTable
                                 where s.RemoteIP.Equals(client.RemoteIP)
                                 select s;

                    if (subset.Count() != 0)
                        Program.DLayer.clientPointTable.Remove(subset.First());
                }
            }));
        }

        public void RemoveClientPoint(IPEndPoint ep)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                // set value for ObservableCollection object 
                currentClientCount.Text = (Convert.ToInt32(currentClientCount.Text) - 1).ToString();
                historyClientCloseCount.Text = (Convert.ToInt32(historyClientCloseCount.Text) + 1).ToString();

                lock (Program.DLayer.clientPointTable) {
                    var subset = from s in Program.DLayer.clientPointTable
                                 where s.RemoteIP.Equals(ep.ToString())
                                 select s;

                    if (subset.Count() != 0)
                        Program.DLayer.clientPointTable.Remove(subset.First());
                }
            }));
        }

        public void RemoveClientPoint(string ip)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                // set value for ObservableCollection object 
                currentClientCount.Text = (Convert.ToInt32(currentClientCount.Text) - 1).ToString();
                historyClientCloseCount.Text = (Convert.ToInt32(historyClientCloseCount.Text) + 1).ToString();

                lock (Program.DLayer.clientPointTable) {
                    var subset = from s in Program.DLayer.clientPointTable
                                 where s.RemoteIP.Equals(ip)
                                 select s;

                    if (subset.Count() != 0)
                        Program.DLayer.clientPointTable.Remove(subset.First());
                }
            }));
        }

        public void UpdateClientPoint(ClientPoint client)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                lock (Program.DLayer.clientPointTable) {
                    var subset = from s in Program.DLayer.clientPointTable
                                 where s.RemoteIP.Equals(client.RemoteIP)
                                 select s;

                    if (subset.Count() != 0) {
                        subset.First().CCID = client.CCID;
                        subset.First().Name = client.Name;
                    }
                }
            }));
        }

        public void DisplayMessage(string msg)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (txtMsg.Text.Length >= 20 * 1024) {
                    txtMsg.Clear();
                }

                txtMsg.AppendText(msg + "\r\n\r\n");
                txtMsg.ScrollToEnd();
            }));
        }

        // Events for itself ==================================================================

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 加载 DataHandles 文件夹下的所有模块
            string pluginPath = System.AppDomain.CurrentDomain.BaseDirectory + @"\DataHandles";

            if (Directory.Exists(pluginPath)) {
                string[] files = Directory.GetFiles(pluginPath);

                // Load dll files one by one
                foreach (var item in files)
                    Program.CLayer.AtLoadPlugin(item);
            }
        }

        private void MenuItem_LoadPlugin_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();

            openFileDialog.Filter = "dll files (*.dll)|*.dll|All files (*.*)|*.*";
            openFileDialog.FileName = "";

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                Program.CLayer.AtLoadPlugin(openFileDialog.FileName);
        }

        private void MenuItem_UnloadPlugin_Click(object sender, RoutedEventArgs e)
        {
            List<DataHandlePlugin> handles = new List<DataHandlePlugin>();

            // 保存要卸载的模块信息
            foreach (var item in lstViewDataHandle.SelectedItems) {
                DataHandlePlugin dataHandle = item as DataHandlePlugin;
                if (dataHandle == null)
                    continue;

                handles.Add(dataHandle);
            }

            // 卸载操作
            foreach (var item in handles)
                Program.CLayer.AtUnLoadPlugin(item);
        }

        private void MenuItem_StartListener_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in lstViewDataHandle.SelectedItems) {
                DataHandlePlugin dataHandle = item as DataHandlePlugin;
                if (dataHandle == null)
                    continue;

                Program.CLayer.AtStartListener(dataHandle);
            }
        }

        private void MenuItem_StopListener_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in lstViewDataHandle.SelectedItems) {
                DataHandlePlugin dataHandle = item as DataHandlePlugin;
                if (dataHandle == null)
                    continue;

                Program.CLayer.AtStopListener(dataHandle);
            }
        }

        private void MenuItem_SetListener_Click(object sender, RoutedEventArgs e)
        {
            InputDialog input = new InputDialog();
            input.Owner = this;
            input.Title = "设置监听端口";
            input.textBlock1.Text = "其他";
            input.textBlock2.Text = "端口";
            input.textBlock1.IsEnabled = false;
            input.textBox1.IsEnabled = false;
            input.textBox2.Focus();

            if (input.ShowDialog() == false)
                return;

            foreach (var item in lstViewDataHandle.SelectedItems) {
                DataHandlePlugin dataHandle = item as DataHandlePlugin;
                if (dataHandle == null)
                    continue;

                Program.CLayer.AtSetListener(dataHandle, Convert.ToInt32(input.textBox2.Text));
            }
        }

        private void MenuItem_StartTimer_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in lstViewDataHandle.SelectedItems) {
                DataHandlePlugin dataHandle = item as DataHandlePlugin;
                if (dataHandle == null)
                    continue;

                Program.CLayer.AtStartTimer(dataHandle);
            }
        }

        private void MenuItem_StopTimer_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in lstViewDataHandle.SelectedItems) {
                DataHandlePlugin dataHandle = item as DataHandlePlugin;
                if (dataHandle == null)
                    continue;

                Program.CLayer.AtStopTimer(dataHandle);
            }
        }

        private void MenuItem_SetTimer_Click(object sender, RoutedEventArgs e)
        {
            InputDialog input = new InputDialog();
            input.Owner = this;
            input.Title = "设置定时器";
            input.textBlock1.Text = "命令";
            input.textBlock2.Text = "时间间隔";
            input.textBox1.Text = "!A0#";
            input.textBox2.Focus();

            if (input.ShowDialog() == false)
                return;

            foreach (var item in lstViewDataHandle.SelectedItems) {
                DataHandlePlugin dataHandle = item as DataHandlePlugin;
                if (dataHandle == null)
                    continue;

                if (input.textBox2.Text == "")
                    Program.CLayer.AtSetTimer(dataHandle, input.textBox1.Text, dataHandle.TimerInterval);
                else
                    Program.CLayer.AtSetTimer(dataHandle, input.textBox1.Text, Convert.ToDouble(input.textBox2.Text));
            }
        }

        private void MenuItem_ClientSendMessage_Click(object sender, RoutedEventArgs e)
        {
            InputDialog input = new InputDialog();
            input.Owner = this;
            input.Title = "发送命令";
            input.textBlock1.Text = "命令";
            input.textBlock2.Text = "时间间隔";
            input.textBlock2.IsEnabled = false;
            input.textBox2.IsEnabled = false;
            input.textBox1.Text = "!A0#";
            input.textBox1.Focus();
            input.textBox1.Select(input.textBox1.Text.Length, 0);

            if (input.ShowDialog() == false)
                return;

            foreach (var item in lstViewClientPoint.SelectedItems) {
                ClientPointState client = item as ClientPointState;
                if (client == null)
                    continue;

                Program.CLayer.AtClientSendMessage(client, input.textBox1.Text);
            }
        }

        private void MenuItem_ClientClose_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in lstViewClientPoint.SelectedItems) {
                ClientPointState client = item as ClientPointState;
                if (client == null)
                    continue;

                Program.CLayer.AtClientClose(client);
            }
        }
    }
}
