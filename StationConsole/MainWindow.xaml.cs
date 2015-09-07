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
    class ClientStateTable : ObservableCollection<ClientUnitState> { }

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

            // Data Source Binding
            this.serverStateTable = new ObservableCollection<ServerUnitState>();
            this.clientStateTable = (ClientStateTable)Resources["clientStateTable"];
            this.moduleStateTable = new ObservableCollection<ModuleUnitState>();

            this.lstViewServer.ItemsSource = serverStateTable;
            //lstViewClientPoint.ItemsSource = clientStateTable;
            this.lstViewModule.ItemsSource = moduleStateTable;
        }

        private ObservableCollection<ServerUnitState> serverStateTable;
        private ObservableCollection<ClientUnitState> clientStateTable;
        private ObservableCollection<ModuleUnitState> moduleStateTable;

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

        public void AddServer(ServerUnit server)
        {
            ServerUnitState state = new ServerUnitState(server);

            if (state.AutoRun == true)
                state.ListenState = ServerUnitState.ListenStateStarted;
            else
                state.ListenState = ServerUnitState.ListenStateStoped;
            if (state.Protocol == "tcp")
                state.TimerState = ServerUnitState.TimerStateStoped;
            else
                state.TimerState = ServerUnitState.TimerStateDisable;
            state.TimerInterval = 0;
            state.TimerCommand = "";

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                serverStateTable.Add(state);
            }));
        }

        public void RemoveServer(ServerUnit server)
        {
            string id = server.ID;

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                var subset = from s in serverStateTable where s.ID.Equals(id) select s;

                if (subset.Count() != 0)
                    serverStateTable.Remove(subset.First());
            }));
        }

        public void AddClient(ClientUnit client)
        {
            ClientUnitState state = new ClientUnitState(client);

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                this.clientStateTable.Add(state);

                // set value for ObservableCollection object 
                currentClientCount.Text = (Convert.ToInt32(currentClientCount.Text) + 1).ToString();
                historyClientOpenCount.Text = (Convert.ToInt32(historyClientOpenCount.Text) + 1).ToString();
            }));
        }

        public void RemoveClient(ClientUnit client)
        {
            IPEndPoint remoteEP = client.RemoteEP;

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                var subset = from s in this.clientStateTable where s.RemoteEP.Equals(remoteEP) select s;

                if (subset.Count() != 0)
                    this.clientStateTable.Remove(subset.First());

                // set value for ObservableCollection object 
                currentClientCount.Text = (Convert.ToInt32(currentClientCount.Text) - 1).ToString();
                historyClientCloseCount.Text = (Convert.ToInt32(historyClientCloseCount.Text) + 1).ToString();
            }));
        }

        public void RemoveClient(IPEndPoint ep)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                var subset = from s in this.clientStateTable where s.RemoteEP.Equals(ep) select s;

                if (subset.Count() != 0)
                    this.clientStateTable.Remove(subset.First());

                // set value for ObservableCollection object 
                currentClientCount.Text = (Convert.ToInt32(currentClientCount.Text) - 1).ToString();
                historyClientCloseCount.Text = (Convert.ToInt32(historyClientCloseCount.Text) + 1).ToString();
            }));
        }

        public void UpdateClient(IPEndPoint ep, string fieldName, object value)
        {
            Type t = typeof(ClientUnit);
            PropertyInfo propertyInfo = t.GetProperty(fieldName);

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                var subset = from s in this.clientStateTable
                                where s.RemoteEP.Equals(ep)
                                select s;

                if (subset.Count() != 0)
                    propertyInfo.SetValue(subset.First(), value, null);
            }));
        }

        public void AddModule(ModuleUnit module)
        {
            ModuleUnitState state = new ModuleUnitState(module);

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                moduleStateTable.Add(state);
            }));
        }

        public void RemoveModule(ModuleUnit module)
        {
            string id = module.ID;

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                var subset = from s in moduleStateTable where s.ID.Equals(id) select s;

                if (subset.Count() != 0)
                    moduleStateTable.Remove(subset.First());
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
            App.Mindow = this;
            App.Ctrler = new Controler();
            App.Ctrler.InitConfig();
            App.Ctrler.InitServer();
            App.Ctrler.InitDefaultModule();
            App.Ctrler.InitMsgHandle();
        }

        private void MenuItem_LoadModule_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();

            openFileDialog.Filter = "dll files (*.dll)|*.dll|All files (*.*)|*.*";
            openFileDialog.FileName = "";

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                App.Ctrler.AtModuleLoad(openFileDialog.FileName);
        }

        private void MenuItem_UnloadModule_Click(object sender, RoutedEventArgs e)
        {
            List<ModuleUnitState> handles = new List<ModuleUnitState>();

            // 保存要卸载的模块信息
            foreach (var item in lstViewModule.SelectedItems) {
                ModuleUnitState dataHandle = item as ModuleUnitState;
                if (dataHandle == null)
                    continue;

                handles.Add(dataHandle);
            }

            // 卸载操作
            foreach (var item in handles) {
                App.Ctrler.AtModuleUnload(item.FileName);
            }
        }

        private void MenuItem_StartListener_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in lstViewServer.SelectedItems) {
                ServerUnitState server = item as ServerUnitState;
                if (server == null)
                    continue;

                if (server.ListenState == ServerUnitState.ListenStateStarted)
                    continue;

                App.Ctrler.AtServerStart(server.ID,
                    new IPEndPoint(IPAddress.Parse(server.IpAddress), server.Port));
                server.ListenState = ServerUnitState.ListenStateStarted;
            }
        }

        private void MenuItem_StopListener_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in lstViewServer.SelectedItems) {
                ServerUnitState server = item as ServerUnitState;
                if (server == null)
                    continue;

                if (server.ListenState == ServerUnitState.ListenStateStoped || server.CanStop == false)
                    continue;

                if (server.CanStop == true)
                    App.Ctrler.AtServerStop(server.ID);
                server.ListenState = ServerUnitState.ListenStateStoped;
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

            foreach (var item in lstViewServer.SelectedItems) {
                ServerUnitState server = item as ServerUnitState;
                if (server == null)
                    continue;

                if (server.ListenState == ServerUnitState.ListenStateStarted)
                    continue;

                server.Port = int.Parse(input.textBox2.Text);
            }
        }

        private void MenuItem_StartTimer_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in lstViewServer.SelectedItems) {
                ServerUnitState server = item as ServerUnitState;
                if (server == null)
                    continue;

                if (server.TimerState == ServerUnitState.TimerStateStarted ||
                    server.TimerState == ServerUnitState.TimerStateDisable ||
                    server.TimerInterval <= 0 || server.TimerCommand == "")
                    continue;

                App.Ctrler.AtServerTimerStart(server.ID, server.TimerInterval * 1000, server.TimerCommand);
                server.TimerState = ServerUnitState.TimerStateStarted;
            }
        }

        private void MenuItem_StopTimer_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in lstViewServer.SelectedItems) {
                ServerUnitState server = item as ServerUnitState;
                if (server == null)
                    continue;

                if (server.TimerState == ServerUnitState.TimerStateStoped ||
                    server.TimerState == ServerUnitState.TimerStateDisable)
                    continue;

                App.Ctrler.AtServerTimerStop(server.ID);
                server.TimerState = ServerUnitState.TimerStateStoped;
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

            foreach (var item in lstViewServer.SelectedItems) {
                ServerUnitState server = item as ServerUnitState;
                if (server == null)
                    continue;

                if (server.TimerState == ServerUnitState.TimerStateStarted)
                    continue;

                server.TimerCommand = input.textBox1.Text;
                if (input.textBox2.Text != "")
                    server.TimerInterval = Convert.ToDouble(input.textBox2.Text);
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
            input.textBox1.Text = "!A1?";
            input.textBox1.Focus();
            input.textBox1.Select(input.textBox1.Text.Length, 0);

            if (input.ShowDialog() == false)
                return;

            foreach (var item in lstViewClient.SelectedItems) {
                ClientUnitState client = item as ClientUnitState;
                if (client == null)
                    continue;

                App.Ctrler.AtClientSendMessage(client.ID, input.textBox1.Text);
            }
        }

        private void MenuItem_ClientClose_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in lstViewClient.SelectedItems) {
                ClientUnitState client = item as ClientUnitState;
                if (client == null)
                    continue;

                App.Ctrler.AtClientClose(client.ID);
            }
        }

    }
}
