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
using System.Xml;
using EnvConsole.Unit;
using mnn.net;
using mnn.misc.env;
using mnn.misc.service;

namespace EnvConsole.Windows
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private DataUI dataui;
        private Core core;

        public MainWindow()
        {
            InitializeComponent();

            InitailizeWindowName();
            InitailizeStatusBar();

            InitLog4net();
            InitCore();
            InitDataUI();
        }

        // Initailize ============================================================================

        private void InitailizeWindowName()
        {
            // Format Main Form's Name
            Assembly asm = Assembly.GetExecutingAssembly();
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
            var config = new FileInfo(AppDomain.CurrentDomain.BaseDirectory + "EnvConsole.xml");
            log4net.Config.XmlConfigurator.ConfigureAndWatch(config);

            var textBoxAppender = new TextBoxAppender();
            textBoxAppender.MsgBox = txtMsg;
            textBoxAppender.Threshold = log4net.Core.Level.All;
            log4net.Config.BasicConfigurator.Configure(textBoxAppender);
        }

        private void InitCore()
        {
            core = new Core();
            Thread thread = new Thread(() =>
            {
                while (true) {
                    try {
                        core.Exec();
                    } catch (Exception ex) {
                        log4net.ILog log = log4net.LogManager.GetLogger(typeof(MainWindow));
                        log.Error("Exception thrown out by socksess thread.", ex);
                    }
                }
            });
            thread.IsBackground = true;
            thread.Start();

            // register events of sessctl
            core.sessctl.sess_create += new SessCtl.SessDelegate(OnSessCreate);
            core.sessctl.sess_delete += new SessCtl.SessDelegate(OnSessDelete);

            core.servctl.RegisterService("MainWindow.ClientUpdateService",
                ClientUpdateService, core.Coding.GetBytes("/center/clientupdate"));

            // load all modules from directory "DataHandles"
            if (Directory.Exists(EnvConst.Module_PATH)) {
                foreach (var item in Directory.GetFiles(EnvConst.Module_PATH)) {
                    string str = item.Substring(item.LastIndexOf("\\") + 1);
                    if (str.Contains("Module") && str.ToLower().EndsWith(".dll")) {
                        if (core.ModuleLoad(item)) {
                            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(item);
                            ModuleUnit unit = new ModuleUnit();
                            unit.FileName = System.IO.Path.GetFileName(item);
                            unit.FileVersion = fvi.FileVersion;
                            unit.FileComment = fvi.Comments;
                            dataui.ModuleTable.Add(unit);
                        }
                    }
                }
            }
        }

        private void InitDataUI()
        {
            // init dataui
            dataui = new DataUI();

            // data bingding with dataui
            DataContext = new {
                ServerTable = dataui.ServerTable,
                ClientTable = dataui.ClientTable,
                ModuleTable = dataui.ModuleTable,
                DataUI = dataui
            };
            this.currentClientCount.SetBinding(TextBlock.TextProperty, new Binding("DataUI.CurrentAcceptCount"));
            this.historyClientOpenCount.SetBinding(TextBlock.TextProperty, new Binding("DataUI.HistoryAcceptOpenCount"));
            this.historyClientCloseCount.SetBinding(TextBlock.TextProperty, new Binding("DataUI.HistoryAcceptCloseCount"));
            this.currentPackCount.SetBinding(TextBlock.TextProperty, new Binding("DataUI.CurrentPackCount"));
            this.historyPackFetchedCount.SetBinding(TextBlock.TextProperty, new Binding("DataUI.HistoryPackFetchedCount"));
            this.historyPackParsedCount.SetBinding(TextBlock.TextProperty, new Binding("DataUI.HistoryPackParsedCount"));

            ConfigDataUI();
        }

        private void ConfigDataUI()
        {
            if (File.Exists(EnvConst.CONF_PATH) == false) {
                System.Windows.MessageBox.Show(EnvConst.CONF_NAME + ": can't find it.");
                Thread.CurrentThread.Abort();
            }

            try {
                XmlDocument xdoc = new XmlDocument();
                xdoc.Load(EnvConst.CONF_PATH);

                // Server Config
                foreach (XmlNode item in xdoc.SelectNodes(EnvConst.CONF_SERVER)) {
                    ServerUnit server = new ServerUnit();
                    server.ID = item.Attributes["id"].Value;
                    server.Name = item.Attributes["name"].Value;
                    server.Target = (ServerTarget)Enum.Parse(typeof(ServerTarget), item.Attributes["target"].Value);
                    server.Protocol = item.Attributes["protocol"].Value;
                    server.IpAddress = item.Attributes["ipaddress"].Value;
                    server.Port = int.Parse(item.Attributes["port"].Value);
                    server.AutoRun = bool.Parse(item.Attributes["autorun"].Value);
                    server.CanStop = bool.Parse(item.Attributes["canstop"].Value);
                    server.ListenState = ServerUnit.ListenStateStoped;
                    server.TimerState = server.Target == ServerTarget.center
                        ? ServerUnit.TimerStateDisable : ServerUnit.TimerStateStoped;
                    server.TimerInterval = 0;
                    server.TimerCommand = "";
                    server.Timer = null;
                    dataui.ServerTable.Add(server);
                }
            } catch (Exception ex) {
                log4net.ILog log = log4net.LogManager.GetLogger(typeof(MainWindow));
                log.Error("Exception of reading configure file.", ex);
                System.Windows.MessageBox.Show(EnvConst.CONF_NAME + ": syntax error.");
            }

            // autorun
            foreach (var item in dataui.ServerTable) {
                if (!item.AutoRun) continue;

                if (item.Protocol == "tcp") {
                    core.ServerStart(item.IpAddress, item.Port);
                } else if (item.Protocol == "udp") {
                }
            }
        }

        // Session Event ==================================================================================

        private void OnSessCreate(object sender, SockSess sess)
        {
            /// ** update DataUI
            if (sess.type == SockType.accept)
                dataui.ClientAdd(sess.lep, sess.rep);

            if (sess.type == SockType.listen)
                dataui.ServerStart(sess.lep.Address.ToString(), sess.lep.Port);
        }

        private void OnSessDelete(object sender, SockSess sess)
        {
            /// ** update DataUI
            if (sess.type == SockType.accept)
                dataui.ClientDel(sess.rep);

            if (sess.type == SockType.listen)
                dataui.ServerStop(sess.lep.Address.ToString(), sess.lep.Port);
        }

        private void ClientUpdateService(ServiceRequest request, ref ServiceResponse response)
        {
            // get param string & parse to dictionary
            string url = core.Coding.GetString(request.data);
            if (!url.Contains('?')) return;
            string param_list = url.Substring(url.IndexOf('?') + 1);
            IDictionary<string, string> dc = SockConvert.ParseUrlQueryParam(param_list);

            /// ** update DataUI
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), int.Parse(dc["port"]));
            SockSess result = core.sessctl.FindSession(SockType.accept, null, ep);
            if (result != null) {
                dataui.ClientUpdate(ep, "ID", dc["ccid"]);
                dataui.ClientUpdate(ep, "Name", dc["name"]);
            }
        }

        // Events for itself ==================================================================

        private void MenuItem_LoadModule_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();

            openFileDialog.Filter = "dll files (*.dll)|*.dll|All files (*.*)|*.*";
            openFileDialog.FileName = "";

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                if (core.ModuleLoad(openFileDialog.FileName)) {
                    FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(openFileDialog.FileName);
                    ModuleUnit unit = new ModuleUnit();
                    unit.FileName = System.IO.Path.GetFileName(openFileDialog.FileName);
                    unit.FileVersion = fvi.FileVersion;
                    unit.FileComment = fvi.Comments;
                    dataui.ModuleTable.Add(unit);
                }
            }
        }

        private void MenuItem_UnloadModule_Click(object sender, RoutedEventArgs e)
        {
            List<ModuleUnit> handles = new List<ModuleUnit>();

            // 保存要卸载的模块信息
            foreach (ModuleUnit item in lstViewModule.SelectedItems)
                handles.Add(item);

            // 卸载操作
            foreach (var item in handles) {
                if (core.ModuleUnload(item.FileName))
                    dataui.ModuleTable.Remove(item);
            }
        }

        private void MenuItem_StartListener_Click(object sender, RoutedEventArgs e)
        {
            foreach (ServerUnit item in lstViewServer.SelectedItems) {
                if (item.ListenState == ServerUnit.ListenStateStarted)
                    continue;

                core.ServerStart(item.IpAddress, item.Port, item.Protocol);
            }
        }

        private void MenuItem_StopListener_Click(object sender, RoutedEventArgs e)
        {
            foreach (ServerUnit item in lstViewServer.SelectedItems) {
                if (item.ListenState == ServerUnit.ListenStateStoped || item.CanStop == false)
                    continue;

                if (item.CanStop == true)
                    core.ServerStop(item.IpAddress, item.Port, item.Protocol);
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

                core.ServerTimerStart(item);
            }
        }

        private void MenuItem_StopTimer_Click(object sender, RoutedEventArgs e)
        {
            foreach (ServerUnit item in lstViewServer.SelectedItems) {
                if (item.TimerState == ServerUnit.TimerStateStoped ||
                    item.TimerState == ServerUnit.TimerStateDisable)
                    continue;

                core.ServerTimerStop(item);
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
                    core.ClientSendMessage(item.RemoteEP.Address.ToString(), item.RemoteEP.Port, input.textBox1.Text);
                    string log = DateTime.Now + " (" + "localhost" + " => " + item.RemoteEP.ToString() + ")\n";
                    log += input.textBox1.Text + "\n\n";
                    txtMsg.Text += log;
                    txtMsg.ScrollToEnd();
                }
            }
        }

        private void MenuItem_ClientClose_Click(object sender, RoutedEventArgs e)
        {
            foreach (ClientUnit item in lstViewClient.SelectedItems)
                core.ClientClose(item.RemoteEP.Address.ToString(), item.RemoteEP.Port);
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
