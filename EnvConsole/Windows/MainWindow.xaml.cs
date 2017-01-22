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
using EnvConsole.Backend;
using EnvConsole.Unit;
using Newtonsoft.Json;

namespace EnvConsole.Windows
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private UIData uidata;
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
            var config = new FileInfo(AppDomain.CurrentDomain.BaseDirectory + "EnvConsole.xml");
            log4net.Config.XmlConfigurator.ConfigureAndWatch(config);

            var textBoxAppender = new TextBoxAppender();
            textBoxAppender.MsgBox = txtMsg;
            textBoxAppender.Threshold = log4net.Core.Level.All;
            textBoxAppender.Layout = new log4net.Layout.PatternLayout("%d [%t] %-5p %c - %m%n");
            log4net.Config.BasicConfigurator.Configure(textBoxAppender);
        }

        private void InitCore()
        {
            core = new Core();
            core.Run();

            // register events of modctl
            core.modctl.module_add += new ModuleCtl.ModuleCtlEvent(OnModuleCtlAdd);
            core.modctl.module_delete += new ModuleCtl.ModuleCtlEvent(OnModuleCtlDelete);

            // register events of sessctl
            core.sessctl.sess_create += new SessCtl.SessDelegate(OnSessCreate);
            core.sessctl.sess_delete += new SessCtl.SessDelegate(OnSessDelete);
            core.sessctl.sess_parse += new SessCtl.SessDelegate(OnSessParse);

            // register events of servctl & register a service
            core.servctl.serv_done += new ServiceDoneDelegate(OnServDone);
            core.servctl.RegisterService("MainWindow.clientupdate", ClientUpdateService);
        }

        private void InitDataUI()
        {
            // init dataui
            uidata = new UIData();

            // data bingding with dataui
            DataContext = new {
                ServerTable = uidata.ServerTable,
                ClientTable = uidata.ClientTable,
                ModuleTable = uidata.ModuleTable,
                DataUI = uidata
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
                    uidata.ServerTable.Add(server);
                }
            } catch (Exception ex) {
                log4net.ILog log = log4net.LogManager.GetLogger(typeof(MainWindow));
                log.Error("Exception of reading configure file.", ex);
                System.Windows.MessageBox.Show(EnvConst.CONF_NAME + ": syntax error.");
            }

            // autorun
            foreach (var item in uidata.ServerTable) {
                if (!item.AutoRun) continue;
                if (item.Protocol != "tcp") continue;

                var server = item;
                core.sessctl.BeginInvoke(new Action(() => {
                    // define variables
                    IPEndPoint ep = new IPEndPoint(IPAddress.Parse(server.IpAddress), server.Port);

                    // make listen
                    SockSess result = core.sessctl.FindSession(SockType.listen, ep, null);
                    if (result == null)
                        result = core.sessctl.MakeListen(ep);
                }));
            }

            // load all modules from directory "DataHandles"
            if (Directory.Exists(EnvConst.Module_PATH)) {
                foreach (var item in Directory.GetFiles(EnvConst.Module_PATH)) {
                    string str = item.Substring(item.LastIndexOf("\\") + 1);
                    if (str.Contains("Module") && str.ToLower().EndsWith(".dll")) {
                        object req = new {
                            id = "service.moduleadd",
                            filepath = item,
                        };
                        core.servctl.AddRequest(ServiceRequest.Parse(JsonConvert.SerializeObject(req)));
                    }
                }
            }
        }

        // Module Event ==================================================================================

        private void OnModuleCtlAdd(object sender, Module module)
        {
            module.module_load += new Module.ModuleEvent(OnModuleLoadOrUnload);
            module.module_unload += new Module.ModuleEvent(OnModuleLoadOrUnload);

            Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(module.AssemblyPath);
                ModuleUnit unit = new ModuleUnit();
                unit.FilePath = module.AssemblyPath;
                unit.FileName = module.AssemblyName;
                unit.FileVersion = fvi.FileVersion;
                unit.FileComment = fvi.Comments;
                unit.ModuleState = module.State.ToString();
                uidata.ModuleTable.Add(unit);
            }));
        }

        private void OnModuleCtlDelete(object sender, Module module)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                foreach (var item in uidata.ModuleTable) {
                    if (item.FileName == module.AssemblyName) {
                        uidata.ModuleTable.Remove(item);
                        break;
                    }
                }
            }));
        }

        private void OnModuleLoadOrUnload(Module module)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                foreach (var item in uidata.ModuleTable) {
                    if (item.FileName == module.AssemblyName) {
                        item.ModuleState = module.State.ToString();
                        break;
                    }
                }
            }));
        }

        // Session Event ==================================================================================

        private void OnSessCreate(object sender, SockSess sess)
        {
            /// ** update DataUI
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (sess.type == SockType.accept)
                    uidata.ClientAdded(sess.lep, sess.rep);

                if (sess.type == SockType.listen)
                    uidata.ServerStarted(sess.lep.Address.ToString(), sess.lep.Port);
            }));

            if (sess.type == SockType.listen) {
                var subset = from s in uidata.ServerTable
                             where s.Target == ServerTarget.center && s.Port == sess.lep.Port
                             select s;
                if (!subset.Any()) {
                    if (sess.sdata == null)
                        sess.sdata = new SessData();
                    (sess.sdata as SessData).IsAdmin = true;
                }
            }
        }

        private void OnSessDelete(object sender, SockSess sess)
        {
            /// ** update DataUI
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (sess.type == SockType.accept)
                    uidata.ClientDeleted(sess.rep);

                if (sess.type == SockType.listen)
                    uidata.ServerStoped(sess.lep.Address.ToString(), sess.lep.Port);
            }));
        }

        private void OnSessParse(object sender, SockSess sess)
        {
            /// ** update DataUI
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                uidata.PackRecved();
            }));
        }

        // Service Event ==================================================================================

        private void OnServDone(ServiceRequest request, ServiceResponse response)
        {
            /// ** update DataUI
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                uidata.PackParsed();
            }));
        }

        private void ClientUpdateService(ServiceRequest request, ref ServiceResponse response)
        {
            // parse to dictionary
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>(Encoding.UTF8.GetString(request.raw_data));

            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), int.Parse(dc["port"]));
            SockSess result = core.sessctl.FindSession(SockType.accept, null, ep);

            /// ** update DataUI
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (result != null) {
                    uidata.ClientUpdated(ep, "ID", dc["ccid"]);
                    uidata.ClientUpdated(ep, "Name", dc["name"]);
                }
            }));
        }

        // Events for itself ==================================================================

        private void MenuItem_AddModule_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();

            openFileDialog.Filter = "dll files (*.dll)|*.dll|All files (*.*)|*.*";
            openFileDialog.FileName = "";

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                object req = new {
                    id = "service.moduleadd",
                    filepath = openFileDialog.FileName,
                };
                core.servctl.AddRequest(ServiceRequest.Parse(JsonConvert.SerializeObject(req)));
            }
        }

        private void MenuItem_DelModule_Click(object sender, RoutedEventArgs e)
        {
            List<ModuleUnit> handles = new List<ModuleUnit>();

            // 保存要卸载的模块信息
            foreach (ModuleUnit item in lstViewModule.SelectedItems)
                handles.Add(item);

            // 卸载操作
            foreach (var item in handles) {
                object req = new {
                    id = "service.moduledel",
                    name = item.FileName,
                };
                core.servctl.AddRequest(ServiceRequest.Parse(JsonConvert.SerializeObject(req)));
            }
        }

        private void MenuItem_LoadModule_Click(object sender, RoutedEventArgs e)
        {
            foreach (ModuleUnit item in lstViewModule.SelectedItems) {
                if (item.ModuleState.Equals(ModuleState.Unload.ToString())) {
                    object req = new {
                        id = "service.moduleload",
                        name = item.FileName,
                    };
                    core.servctl.AddRequest(ServiceRequest.Parse(JsonConvert.SerializeObject(req)));
                }
            }
        }

        private void MenuItem_UnloadModule_Click(object sender, RoutedEventArgs e)
        {
            foreach (ModuleUnit item in lstViewModule.SelectedItems) {
                if (item.ModuleState.Equals(ModuleState.Loaded.ToString())) {
                    object req = new {
                        id = "service.moduleunload",
                        name = item.FileName,
                    };
                    core.servctl.AddRequest(ServiceRequest.Parse(JsonConvert.SerializeObject(req)));
                }
            }
        }

        private void MenuItem_StartListener_Click(object sender, RoutedEventArgs e)
        {
            foreach (ServerUnit item in lstViewServer.SelectedItems) {
                if (item.ListenState == ServerUnit.ListenStateStarted)
                    continue;

                object req = new {
                    id = "service.sessopen",
                    type = "listen",
                    ip = item.IpAddress,
                    port = item.Port,
                };
                core.servctl.AddRequest(ServiceRequest.Parse(JsonConvert.SerializeObject(req)));
            }
        }

        private void MenuItem_StopListener_Click(object sender, RoutedEventArgs e)
        {
            foreach (ServerUnit item in lstViewServer.SelectedItems) {
                if (item.ListenState == ServerUnit.ListenStateStoped || !item.CanStop)
                    continue;

                object req = new {
                    id = "service.sessclose",
                    type = "listen",
                    ip = item.IpAddress,
                    port = item.Port,
                };
                core.servctl.AddRequest(ServiceRequest.Parse(JsonConvert.SerializeObject(req)));
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
                    object req = new {
                        id = "service.sesssend",
                        type = "accept",
                        ip = item.RemoteEP.Address.ToString(),
                        port = item.RemoteEP.Port,
                        data = input.textBox1.Text,
                    };
                    core.servctl.AddRequest(ServiceRequest.Parse(JsonConvert.SerializeObject(req)));

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
                object req = new {
                    id = "service.sessclose",
                    type = "accept",
                    ip = item.RemoteEP.Address.ToString(),
                    port = item.RemoteEP.Port,
                };
                core.servctl.AddRequest(ServiceRequest.Parse(JsonConvert.SerializeObject(req)));
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
