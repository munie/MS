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
using System.Diagnostics;
using System.IO;
using System.Collections.ObjectModel;
using System.Threading;
using System.Xml;
using mnn.net;
using mnn.misc.env;
using mnn.service;
using mnn.module;
using EnvClient.Env;
using EnvClient.Unit;

namespace EnvClient.Windows
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public static readonly string BASE_DIR = System.AppDomain.CurrentDomain.BaseDirectory;
        public static readonly string CONF_NAME = "EnvClient.xml";
        public static readonly string CONF_PATH = BASE_DIR + CONF_NAME;

        private Backend backend;

        public MainWindow()
        {
            InitializeComponent();

            InitailizeWindowName();
            InitailizeStatusBar();

            InitLog4net();
            InitBackend();
        }

        // Initailize =========================================================================

        private void InitailizeWindowName()
        {
            // Format Main Form's Name
            System.Reflection.Assembly asm = System.Reflection.Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(asm.Location);
            this.Title = string.Format("{0} {1} - Powered By {2}",
                fvi.ProductName,
                fvi.FileVersion,
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

        private void InitLog4net()
        {
            var config = new FileInfo(AppDomain.CurrentDomain.BaseDirectory + "EnvClient.xml");
            log4net.Config.XmlConfigurator.ConfigureAndWatch(config);

            var textBoxAppender = new TextBoxAppender();
            textBoxAppender.MsgBox = txtMsg;
            textBoxAppender.Threshold = log4net.Core.Level.All;
            textBoxAppender.Layout = new log4net.Layout.PatternLayout("%d [%t] %-5p %c - %m%n");
            log4net.Config.BasicConfigurator.Configure(textBoxAppender);
        }

        private void InitBackend()
        {
            // init dataui
            backend = new Backend();

            // data bingding with dataui
            DataContext = new {
                ServerTable = backend.uidata.ServerTable,
                ClientTable = backend.uidata.ClientTable,
                ModuleTable = backend.uidata.ModuleTable,
                DataUI = backend.uidata
            };
            this.currentClientCount.SetBinding(TextBlock.TextProperty, new Binding("DataUI.CurrentAcceptCount"));
            this.historyClientOpenCount.SetBinding(TextBlock.TextProperty, new Binding("DataUI.HistoryAcceptOpenCount"));
            this.historyClientCloseCount.SetBinding(TextBlock.TextProperty, new Binding("DataUI.HistoryAcceptCloseCount"));
            this.currentPackCount.SetBinding(TextBlock.TextProperty, new Binding("DataUI.CurrentPackCount"));
            this.historyPackFetchedCount.SetBinding(TextBlock.TextProperty, new Binding("DataUI.HistoryPackFetchedCount"));
            this.historyPackParsedCount.SetBinding(TextBlock.TextProperty, new Binding("DataUI.HistoryPackParsedCount"));

            // run backend
            backend.Run();
            backend.Login();
        }

        // Events for itself ==================================================================

        private void MenuItem_AddModule_Click(object sender, RoutedEventArgs e)
        {
        }

        private void MenuItem_DelModule_Click(object sender, RoutedEventArgs e)
        {
            List<ModuleUnit> handles = new List<ModuleUnit>();

            // 保存要卸载的模块信息
            foreach (ModuleUnit item in lstViewModule.SelectedItems)
                handles.Add(item);

            // 卸载操作
            foreach (var item in handles) {
            }
        }

        private void MenuItem_LoadModule_Click(object sender, RoutedEventArgs e)
        {
            foreach (ModuleUnit item in lstViewModule.SelectedItems) {
                if (item.ModuleState.Equals(ModuleState.Unload.ToString())) {
                }
            }
        }

        private void MenuItem_UnloadModule_Click(object sender, RoutedEventArgs e)
        {
            foreach (ModuleUnit item in lstViewModule.SelectedItems) {
                if (item.ModuleState.Equals(ModuleState.Loaded.ToString())) {
                }
            }
        }

        private void MenuItem_StartListener_Click(object sender, RoutedEventArgs e)
        {
            foreach (ServerUnit item in lstViewServer.SelectedItems) {
                    continue;
            }
        }

        private void MenuItem_StopListener_Click(object sender, RoutedEventArgs e)
        {
            foreach (ServerUnit item in lstViewServer.SelectedItems) {
                    continue;
            }
        }

        private void MenuItem_StartTimer_Click(object sender, RoutedEventArgs e)
        {
            foreach (ServerUnit item in lstViewServer.SelectedItems) {
                if (item.TimerState == ServerUnit.TimerStateStarted ||
                    item.TimerState == ServerUnit.TimerStateDisable ||
                    item.TimerInterval <= 0 || item.TimerCommand == "")
                    continue;

                item.TimerState = ServerUnit.TimerStateStarted;
            }
        }

        private void MenuItem_StopTimer_Click(object sender, RoutedEventArgs e)
        {
            foreach (ServerUnit item in lstViewServer.SelectedItems) {
                if (item.TimerState == ServerUnit.TimerStateStoped ||
                    item.TimerState == ServerUnit.TimerStateDisable)
                    continue;

                item.TimerState = ServerUnit.TimerStateStoped;
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

                foreach (ClientUnit item in lstViewClient.SelectedItems) {

                    string logmsg = "(" + "localhost" + " => " + item.RemoteEP.ToString() + ")" + Environment.NewLine;
                    logmsg += "\t" + input.textBox1.Text;

                    log4net.ILog logger = log4net.LogManager.GetLogger(typeof(MainWindow));
                    logger.Info(logmsg);
                }
            }
        }

        private void MenuItem_ClientClose_Click(object sender, RoutedEventArgs e)
        {
            foreach (ClientUnit item in lstViewClient.SelectedItems) {
            }
        }

        private void MenuItem_MsgClear_Click(object sender, RoutedEventArgs e)
        {
            txtMsg.Text = "";
        }

        //private void txtMsg_TextChanged(object sender, TextChangedEventArgs e)
        //{
        //    txtMsg.ScrollToEnd();
        //}
    }
}
