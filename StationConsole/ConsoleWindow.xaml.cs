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
using StationConsole.CtrlLayer;

namespace StationConsole
{
    /// <summary>
    /// ConsoleWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ConsoleWindow : Window
    {
        public ConsoleWindow()
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

        private void Window_Closed(object sender, EventArgs e)
        {
            (this.Owner as MainWindow).Close();
        }

        public void ClienConnect()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                currentClientCount.Text = (Convert.ToInt32(currentClientCount.Text) + 1).ToString();
                historyClientOpenCount.Text = (Convert.ToInt32(historyClientOpenCount.Text) + 1).ToString();
            }));
        }

        public void ClientDisconn()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                currentClientCount.Text = (Convert.ToInt32(currentClientCount.Text) - 1).ToString();
                historyClientCloseCount.Text = (Convert.ToInt32(historyClientCloseCount.Text) + 1).ToString();
            }));
        }

        public void ClientUpdate(IPEndPoint ep, string fieldName, object value)
        {
            //Type t = typeof(ClientUnit);
            //PropertyInfo propertyInfo = t.GetProperty(fieldName);

            //Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            //{
            //    var subset = from s in this.clientStateTable
            //                 where s.RemoteEP.Equals(ep)
            //                 select s;

            //    if (subset.Count() != 0)
            //        propertyInfo.SetValue(subset.First(), value, null);
            //}));
        }

        public void MessageAppend()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                currentMsgCount.Text = (Convert.ToInt32(currentMsgCount.Text) + 1).ToString();
                historyMsgFetchCount.Text = (Convert.ToInt32(historyMsgFetchCount.Text) + 1).ToString();
            }));
        }

        public void MessageRemove()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                currentMsgCount.Text = (Convert.ToInt32(currentMsgCount.Text) - 1).ToString();
                historyMsgHandledCount.Text = (Convert.ToInt32(historyMsgHandledCount.Text) + 1).ToString();
            }));
        }

        public void MessageDisplay(string msg)
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

        private void MenuItem_LoadModule_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();

            openFileDialog.Filter = "dll files (*.dll)|*.dll|All files (*.*)|*.*";
            openFileDialog.FileName = "";

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                (this.Owner as MainWindow).AtModuleLoad(openFileDialog.FileName);
        }

        private void MenuItem_UnloadModule_Click(object sender, RoutedEventArgs e)
        {
            List<ModuleUnitState> handles = new List<ModuleUnitState>();

            // 保存要卸载的模块信息
            foreach (ModuleUnitState item in lstViewModule.SelectedItems) {
                handles.Add(item);
            }

            // 卸载操作
            foreach (var item in handles) {
                (this.Owner as MainWindow).AtModuleUnload(item.FileName);
            }
        }

        private void MenuItem_StartListener_Click(object sender, RoutedEventArgs e)
        {
            foreach (ServerUnitState item in lstViewServer.SelectedItems) {
                if (item.ListenState == ServerUnitState.ListenStateStarted)
                    continue;

                (this.Owner as MainWindow).AtServerStart(item.ID, new IPEndPoint(IPAddress.Parse(item.IpAddress), item.Port));
            }
        }

        private void MenuItem_StopListener_Click(object sender, RoutedEventArgs e)
        {
            foreach (ServerUnitState item in lstViewServer.SelectedItems) {
                if (item.ListenState == ServerUnitState.ListenStateStoped || item.CanStop == false)
                    continue;

                if (item.CanStop == true)
                    (this.Owner as MainWindow).AtServerStop(item.ID);
            }
        }

        private void MenuItem_SetListener_Click(object sender, RoutedEventArgs e)
        {
            using (InputDialog input = new InputDialog()) {
                input.Owner = this;
                input.Title = "设置监听端口";
                input.textBlock1.Text = "其他";
                input.textBlock2.Text = "端口";
                input.textBlock1.IsEnabled = false;
                input.textBox1.IsEnabled = false;
                input.textBox2.Focus();

                if (input.ShowDialog() == false)
                    return;

                foreach (ServerUnitState item in lstViewServer.SelectedItems) {
                    if (item.ListenState == ServerUnitState.ListenStateStarted)
                        continue;

                    item.Port = int.Parse(input.textBox2.Text);
                }
            }
        }

        private void MenuItem_StartTimer_Click(object sender, RoutedEventArgs e)
        {
            foreach (ServerUnitState item in lstViewServer.SelectedItems) {
                if (item.TimerState == ServerUnitState.TimerStateStarted ||
                    item.TimerState == ServerUnitState.TimerStateDisable ||
                    item.TimerInterval <= 0 || item.TimerCommand == "")
                    continue;

                (this.Owner as MainWindow).AtServerTimerStart(item.ID, item.TimerInterval * 1000, item.TimerCommand);
            }
        }

        private void MenuItem_StopTimer_Click(object sender, RoutedEventArgs e)
        {
            foreach (ServerUnitState item in lstViewServer.SelectedItems) {
                if (item.TimerState == ServerUnitState.TimerStateStoped ||
                    item.TimerState == ServerUnitState.TimerStateDisable)
                    continue;

                (this.Owner as MainWindow).AtServerTimerStop(item.ID);
            }
        }

        private void MenuItem_SetTimer_Click(object sender, RoutedEventArgs e)
        {
            using (InputDialog input = new InputDialog()) {
                input.Owner = this;
                input.Title = "设置定时器";
                input.textBlock1.Text = "命令";
                input.textBlock2.Text = "时间间隔";
                input.textBox1.Text = "!A0#";
                input.textBox2.Focus();

                if (input.ShowDialog() == false)
                    return;

                foreach (ServerUnitState item in lstViewServer.SelectedItems) {
                    if (item.TimerState == ServerUnitState.TimerStateStarted)
                        continue;

                    item.TimerCommand = input.textBox1.Text;
                    if (input.textBox2.Text != "")
                        item.TimerInterval = Convert.ToDouble(input.textBox2.Text);
                }
            }
        }

        private void MenuItem_ClientSendMessage_Click(object sender, RoutedEventArgs e)
        {
            using (InputDialog input = new InputDialog()) {
                input.Owner = this;
                input.Title = "发送命令";
                input.textBlock1.Text = "命令";
                input.textBlock2.Text = "时间间隔";
                input.textBlock2.IsEnabled = false;
                input.textBox2.IsEnabled = false;
                input.textBox1.Text = "!A1?";
                input.textBox1.Focus();
                input.textBox1.Select(input.textBox1.Text.Length, 0);

                if (input.ShowDialog() == false)
                    return;

                foreach (ClientUnitState item in lstViewClient.SelectedItems) {
                    (this.Owner as MainWindow).AtClientSendMessage(item.ID, input.textBox1.Text);
                }
            }
        }

        private void MenuItem_ClientClose_Click(object sender, RoutedEventArgs e)
        {
            foreach (ClientUnitState item in lstViewClient.SelectedItems) {
                (this.Owner as MainWindow).AtClientClose(item.ID);
            }
        }
    }
}
