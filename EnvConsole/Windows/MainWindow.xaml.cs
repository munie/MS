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
using System.Threading;
using EnvConsole.Unit;

namespace EnvConsole.Windows
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            Initailize();
            InitailizeWindowName();
            InitailizeStatusBar();
        }

        private CtlCenter center;

        // Methods ============================================================================

        private void Initailize()
        {
            center = new CtlCenter();
            center.Config();
            Thread thread = new Thread(() => { while (true) center.Perform(1000); });
            thread.IsBackground = true;
            thread.Start();

            DataContext = new { ServerTable = center.DataUI.ServerTable, ClientTable = center.DataUI.ClientTable,
                ModuleTable = center.DataUI.ModuleTable, DataUI = center.DataUI };
            this.txtMsg.SetBinding(TextBox.TextProperty, new Binding("DataUI.Log"));
            this.currentClientCount.SetBinding(TextBlock.TextProperty, new Binding("DataUI.CurrentAcceptCount"));
            this.historyClientOpenCount.SetBinding(TextBlock.TextProperty, new Binding("DataUI.HistoryAcceptOpenCount"));
            this.historyClientCloseCount.SetBinding(TextBlock.TextProperty, new Binding("DataUI.HistoryAcceptCloseCount"));
            this.currentPackCount.SetBinding(TextBlock.TextProperty, new Binding("DataUI.CurrentPackCount"));
            this.historyPackFetchedCount.SetBinding(TextBlock.TextProperty, new Binding("DataUI.HistoryPackFetchedCount"));
            this.historyPackParsedCount.SetBinding(TextBlock.TextProperty, new Binding("DataUI.HistoryPackParsedCount"));
        }

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

        // Events for itself ==================================================================

        private void MenuItem_LoadModule_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();

            openFileDialog.Filter = "dll files (*.dll)|*.dll|All files (*.*)|*.*";
            openFileDialog.FileName = "";

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                center.ModuleLoad(openFileDialog.FileName);
        }

        private void MenuItem_UnloadModule_Click(object sender, RoutedEventArgs e)
        {
            List<ModuleUnit> handles = new List<ModuleUnit>();

            // 保存要卸载的模块信息
            foreach (ModuleUnit item in lstViewModule.SelectedItems)
                handles.Add(item);

            // 卸载操作
            foreach (var item in handles)
                center.ModuleUnload(item.FileName);
        }

        private void MenuItem_StartListener_Click(object sender, RoutedEventArgs e)
        {
            foreach (ServerUnit item in lstViewServer.SelectedItems) {
                if (item.ListenState == ServerUnit.ListenStateStarted)
                    continue;

                center.ServerStart(item.IpAddress, item.Port, item.Protocol);
            }
        }

        private void MenuItem_StopListener_Click(object sender, RoutedEventArgs e)
        {
            foreach (ServerUnit item in lstViewServer.SelectedItems) {
                if (item.ListenState == ServerUnit.ListenStateStoped || item.CanStop == false)
                    continue;

                if (item.CanStop == true)
                    center.ServerStop(item.IpAddress, item.Port, item.Protocol);
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

                foreach (ServerUnit item in lstViewServer.SelectedItems) {
                    if (item.ListenState == ServerUnit.ListenStateStarted)
                        continue;

                    item.Port = int.Parse(input.textBox2.Text);
                }
            }
        }

        private void MenuItem_StartTimer_Click(object sender, RoutedEventArgs e)
        {
            foreach (ServerUnit item in lstViewServer.SelectedItems) {
                if (item.TimerState == ServerUnit.TimerStateStarted ||
                    item.TimerState == ServerUnit.TimerStateDisable ||
                    item.TimerInterval <= 0 || item.TimerCommand == "")
                    continue;

                center.ServerTimerStart(item);
            }
        }

        private void MenuItem_StopTimer_Click(object sender, RoutedEventArgs e)
        {
            foreach (ServerUnit item in lstViewServer.SelectedItems) {
                if (item.TimerState == ServerUnit.TimerStateStoped ||
                    item.TimerState == ServerUnit.TimerStateDisable)
                    continue;

                center.ServerTimerStop(item);
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

                foreach (ServerUnit item in lstViewServer.SelectedItems) {
                    if (item.TimerState == ServerUnit.TimerStateStarted)
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

                foreach (ClientUnit item in lstViewClient.SelectedItems)
                    center.ClientSendMessage(item.RemoteEP.Address.ToString(), item.RemoteEP.Port, input.textBox1.Text);
            }
        }

        private void MenuItem_ClientClose_Click(object sender, RoutedEventArgs e)
        {
            foreach (ClientUnit item in lstViewClient.SelectedItems)
                center.ClientClose(item.RemoteEP.Address.ToString(), item.RemoteEP.Port);
        }

        private void MenuItem_MsgClear_Click(object sender, RoutedEventArgs e)
        {
            txtMsg.Text = "";
        }

        private void txtMsg_TextChanged(object sender, TextChangedEventArgs e)
        {
            txtMsg.ScrollToEnd();
        }
    }
}
