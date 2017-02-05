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
using EnvClient.Backend;
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

        private Core backend;

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
            backend = new Core();

            // data bingding with dataui
            DataContext = new {
                ServerTable = backend.uidata.ListenTable,
                ClientTable = backend.uidata.AcceptTable,
                ModuleTable = backend.uidata.ModuleTable,
                DataUI = backend.uidata
            };
            this.acceptOpenCount.SetBinding(TextBlock.TextProperty, new Binding("DataUI.AcceptOpenCount"));
            this.acceptTotalCount.SetBinding(TextBlock.TextProperty, new Binding("DataUI.AcceptTotalCount"));
            this.acceptCloseCount.SetBinding(TextBlock.TextProperty, new Binding("DataUI.AcceptCloseCount"));
            this.packFetchedCount.SetBinding(TextBlock.TextProperty, new Binding("DataUI.PackFetchedCount"));
            this.packTotalCount.SetBinding(TextBlock.TextProperty, new Binding("DataUI.PackTotalCount"));
            this.packParsedCount.SetBinding(TextBlock.TextProperty, new Binding("DataUI.PackParsedCount"));

            // run backend
            backend.Run();
            backend.SessLoginRequest();
            backend.SessDetailRequest();
            backend.SessGroupStateRequest();
            backend.ModuleDetailRequest();
        }

        // Events for itself ==================================================================

        private void MenuItem_AddModule_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();

            openFileDialog.Filter = "dll files (*.dll)|*.dll|All files (*.*)|*.*";
            openFileDialog.FileName = "";

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                backend.ModuleAddRequest(openFileDialog.FileName);
        }

        private void MenuItem_DelModule_Click(object sender, RoutedEventArgs e)
        {
            List<ModuleUnit> handles = new List<ModuleUnit>();

            // 保存要卸载的模块信息
            foreach (ModuleUnit item in lstViewModule.SelectedItems)
                handles.Add(item);

            // 卸载操作
            foreach (var item in handles)
                backend.ModuleDelRequest(item.FileName);
        }

        private void MenuItem_LoadModule_Click(object sender, RoutedEventArgs e)
        {
            foreach (ModuleUnit item in lstViewModule.SelectedItems) {
                if (item.ModuleState.Equals(ModuleState.Unload.ToString()))
                    backend.ModuleLoadRequest(item.FileName);
            }
        }

        private void MenuItem_UnloadModule_Click(object sender, RoutedEventArgs e)
        {
            foreach (ModuleUnit item in lstViewModule.SelectedItems) {
                if (item.ModuleState.Equals(ModuleState.Loaded.ToString()))
                    backend.ModuleUnloadRequest(item.FileName);
            }
        }

        private void MenuItem_OpenPort_Click(object sender, RoutedEventArgs e)
        {
            using (InputDialog input = new InputDialog()) {
                input.Owner = this;
                input.Title = "开启端口";
                input.textBlock1.Text = "名称";
                input.textBlock2.Text = "端口";
                input.textBox1.Text = "XX服务";
                input.textBox1.Focus();
                input.textBox1.Select(input.textBox1.Text.Length, 0);

                if (input.ShowDialog() == false)
                    return;

                if (String.IsNullOrEmpty(input.textBox2.Text))
                    return;

                backend.SessListenRequest("0", int.Parse(input.textBox2.Text));
            }
        }

        private void MenuItem_ClosePort_Click(object sender, RoutedEventArgs e)
        {
            foreach (ListenUnit item in lstViewServer.SelectedItems)
                backend.SessCloseRequest(item.ID);
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

                foreach (AcceptUnit item in lstViewClient.SelectedItems) {
                    backend.SessSendRequest(item.ID, input.textBox1.Text);

                    string logmsg = "(" + "localhost" + " => " + item.RemoteEP.ToString() + ")" + Environment.NewLine;
                    logmsg += "\t" + input.textBox1.Text;

                    log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType).Info(logmsg);
                }
            }
        }

        private void MenuItem_ClientClose_Click(object sender, RoutedEventArgs e)
        {
            foreach (AcceptUnit item in lstViewClient.SelectedItems)
                backend.SessCloseRequest(item.ID);
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
