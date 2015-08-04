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

namespace StationConsoler
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
            InitailizeAsyncSocketListener();
            InitailizePluginManager();
        }

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

        private void InitailizeAsyncSocketListener()
        {
            // Create socket listener & Start user interface model
            sckListener = new AsyncSocketListener();

            sckListener.ListenerStarted += sckListener_ListenerStarted;
            sckListener.ListenerStopped += sckListener_ListenerStopped;
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
                foreach (string file in files) {
                    LoadPlugin(file);
                }
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
                ListenState = DataHandleState.ListenStateNotRunning,
                ChineseName = fvi.ProductName,
                FileName = assemblyName,
                TimerState = DataHandleState.TimerStateNotRunning,
                TimerInterval = 0,
                TimerString = "",
            });

            MenuItem menuItem = new MenuItem();
            menuItem.Header = "仅显示 " + listenPort + " " + fvi.ProductName;
            menuItem.Click += new RoutedEventHandler((s, ea) => {
                for (int i = 0; i < clientPointTable.Count; i++) {
                    DataGridRow row = (DataGridRow)lstClientPoint.ItemContainerGenerator.ContainerFromIndex(i);

                    if (clientPointTable[i].AcceptedPort == listenPort) {
                        row.Visibility = System.Windows.Visibility.Visible;
                    }
                    else {
                        row.Visibility = System.Windows.Visibility.Collapsed;
                    }
                }
            });

            lstClientPoint.ContextMenu.Items.Add(menuItem);
        }

        // Events for AsyncSocketListener =====================================================

        private void sckListener_ListenerStarted(object sender, ListenerEventArgs e)
        {
            var subset = from s in dataHandleTable where s.ListenPort.Equals(e.ListenEP.Port) select s;
            foreach (var item in subset) {
                item.ListenState = DataHandleState.ListenStateRunning;
            }
        }

        private void sckListener_ListenerStopped(object sender, ListenerEventArgs e)
        {
            var subset = from s in dataHandleTable where s.ListenPort.Equals(e.ListenEP.Port) select s;
            foreach (var item in subset) {
                item.ListenState = DataHandleState.ListenStateNotRunning;
            }

            sckListener.CloseClientByListener(e.ListenEP);
        }

        private void sckListener_ClientConnect(object sender, ClientEventArgs e)
        {
            ClientPoint clientPoint = new ClientPoint();

            clientPoint.IpAddress = e.RemoteEP.ToString();
            clientPoint.AcceptedPort = e.LocalEP.Port;
            clientPoint.ConnectTime = DateTime.Now;
            clientPoint.CCID = "";

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                // set value for ObservableCollection object 
                clientPointTable.Add(clientPoint);
            }));
        }

        private void sckListener_ClientDisconn(object sender, ClientEventArgs e)
        {
            var subfirst = (from s in clientPointTable
                            where s.IpAddress.Equals(e.RemoteEP.ToString())
                            select s).First();

            if (subfirst != null) {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    // set value for ObservableCollection object 
                    clientPointTable.Remove(subfirst);
                }));
            }
        }

        private void sckListener_ClientReadMsg(object sender, ClientEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (txtMsg.Text.Length >= 5 * 1024)
                    txtMsg.Clear();

                string logFormat = e.RemoteEP.ToString() + " " + DateTime.Now.ToString() + "接收数据：" + e.Data;

                txtMsg.AppendText(logFormat + "\r\n\r\n");
                txtMsg.ScrollToEnd();
                LogRecord.WriteInfoLog(logFormat);
            }));

            /// @@ 没有办法的办法，必须删改
            string[] str = e.Data.Split("|".ToArray());
            foreach (var item in str) {
                if (item.StartsWith("CCID=")) {
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
            /// @@ 没有办法的办法，必须删改

            /// 调用数据处理插件 ======================================================
            try {
                var subset = from s in dataHandleTable where s.ListenPort == e.LocalEP.Port select s;
                object retValue = pluginManager.Invoke(subset.First().FileName, "IDataHandle", "Handle", new object[] { e.Data });
                if (retValue != null)
                    sckListener.Send(e.RemoteEP, (string)retValue);
            }
            catch (Exception ex) {
                LogRecord.writeLog(ex);
            }
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
            dgDataHandle.ItemsSource = dataHandleTable;
            lstClientPoint.ItemsSource = clientPointTable;

            dgDataHandle.Columns[0].Header = "监听端口";
            dgDataHandle.Columns[0].IsReadOnly = true;
            dgDataHandle.Columns[1].Header = "监听状态";
            dgDataHandle.Columns[1].IsReadOnly = true;
            dgDataHandle.Columns[2].Header = "定时器状态";
            dgDataHandle.Columns[2].IsReadOnly = true;
            dgDataHandle.Columns[3].Header = "模块名";
            dgDataHandle.Columns[3].IsReadOnly = true;
            dgDataHandle.Columns[4].Header = "文件名";
            dgDataHandle.Columns[4].IsReadOnly = true;
            //dgDataHandle.Columns[4].Visibility = Visibility.Hidden;
            dgDataHandle.Columns[5].Header = "定时器时间";
            dgDataHandle.Columns[5].IsReadOnly = false;
            dgDataHandle.Columns[6].Header = "定时器命令";
            dgDataHandle.Columns[6].IsReadOnly = false;

            TextBlock blockNow = new TextBlock();
            TextBlock blockSpan = new TextBlock();
            statusBar.Items.Add(blockNow);
            statusBar.Items.Add(blockSpan);

            DateTime startTime = DateTime.Now;

            System.Windows.Threading.DispatcherTimer timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = new TimeSpan(0, 0, 1);
            timer.Tick += new EventHandler((s, ea) =>
            {
                blockNow.Text = DateTime.Now.ToString();

                blockSpan.Text = "运行时间 " + DateTime.Now.Subtract(startTime).ToString(@"dd\-hh\:mm\:ss");
            });
            timer.Start();
        }

        void timer_Tick(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        private void MenuItem_StartListen_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in dgDataHandle.SelectedItems) {
                DataHandleState dataHandle = item as DataHandleState;

                if (dataHandle == null)
                    continue;

                if (dataHandle.ListenState == DataHandleState.ListenStateRunning)
                    continue;

                List<IPEndPoint> ep = new List<IPEndPoint>();
                IPAddress[] ipAddr = Dns.GetHostAddresses(Dns.GetHostName());
                foreach (IPAddress ip in ipAddr) {
                    if (ip.AddressFamily.Equals(AddressFamily.InterNetwork)) {
                        ep.Add(new IPEndPoint(ip, dataHandle.ListenPort));
                        break;
                    }
                }

                sckListener.Start(ep);
            }
        }

        private void MenuItem_StopListen_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in dgDataHandle.SelectedItems) {
                DataHandleState dataHandle = item as DataHandleState;

                if (dataHandle == null)
                    continue;

                if (dataHandle.ListenState == DataHandleState.ListenStateNotRunning)
                    continue;

                List<IPEndPoint> ep = new List<IPEndPoint>();
                IPAddress[] ipAddr = Dns.GetHostAddresses(Dns.GetHostName());
                foreach (IPAddress ip in ipAddr) {
                    if (ip.AddressFamily.Equals(AddressFamily.InterNetwork)) {
                        ep.Add(new IPEndPoint(ip, dataHandle.ListenPort));
                        break;
                    }
                }

                sckListener.Stop(ep);
            }
        }

        private void MenuItem_Load_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();

            openFileDialog.Filter = "dll files (*.dll)|*.dll|All files (*.*)|*.*";
            openFileDialog.FileName = "";

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                LoadPlugin(openFileDialog.FileName);
            }
        }

        private void MenuItem_Unload_Click(object sender, RoutedEventArgs e)
        {
            List<DataHandleState> handles = new List<DataHandleState>();

            foreach (var item in dgDataHandle.SelectedItems) {
                DataHandleState dataHandle = item as DataHandleState;

                if (dataHandle == null)
                    continue;

                handles.Add(dataHandle);
            }

            foreach (var item in handles) {
                pluginManager.UnLoadPlugin(item.FileName);

                if (item.ListenState == DataHandleState.ListenStateRunning) {
                    List<IPEndPoint> ep = new List<IPEndPoint>();
                    IPAddress[] ipAddr = Dns.GetHostAddresses(Dns.GetHostName());
                    foreach (IPAddress ip in ipAddr) {
                        if (ip.AddressFamily.Equals(AddressFamily.InterNetwork)) {
                            ep.Add(new IPEndPoint(ip, item.ListenPort));
                            break;
                        }
                    }
                    sckListener.Stop(ep);
                }

                dataHandleTable.Remove(item);
            }
        }

        private void MenuItem_StartTimer_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in dgDataHandle.SelectedItems) {
                DataHandleState dataHandle = item as DataHandleState;

                if (dataHandle == null)
                    continue;

                if (dataHandle.TimerInterval <= 0 || dataHandle.TimerString == "")
                    continue;

                dataHandle.Timer = new System.Timers.Timer(dataHandle.TimerInterval);
                dataHandle.Timer.Elapsed += new System.Timers.ElapsedEventHandler((s, ea) => {
                    foreach (var clientPoint in clientPointTable) {
                        if (clientPoint.AcceptedPort == dataHandle.ListenPort) {

                            try {
                                string[] str = clientPoint.IpAddress.Split(":".ToArray());
                                sckListener.Send(new IPEndPoint(IPAddress.Parse(str[0]), Convert.ToInt32(str[1])),
                                    dataHandle.TimerString);
                            }
                            catch (Exception ex) {
                                LogRecord.writeLog(ex);
                            }

                        }
                    }
                });
                dataHandle.Timer.Start();
                dataHandle.TimerState = DataHandleState.TimerStateRunning;

            }
        }

        private void MenuItem_StopTimer_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in dgDataHandle.SelectedItems) {
                DataHandleState dataHandle = item as DataHandleState;

                if (dataHandle == null)
                    continue;

                if (dataHandle.Timer != null) {
                    dataHandle.Timer.Stop();
                    dataHandle.Timer = null;
                    dataHandle.TimerState = DataHandleState.TimerStateNotRunning;
                }
            }
        }

        private void MenuItem_SendCommand_Click(object sender, RoutedEventArgs e)
        {
            string sendmsg = Microsoft.VisualBasic.Interaction.InputBox("请输入要发送的命令", "手动命令发送框", "!A1?", 200, 200);
            if (sendmsg == "") {
                return;
            }

            foreach (var item in lstClientPoint.SelectedItems) {
                ClientPoint client = item as ClientPoint;

                if (client == null)
                    return;

                try {
                    string[] s = client.IpAddress.Split(":".ToArray());
                    sckListener.Send(new IPEndPoint(IPAddress.Parse(s[0]), Convert.ToInt32(s[1])),
                        sendmsg);
                }
                catch (Exception ex) {
                    LogRecord.writeLog(ex);
                }
            }
        }

        private void MenuItem_CloseClient_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in lstClientPoint.SelectedItems) {
                ClientPoint client = item as ClientPoint;

                if (client == null)
                    return;

                string[] s = client.IpAddress.Split(":".ToArray());
                sckListener.CloseClient(new IPEndPoint(IPAddress.Parse(s[0]), Convert.ToInt32(s[1])));
            }
        }
        
        private void MenuItem_ShowClient_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < lstClientPoint.Items.Count; i++) {
                DataGridRow row = (DataGridRow)lstClientPoint.ItemContainerGenerator.ContainerFromIndex(i);
                row.Visibility = System.Windows.Visibility.Visible;
            }
        }


    }
}
