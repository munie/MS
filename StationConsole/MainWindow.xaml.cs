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
            InitailizeAsyncSocketListener();
            InitailizePluginManager();

            UpdateClientPointMenu();
        }

        private IPAddress ipAddress = null;

        private AsyncSocketListener sckListener = null;
        private PluginManager pluginManager = null;

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

        private void InitailizeAsyncSocketListener()
        {
            // Create socket listener & Start user interface model
            sckListener = new AsyncSocketListener();

            sckListener.ClientConnect += sckListener_ClientConnect;
            sckListener.ClientDisconn += sckListener_ClientDisconn;
            sckListener.ClientReadMsg += sckListener_ClientReadMsg;
            sckListener.ClientSendMsg += sckListener_ClientSendMsg;
        }

        private void InitailizePluginManager()
        {
            // Start data processing model
            pluginManager = new PluginManager();

            // Get all files in directory "DataHandles"
            string pluginPath = System.AppDomain.CurrentDomain.BaseDirectory + @"\DataHandles";

            if (Directory.Exists(pluginPath)) {
                string[] files = Directory.GetFiles(pluginPath);

                // Load dll files one by one
                foreach (string file in files)
                    LoadPlugin(file);
            }
        }

        private void LoadPlugin(string filePath)
        {
            string assemblyName = null;
            int listenPort = 0;

            try {
                assemblyName = pluginManager.LoadPlugin(filePath);
            }
            catch (ApplicationException ex) {
                MessageBox.Show(ex.Message, "Error");
                return;
            }

            try {
                listenPort = (int)pluginManager.Invoke(assemblyName, "IDataHandle", "GetIdentity", null);
            }
            catch (Exception ex) {
                pluginManager.UnLoadPlugin(assemblyName);
                MessageBox.Show(ex.Message, "Error");
                return;
            }

            // 加载模块已经成功
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(filePath);
            dataHandleTable.Add(new DataHandleState
            {
                ListenPort = listenPort,
                ListenState = DataHandleState.ListenStateStoped,
                ChineseName = fvi.ProductName,
                FileName = assemblyName,
                TimerState = DataHandleState.TimerStateStoped,
                TimerInterval = 0,
                TimerCommand = "",
            });
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
                    lock (clientPointTable) {
                        for (int i = 0; i < clientPointTable.Count; i++) {
                            ListViewItem row = (ListViewItem)lstViewClientPoint.ItemContainerGenerator.ContainerFromIndex(i);

                            if (clientPointTable[i].AcceptedPort == item.ListenPort) {
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

        // Events for AsyncSocketListener =====================================================

        private void sckListener_ClientConnect(object sender, ClientEventArgs e)
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

        private void sckListener_ClientDisconn(object sender, ClientEventArgs e)
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

        private void sckListener_ClientReadMsg(object sender, ClientEventArgs e)
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
                            // 更新CCID
                            foreach (var client in clientPointTable) {
                                if (client.IpAddress.Equals(e.RemoteEP.ToString())) {
                                    client.CCID = item.Substring(5);
                                }
                            }

                            // 基站不会自动断开前一次连接...相同CCID的连上来后，断开前面的连接
                            foreach (var client in clientPointTable) {
                                if (client.CCID.Equals(item.Substring(5)) && !client.IpAddress.Equals(e.RemoteEP.ToString())) {
                                    string[] s = client.IpAddress.Split(":".ToArray());
                                    sckListener.CloseClient(new IPEndPoint(IPAddress.Parse(s[0]), Convert.ToInt32(s[1])));
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
                    object retValue = pluginManager.Invoke(subFirst.FileName, "IDataHandle", "Handle", new object[] { e.Data });
                    if (retValue != null)
                        sckListener.Send(e.RemoteEP, (string)retValue);
                }
                catch (Exception ex) {
                    LogRecord.writeLog(ex);
                }
            }));
        }

        private void sckListener_ClientSendMsg(object sender, ClientEventArgs e)
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
            foreach (var item in handles) {
                // 关闭端口
                if (item.ListenState == DataHandleState.ListenStateStarted) {
                    List<IPEndPoint> ep = new List<IPEndPoint>() { new IPEndPoint(ipAddress, item.ListenPort) };
                    sckListener.Stop(ep);
                    item.ListenState = DataHandleState.ListenStateStoped;
                    // 同时关闭对应客户端
                    sckListener.CloseClientByListener(ep.First());
                }

                // 关闭定时器
                if (item.TimerState == DataHandleState.TimerStateStarted) {
                    item.Timer.Stop();
                    item.TimerState = DataHandleState.TimerStateStoped;
                }

                // 卸载模块
                pluginManager.UnLoadPlugin(item.FileName);

                // 移出 table
                dataHandleTable.Remove(item);
            }

            // 更新菜单
            UpdateClientPointMenu();
        }

        private void MenuItem_StartListen_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in lstViewDataHandle.SelectedItems) {
                DataHandleState dataHandle = item as DataHandleState;

                if (dataHandle == null)
                    continue;

                if (dataHandle.ListenState == DataHandleState.ListenStateStarted)
                    continue;

                // 端口可能已经被其他程序监听
                try {
                    List<IPEndPoint> ep = new List<IPEndPoint>() { new IPEndPoint(ipAddress, dataHandle.ListenPort) };
                    sckListener.Start(ep);
                    dataHandle.ListenState = DataHandleState.ListenStateStarted;
                }
                catch (Exception ex) {
                    MessageBox.Show(ex.Message, "Error");
                }
            }
        }

        private void MenuItem_StopListen_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in lstViewDataHandle.SelectedItems) {
                DataHandleState dataHandle = item as DataHandleState;

                if (dataHandle == null)
                    continue;

                if (dataHandle.ListenState == DataHandleState.ListenStateStoped)
                    continue;

                // 逻辑上讲，不会出现异常
                List<IPEndPoint> ep = new List<IPEndPoint>() { new IPEndPoint(ipAddress, dataHandle.ListenPort) };
                sckListener.Stop(ep);
                dataHandle.ListenState = DataHandleState.ListenStateStoped;
                // 同时关闭对应客户端
                sckListener.CloseClientByListener(ep.First());
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

                dataHandle.Timer = new System.Timers.Timer(dataHandle.TimerInterval * 1000);
                dataHandle.Timer.Elapsed += new System.Timers.ElapsedEventHandler((s, ea) => {
                    lock (clientPointTable) {
                        foreach (var clientPoint in clientPointTable) {
                            if (clientPoint.AcceptedPort == dataHandle.ListenPort) {

                                try {
                                    string[] str = clientPoint.IpAddress.Split(":".ToArray());
                                    sckListener.Send(new IPEndPoint(IPAddress.Parse(str[0]), Convert.ToInt32(str[1])),
                                        dataHandle.TimerCommand);
                                }
                                catch (Exception ex) {
                                    LogRecord.writeLog(ex);
                                }

                            }
                        }
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
            input.Title = "定时器设置";
            input.textBlock1.Text = "命令";
            input.textBlock2.Text = "时间间隔";
            input.textBox1.Text = "!A0#";
            input.textBox2.Focus();
            if (input.ShowDialog() == true) {
                foreach (var item in lstViewDataHandle.SelectedItems) {
                    DataHandleState dataHandle = item as DataHandleState;

                    if (dataHandle == null)
                        continue;

                    dataHandle.TimerCommand = input.textBox1.Text;
                    if (input.textBox2.Text != "")
                        dataHandle.TimerInterval = double.Parse(input.textBox2.Text);
                }
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
            if (input.ShowDialog() == true) {
                foreach (var item in lstViewClientPoint.SelectedItems) {
                    ClientPoint client = item as ClientPoint;

                    if (client == null)
                        continue;

                    try {
                        string[] s = client.IpAddress.Split(":".ToArray());
                        sckListener.Send(new IPEndPoint(IPAddress.Parse(s[0]), Convert.ToInt32(s[1])),
                            input.textBox1.Text);
                    }
                    catch (Exception ex) {
                        LogRecord.writeLog(ex);
                    }
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
                sckListener.CloseClient(new IPEndPoint(IPAddress.Parse(s[0]), Convert.ToInt32(s[1])));
            }
        }

    }
}
