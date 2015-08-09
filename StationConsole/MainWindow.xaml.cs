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
using System.Net.Sockets;
using System.Reflection;
using System.Diagnostics;
using System.IO;
using System.Collections.ObjectModel;
using System.Threading;
using MnnSocket;
using MnnPlugin;

namespace StationConsole
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            InitailizeWindowName();
            InitailizeIPAddress();

            LoadPluginFromDirectory();
            UpdateClientPointMenu();
        }

        private IPAddress ipAddress = null;

        private AsyncSocketListenerManager listenerManager = new AsyncSocketListenerManager();
        private PluginManager pluginManager = new PluginManager();

        private ObservableCollection<DataHandleState> dataHandleTable = new ObservableCollection<DataHandleState>();
        private ObservableCollection<ClientPoint> clientPointTable = new ObservableCollection<ClientPoint>();

        // Methods ============================================================================

        private void InitailizeWindowName()
        {
            // Format Main Form's Name
            Assembly asm = Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(asm.Location);
            this.Title = string.Format("{0} {1}.{2} - Powered By {3}",
                fvi.ProductName, fvi.ProductMajorPart, fvi.ProductMinorPart, fvi.CompanyName);
        }

        private void InitailizeIPAddress()
        {
            IPAddress[] ipAddr = Dns.GetHostAddresses(Dns.GetHostName());
            foreach (IPAddress ip in ipAddr) {
                if (ip.AddressFamily.Equals(AddressFamily.InterNetwork)) {
                    ipAddress = ip;
                    break;
                }
            }
        }

        private void LoadPluginFromDirectory()
        {
            // Get all files in directory "DataHandles"
            string pluginPath = System.AppDomain.CurrentDomain.BaseDirectory + @"\DataHandles";

            if (Directory.Exists(pluginPath)) {
                string[] files = Directory.GetFiles(pluginPath);

                // Load dll files one by one
                foreach (var item in files)
                    LoadPlugin(item);
            }
        }

        private void LoadPlugin(string filePath)
        {
            int listenPort = 0;
            DataHandleState dataHandle = new DataHandleState();
            dataHandle.Plugin = new PluginItem();

            try {
                dataHandle.Plugin.Load(filePath);
            }
            catch (Exception ex) {
                MessageBox.Show(ex.Message, "Error");
                return;
            }

            try {
                listenPort = (int)dataHandle.Plugin.Invoke("IDataHandle", "GetIdentity", null);
            }
            catch (Exception ex) {
                dataHandle.Plugin.UnLoad();
                MessageBox.Show(ex.Message, "Error");
                return;
            }

            // 加载模块已经成功
            dataHandle.Listener = new AsyncSocketListenerItem();
            dataHandle.Timer = new System.Timers.Timer();

            dataHandle.ListenPort = listenPort;
            dataHandle.ListenState = DataHandleState.ListenStateStoped;
            dataHandle.ChineseName = FileVersionInfo.GetVersionInfo(filePath).ProductName;
            dataHandle.FileName = dataHandle.Plugin.AssemblyName;
            dataHandle.TimerState = DataHandleState.TimerStateStoped;
            dataHandle.TimerInterval = 0;
            dataHandle.TimerCommand = "";

            dataHandle.Listener.ClientConnect += SocketListener_ClientConnect;
            dataHandle.Listener.ClientDisconn += SocketListener_ClientDisconn;
            dataHandle.Listener.ClientReadMsg += SocketListener_ClientReadMsg;
            dataHandle.Listener.ClientSendMsg += SocketListener_ClientSendMsg;

            // 加入 table
            listenerManager.Items.Add(dataHandle.Listener);
            pluginManager.Items.Add(dataHandle.Plugin);
            dataHandleTable.Add(dataHandle);
        }

        private void UnLoadPlugin(DataHandleState dataHandle)
        {
            // 关闭端口
            if (dataHandle.ListenState == DataHandleState.ListenStateStarted) {
                // 逻辑上讲，不会出现异常
                dataHandle.Listener.Stop();
                // 同时关闭对应客户端
                dataHandle.Listener.CloseClient();
                dataHandle.ListenState = DataHandleState.ListenStateStoped;
            }

            // 关闭定时器
            if (dataHandle.TimerState == DataHandleState.TimerStateStarted) {
                dataHandle.Timer.Stop();
                dataHandle.TimerState = DataHandleState.TimerStateStoped;
            }

            // 卸载模块
            dataHandle.Plugin.UnLoad();

            // 移出 table
            listenerManager.Items.Remove(dataHandle.Listener);
            pluginManager.Items.Remove(dataHandle.Plugin);
            dataHandleTable.Remove(dataHandle);
        }

        private void UpdateClientPointMenu()
        {
            clientPointShowModeMenu.Items.Clear();

            MenuItem menuItem = new MenuItem();
            menuItem.Header = "全部显示";
            menuItem.Click += new RoutedEventHandler((s, ea) =>
            {
                MenuItem m = s as MenuItem;

                for (int i = 0; i < lstViewClientPoint.Items.Count; i++) {
                    ListViewItem row = (ListViewItem)lstViewClientPoint.ItemContainerGenerator.ContainerFromIndex(i);
                    row.Visibility = System.Windows.Visibility.Visible;
                }

                clientPointShowModeStatus.Text = m.Header.ToString();
            });

            clientPointShowModeMenu.Items.Add(menuItem);
            Separator separator = new Separator();
            clientPointShowModeMenu.Items.Add(separator);

            foreach (var item in dataHandleTable) {
                menuItem = new MenuItem();
                menuItem.Header = "仅显示 " + item.ListenPort + "" + item.ChineseName;
                menuItem.Click += new RoutedEventHandler((s, ea) =>
                {
                    MenuItem m = s as MenuItem;
                    System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex("[0-9]+");
                    System.Text.RegularExpressions.Match match = regex.Match(m.Header.ToString());

                    lock (clientPointTable) {
                        for (int i = 0; i < clientPointTable.Count; i++) {
                            ListViewItem row = (ListViewItem)lstViewClientPoint.ItemContainerGenerator.ContainerFromIndex(i);

                            if (clientPointTable[i].AcceptedPort == Convert.ToInt32(match.Value)) {
                                row.Visibility = System.Windows.Visibility.Visible;
                            }
                            else {
                                row.Visibility = System.Windows.Visibility.Collapsed;
                            }
                        }
                    }

                    clientPointShowModeStatus.Text = m.Header.ToString();
                });

                clientPointShowModeMenu.Items.Add(menuItem);
            }
        }

        // Events for AsyncSocketListenerItem =================================================

        private void SocketListener_ClientConnect(object sender, ClientEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                // set value for ObservableCollection object 
                ClientPoint clientPoint = new ClientPoint();

                clientPoint.IpAddress = e.RemoteEP.ToString();
                clientPoint.AcceptedPort = e.LocalEP.Port;
                clientPoint.ConnectTime = DateTime.Now;
                clientPoint.CCID = "";

                lock (clientPointTable) {
                    clientPointTable.Add(clientPoint);
                }
            }));
        }

        private void SocketListener_ClientDisconn(object sender, ClientEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                // set value for ObservableCollection object 
                lock (clientPointTable) {
                    var subfirst = (from s in clientPointTable
                                    where s.IpAddress.Equals(e.RemoteEP.ToString())
                                    select s).First();

                    if (subfirst != null)
                        clientPointTable.Remove(subfirst);
                }
            }));
        }

        private void SocketListener_ClientReadMsg(object sender, ClientEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (txtMsg.Text.Length >= 20 * 1024)
                    txtMsg.Clear();

                string logFormat = e.RemoteEP.ToString() + " " + DateTime.Now.ToString() + "接收数据：" + e.Data;

                txtMsg.AppendText(logFormat + "\r\n\r\n");
                txtMsg.ScrollToEnd();
                //LogRecord.WriteInfoLog(logFormat);

                /// @@ 没有办法的办法，必须删改
                string[] str = e.Data.Split("|".ToArray());
                foreach (var item in str) {
                    if (item.StartsWith("CCID=")) {

                        lock (clientPointTable) {
                            // 从客户表中找到与远程ip地址相同的条目，更新CCID
                            foreach (var client in clientPointTable) {
                                if (client.IpAddress.Equals(e.RemoteEP.ToString())) {
                                    client.CCID = item.Substring(5);
                                }
                            }

                            // 基站不会自动断开前一次连接...相同CCID的连上来后，断开前面的连接
                            foreach (var client in clientPointTable) {
                                // 1.CCID相同 2.远程IP地址不相同 3.本地端口相同
                                if (client.CCID.Equals(item.Substring(5)) && !client.IpAddress.Equals(e.RemoteEP.ToString()) &&
                                    client.AcceptedPort.Equals(e.LocalEP.Port)) {
                                    string[] s = client.IpAddress.Split(":".ToArray());
                                    listenerManager.CloseClient(new IPEndPoint(IPAddress.Parse(s[0]), Convert.ToInt32(s[1])));
                                    break;
                                }
                            }
                        }

                    }
                }
                /// @@ 没有办法的办法，必须删改

                /// 调用数据处理插件 ======================================================
                try {
                    var subFirst = (from s in dataHandleTable where s.ListenPort == e.LocalEP.Port select s).First();

                    object retValue = subFirst.Plugin.Invoke("IDataHandle", "Handle", new object[] { e.Data });
                    if (retValue != null)
                        subFirst.Listener.Send(e.RemoteEP, (string)retValue);
                }
                catch (Exception ex) {
                    LogRecord.writeLog(ex);
                }
            }));
        }

        private void SocketListener_ClientSendMsg(object sender, ClientEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                string logFormat = e.RemoteEP.ToString() + " " + DateTime.Now.ToString() + "发送数据：" + e.Data;

                txtMsg.AppendText(logFormat + "\r\n\r\n");
                txtMsg.ScrollToEnd();
                LogRecord.WriteInfoLog(logFormat);
            }));
        }

        // Events for itself ==================================================================

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            lstViewClientPoint.ItemsSource = clientPointTable;
            lstViewDataHandle.ItemsSource = dataHandleTable;

            // 运行时间
            TextBlock blockTimeRun = new TextBlock();
            statusBar.Items.Add(blockTimeRun);

            DateTime startTime = DateTime.Now;

            System.Windows.Threading.DispatcherTimer timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = new TimeSpan(0, 0, 1);
            timer.Tick += new EventHandler((s, ea) =>
            {
                blockTimeRun.Text = "运行时间 " + DateTime.Now.Subtract(startTime).ToString(@"dd\-hh\:mm\:ss");
            });
            timer.Start();
        }

        private void MenuItem_LoadPlugin_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();

            openFileDialog.Filter = "dll files (*.dll)|*.dll|All files (*.*)|*.*";
            openFileDialog.FileName = "";

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                LoadPlugin(openFileDialog.FileName);

            // 更新菜单
            UpdateClientPointMenu();
        }

        private void MenuItem_UnloadPlugin_Click(object sender, RoutedEventArgs e)
        {
            List<DataHandleState> handles = new List<DataHandleState>();

            // 保存要卸载的模块信息
            foreach (var item in lstViewDataHandle.SelectedItems) {
                DataHandleState dataHandle = item as DataHandleState;

                if (dataHandle == null)
                    continue;

                handles.Add(dataHandle);
            }

            // 卸载操作
            foreach (var item in handles)
                UnLoadPlugin(item);

            // 更新菜单
            UpdateClientPointMenu();
        }

        private void MenuItem_StartListener_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in lstViewDataHandle.SelectedItems) {
                DataHandleState dataHandle = item as DataHandleState;

                if (dataHandle == null)
                    continue;
                if (dataHandle.ListenState == DataHandleState.ListenStateStarted)
                    continue;

                // 端口可能已经被其他程序监听
                try {
                    dataHandle.Listener.Start(new IPEndPoint(ipAddress, dataHandle.ListenPort));
                    dataHandle.ListenState = DataHandleState.ListenStateStarted;
                }
                catch (Exception ex) {
                    MessageBox.Show(ex.Message, "Error");
                }
            }
        }

        private void MenuItem_StopListener_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in lstViewDataHandle.SelectedItems) {
                DataHandleState dataHandle = item as DataHandleState;

                if (dataHandle == null)
                    continue;
                if (dataHandle.ListenState == DataHandleState.ListenStateStoped)
                    continue;

                // 逻辑上讲，不会出现异常
                dataHandle.Listener.Stop();
                // 同时关闭对应客户端
                dataHandle.Listener.CloseClient();
                dataHandle.ListenState = DataHandleState.ListenStateStoped;
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
                DataHandleState dataHandle = item as DataHandleState;

                if (dataHandle == null)
                    continue;
                if (dataHandle.ListenState == DataHandleState.ListenStateStarted)
                    continue;

                dataHandle.ListenPort = Convert.ToInt32(input.textBox2.Text);
            }
        }

        private void MenuItem_StartTimer_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in lstViewDataHandle.SelectedItems) {
                DataHandleState dataHandle = item as DataHandleState;

                if (dataHandle == null)
                    continue;
                if (dataHandle.TimerState == DataHandleState.TimerStateStarted ||
                    dataHandle.TimerInterval <= 0 || dataHandle.TimerCommand == "")
                    continue;

                dataHandle.Timer.Interval = dataHandle.TimerInterval * 1000;
                dataHandle.Timer.Elapsed += new System.Timers.ElapsedEventHandler((s, ea) => {
                    try {
                        dataHandle.Listener.Send(dataHandle.TimerCommand);
                    }
                    catch (Exception ex) {
                        LogRecord.writeLog(ex);
                    }
                });

                dataHandle.Timer.Start();
                dataHandle.TimerState = DataHandleState.TimerStateStarted;
            }
        }

        private void MenuItem_StopTimer_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in lstViewDataHandle.SelectedItems) {
                DataHandleState dataHandle = item as DataHandleState;

                if (dataHandle == null)
                    continue;
                if (dataHandle.TimerState == DataHandleState.TimerStateStoped)
                    continue;

                dataHandle.Timer.Stop();
                dataHandle.TimerState = DataHandleState.TimerStateStoped;
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
                DataHandleState dataHandle = item as DataHandleState;

                if (dataHandle == null)
                    continue;
                if (dataHandle.TimerState == DataHandleState.TimerStateStarted)
                    continue;

                dataHandle.TimerCommand = input.textBox1.Text;
                if (input.textBox2.Text != "")
                    dataHandle.TimerInterval = double.Parse(input.textBox2.Text);
            }
        }

        private void MenuItem_SendCommand_Click(object sender, RoutedEventArgs e)
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
                ClientPoint client = item as ClientPoint;

                if (client == null)
                    continue;

                try {
                    string[] s = client.IpAddress.Split(":".ToArray());
                    listenerManager.Send(new IPEndPoint(IPAddress.Parse(s[0]), Convert.ToInt32(s[1])),
                        input.textBox1.Text);
                }
                catch (Exception ex) {
                    LogRecord.writeLog(ex);
                }
            }
        }

        private void MenuItem_CloseClient_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in lstViewClientPoint.SelectedItems) {
                ClientPoint client = item as ClientPoint;

                if (client == null)
                    continue;

                string[] s = client.IpAddress.Split(":".ToArray());
                listenerManager.CloseClient(new IPEndPoint(IPAddress.Parse(s[0]), Convert.ToInt32(s[1])));
            }
        }

    }
}
