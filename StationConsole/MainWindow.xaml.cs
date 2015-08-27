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
            //lstViewClientPoint.ItemsSource = clientStateTable;
            this.lstViewServer.ItemsSource = serverStateTable;
        }

        private ObservableCollection<ServerUnitState> serverStateTable;
        private ObservableCollection<ClientUnitState> clientStateTable;

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

            state.ListenState = ServerUnitState.ListenStateStoped;
            if (state.Protocol == "tcp")
                state.TimerState = ServerUnitState.TimerStateStoped;
            else
                state.TimerState = ServerUnitState.TimerStateDisable;
            state.TimerInterval = 0;
            state.TimerCommand = "";
            state.PluginSupport = "";

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                lock (serverStateTable) {
                    serverStateTable.Add(state);
                }
            }));
        }

        public void RemoveServer(ServerUnit server)
        {
            string id = server.ID;

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                lock (serverStateTable) {
                    var subset = from s in serverStateTable where s.ID.Equals(id) select s;

                    if (subset.Count() != 0)
                        serverStateTable.Remove(subset.First());
                }
            }));
        }

        public void AddClient(ClientUnit client)
        {
            ClientUnitState state = new ClientUnitState(client);

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                lock (this.clientStateTable) {
                    this.clientStateTable.Add(state);
                }

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
                lock (this.clientStateTable) {
                    var subset = from s in this.clientStateTable where s.RemoteEP.Equals(remoteEP) select s;

                    if (subset.Count() != 0)
                        this.clientStateTable.Remove(subset.First());
                }

                // set value for ObservableCollection object 
                currentClientCount.Text = (Convert.ToInt32(currentClientCount.Text) - 1).ToString();
                historyClientCloseCount.Text = (Convert.ToInt32(historyClientCloseCount.Text) + 1).ToString();
            }));
        }

        public void RemoveClient(IPEndPoint ep)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                lock (this.clientStateTable) {
                    var subset = from s in this.clientStateTable where s.RemoteEP.Equals(ep) select s;

                    if (subset.Count() != 0)
                        this.clientStateTable.Remove(subset.First());
                }

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
                lock (this.clientStateTable) {
                    var subset = from s in this.clientStateTable
                                 where s.RemoteEP.Equals(ep)
                                 select s;

                    if (subset.Count() != 0)
                        propertyInfo.SetValue(subset.First(), value, null);
                }
            }));
        }

        public void AddPlugin(PluginUnit plugin)
        {
            string name = plugin.Name;
            string fileName = plugin.FileName;

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                lock (serverStateTable) {
                    var subset = from s in serverStateTable where s.Name.Contains(name) select s;

                    if (subset.Count() == 0) {
                        subset = from s in serverStateTable where s.Name.Contains("通用") select s;
                    }

                    subset.First().PluginSupport += fileName + "，";
                }
            }));
        }

        public void RemovePlugin(PluginUnit plugin)
        {
            string fileName = plugin.FileName;

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                lock (serverStateTable) {
                    var subset = from s in serverStateTable where s.PluginSupport.Contains(fileName) select s;

                    if (subset.Count() == 0)
                        return;

                    subset.First().PluginSupport = subset.First().PluginSupport.Replace(plugin.FileName + "，", "");
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
            App.MWindow = this;
            App.CLayer = new ControlLayer();
            App.CLayer.InitailizeConfig();
            App.CLayer.InitailizeServer();
            App.CLayer.InitailizeDefaultPlugin();
        }

        private void MenuItem_LoadPlugin_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();

            openFileDialog.Filter = "dll files (*.dll)|*.dll|All files (*.*)|*.*";
            openFileDialog.FileName = "";

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                App.CLayer.AtPluginLoad(openFileDialog.FileName);
        }

        private void MenuItem_UnloadPlugin_Click(object sender, RoutedEventArgs e)
        {
            List<ServerUnitState> handles = new List<ServerUnitState>();

            // 保存要卸载的模块信息
            foreach (var item in lstViewServer.SelectedItems) {
                ServerUnitState dataHandle = item as ServerUnitState;
                if (dataHandle == null)
                    continue;

                handles.Add(dataHandle);
            }

            // 卸载操作
            foreach (var item in handles) {
                if (string.IsNullOrEmpty(item.PluginSupport) == true)
                    continue;

                string[] strTmp = item.PluginSupport.Split("，".ToArray(), StringSplitOptions.RemoveEmptyEntries);
                foreach (var i in strTmp) {
                    App.CLayer.AtPluginUnload(i);
                }
            }
        }

        private void MenuItem_StartListener_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in lstViewServer.SelectedItems) {
                ServerUnitState server = item as ServerUnitState;
                if (server == null)
                    continue;

                if (server.ListenState == ServerUnitState.ListenStateStarted)
                    return;

                App.CLayer.AtServerStart(server.ID,
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

                if (server.ListenState == ServerUnitState.ListenStateStoped)
                    return;

                App.CLayer.AtServerStop(server.ID);
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
                    return;

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
                    return;

                App.CLayer.AtTimerStart(server.ID, server.TimerInterval * 1000, server.TimerCommand);
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
                    return;

                App.CLayer.AtTimerStop(server.ID);
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
                    return;

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
            input.textBox1.Text = "!A0#";
            input.textBox1.Focus();
            input.textBox1.Select(input.textBox1.Text.Length, 0);

            if (input.ShowDialog() == false)
                return;

            foreach (var item in lstViewClient.SelectedItems) {
                ClientUnitState client = item as ClientUnitState;
                if (client == null)
                    continue;

                App.CLayer.AtClientSendMessage(client.ServerID, client.ID, input.textBox1.Text);
            }
        }

        private void MenuItem_ClientClose_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in lstViewClient.SelectedItems) {
                ClientUnitState client = item as ClientUnitState;
                if (client == null)
                    continue;

                App.CLayer.AtClientClose(client.ServerID, client.ID);
            }
        }

    }
}
