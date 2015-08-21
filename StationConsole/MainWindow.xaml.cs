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
using Mnn.MnnPlugin;
using Mnn.MnnSocket;
using Mnn.MnnUtil;

namespace StationConsole
{
    public class ClientPointTable : ObservableCollection<ClientPointState> { }

    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            InitailizeWindowName();
            InitailizeDataLayer();

            InitailizeOthers();
        }

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

        private void InitailizeDataLayer()
        {
            /*
            IPAddress[] ipAddr = Dns.GetHostAddresses(Dns.GetHostName());
            foreach (IPAddress ip in ipAddr) {
                if (ip.AddressFamily.Equals(AddressFamily.InterNetwork)) {
                    DataLayer.ipAddress = ip;
                    break;
                }
            }
             * */

            DataLayer.ipAddress = IPAddress.Parse("0.0.0.0");
            DataLayer.dataHandleTable = new ObservableCollection<DataHandleState>();
            DataLayer.clientPointTable = (ClientPointTable)this.Resources["clientPointTable"];
        }

        private void InitailizeOthers()
        {
            // 1.Get all files in directory "DataHandles"
            string pluginPath = System.AppDomain.CurrentDomain.BaseDirectory + @"\DataHandles";

            if (Directory.Exists(pluginPath)) {
                string[] files = Directory.GetFiles(pluginPath);

                // Load dll files one by one
                foreach (var item in files)
                    AtLoadPlugin(item);
            }

            // 2.Data Source Binding
            //lstViewClientPoint.ItemsSource = DataLayer.clientPointTable;
            lstViewDataHandle.ItemsSource = DataLayer.dataHandleTable;

            // 3.Display TimeRun
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

        // At Commands ========================================================================

        private void AtLoadPlugin(string filePath)
        {
            int listenPort = 0;
            DataHandleState dataHandle = new DataHandleState();
            dataHandle.InitializeSource();

            try {
                listenPort = dataHandle.LoadDataHandlePlugin(filePath);
            }
            catch (Exception ex) {
                MessageBox.Show(ex.Message, "Error");
                return;
            }

            // 加载模块已经成功
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
            DataLayer.dataHandleTable.Add(dataHandle);
        }

        private void AtUnLoadPlugin(DataHandleState dataHandle)
        {
            // 关闭端口
            if (dataHandle.ListenState == DataHandleState.ListenStateStarted) {
                dataHandle.StopListener();
                dataHandle.ListenState = DataHandleState.ListenStateStoped;
                dataHandle.StopHandleData();
            }

            // 关闭定时器
            if (dataHandle.TimerState == DataHandleState.TimerStateStarted) {
                dataHandle.StopTimerCommand();
                dataHandle.TimerState = DataHandleState.TimerStateStoped;
            }

            // 卸载模块
            dataHandle.UnloadDataHandlePlugin();

            // 移出 table
            DataLayer.dataHandleTable.Remove(dataHandle);
        }

        private void AtStartListener(DataHandleState dataHandle)
        {
            if (dataHandle.ListenState == DataHandleState.ListenStateStarted)
                return;

            // 端口可能已经被其他程序监听
            try {
                dataHandle.StartListener(new IPEndPoint(DataLayer.ipAddress, dataHandle.ListenPort));
                dataHandle.ListenState = DataHandleState.ListenStateStarted;
                dataHandle.StartHandleData();
            }
            catch (Exception ex) {
                MessageBox.Show(ex.Message, "Error");
            }
        }

        private void AtStopListener(DataHandleState dataHandle)
        {
            if (dataHandle.ListenState == DataHandleState.ListenStateStoped)
                return;

            // 逻辑上讲，不会出现异常
            dataHandle.StopListener();
            dataHandle.ListenState = DataHandleState.ListenStateStoped;
            dataHandle.StopHandleData();
        }

        private void AtSetListener(DataHandleState dataHandle, int listenPort)
        {
            if (dataHandle.ListenState == DataHandleState.ListenStateStarted)
                return;

            dataHandle.ListenPort = listenPort;
        }

        private void AtStartTimer(DataHandleState dataHandle)
        {
            if (dataHandle.TimerState == DataHandleState.TimerStateStarted ||
                dataHandle.TimerInterval <= 0 || dataHandle.TimerCommand == "")
                return;

            dataHandle.StartTimerCommand(dataHandle.TimerInterval * 1000, dataHandle.TimerCommand);
            dataHandle.TimerState = DataHandleState.TimerStateStarted;
        }

        private void AtStopTimer(DataHandleState dataHandle)
        {
            if (dataHandle.TimerState == DataHandleState.TimerStateStoped)
                return;

            dataHandle.StopTimerCommand();
            dataHandle.TimerState = DataHandleState.TimerStateStoped;
        }

        private void AtSetTimer(DataHandleState dataHandle, string cmd, double interval)
        {
            if (dataHandle.TimerState == DataHandleState.TimerStateStarted)
                return;

            dataHandle.TimerCommand = cmd;
            dataHandle.TimerInterval = interval;
        }

        private void AtClientSendMessage(ClientPointState client, string msg)
        {
            try {
                string[] strTmp = client.IpAddress.Split(":".ToArray());
                var subset = from s in DataLayer.dataHandleTable where s.ListenPort == client.AcceptedPort select s.Listener;
                if (subset.Count() != 0)
                    subset.First().Send(new IPEndPoint(IPAddress.Parse(strTmp[0]), Convert.ToInt32(strTmp[1])),
                                        Encoding.Default.GetBytes(msg));

            }
            catch (Exception) { }
        }

        private void AtClientClose(ClientPointState client)
        {
            string[] strTmp = client.IpAddress.Split(":".ToArray());

            var subset = from s in DataLayer.dataHandleTable where s.ListenPort == client.AcceptedPort select s.Listener;
            if (subset.Count() != 0)
                subset.First().CloseClient(new IPEndPoint(IPAddress.Parse(strTmp[0]), Convert.ToInt32(strTmp[1])));
        }

        // Events for AsyncSocketListenerItem =================================================

        private void SocketListener_ClientConnect(object sender, ClientEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                // set value for ObservableCollection object 
                currentClientCount.Text = (Convert.ToInt32(currentClientCount.Text) + 1).ToString();
                historyClientOpenCount.Text = (Convert.ToInt32(historyClientOpenCount.Text) + 1).ToString();

                var subset = from s in DataLayer.dataHandleTable
                            where s.ListenPort == e.LocalEP.Port
                            select s.ChineseName;
                string acceptedName = subset.Count() != 0 ? subset.First() : "";

                ClientPointState clientPoint = new ClientPointState();

                clientPoint.IpAddress = e.RemoteEP.ToString();
                clientPoint.AcceptedPort = e.LocalEP.Port;
                clientPoint.AcceptedName = acceptedName;
                clientPoint.ConnectTime = DateTime.Now;
                clientPoint.CCID = "";

                lock (DataLayer.clientPointTable) {
                    DataLayer.clientPointTable.Add(clientPoint);
                }
            }));
        }

        private void SocketListener_ClientDisconn(object sender, ClientEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                // set value for ObservableCollection object 
                currentClientCount.Text = (Convert.ToInt32(currentClientCount.Text) - 1).ToString();
                historyClientCloseCount.Text = (Convert.ToInt32(historyClientCloseCount.Text) + 1).ToString();

                lock (DataLayer.clientPointTable) {
                    var subset = from s in DataLayer.clientPointTable
                                    where s.IpAddress.Equals(e.RemoteEP.ToString())
                                    select s;

                    if (subset.Count() != 0)
                        DataLayer.clientPointTable.Remove(subset.First());
                }
            }));
        }

        private void SocketListener_ClientReadMsg(object sender, ClientEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                string msg = Encoding.Default.GetString(e.Data);

                if (txtMsg.Text.Length >= 20 * 1024) {
                    txtMsg.Clear();
                }

                string logFormat = e.RemoteEP.ToString() + " " + DateTime.Now.ToString() + "接收数据：" + msg;

                txtMsg.AppendText(logFormat + "\r\n\r\n");
                txtMsg.ScrollToEnd();

                /// @@ 没有办法的办法，必须删改
                string[] strMsg = msg.Split("|".ToArray());
                foreach (var item in strMsg) {
                    if (item.StartsWith("CCID=")) {

                        lock (DataLayer.clientPointTable) {
                            // 从客户表中找到与远程ip地址相同的条目，更新CCID
                            foreach (var client in DataLayer.clientPointTable) {
                                if (client.IpAddress.Equals(e.RemoteEP.ToString())) {
                                    client.CCID = item.Substring(5);
                                }
                            }

                            // 基站不会自动断开前一次连接...相同CCID的连上来后，断开前面的连接
                            foreach (var client in DataLayer.clientPointTable) {
                                // 1.CCID相同 2.远程IP地址不相同 3.本地端口相同
                                if (client.CCID.Equals(item.Substring(5)) && !client.IpAddress.Equals(e.RemoteEP.ToString()) &&
                                    client.AcceptedPort.Equals(e.LocalEP.Port)) {
                                    string[] strTmp = client.IpAddress.Split(":".ToArray());

                                    var subset = from s in DataLayer.dataHandleTable where s.ListenPort == client.AcceptedPort select s.Listener;
                                    if (subset.Count() != 0)
                                        subset.First().CloseClient(new IPEndPoint(IPAddress.Parse(strTmp[0]), Convert.ToInt32(strTmp[1])));
                                    break;
                                }
                            }
                        }
                    }
                    else if (item.Contains("R=POWER OFF OK")) {
                        var subset = from s in DataLayer.dataHandleTable where s.ListenPort == e.LocalEP.Port select s.Listener;
                        if (subset.Count() != 0)
                            subset.First().CloseClient(e.RemoteEP);
                    }
                }
                /// @@ 没有办法的办法，必须删改

                /// 调用数据处理插件 ======================================================
                try {
                    var subFirst = (from s in DataLayer.dataHandleTable where s.ListenPort == e.LocalEP.Port select s).First();
                    subFirst.AppendData(e.RemoteEP, msg);
                }
                catch (Exception ex) {
                    // 正常不会出现subFirst不存在的情况，如果出现，应该记录
                    Logger.WriteException(ex);
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
            }));
        }

        // Events for itself ==================================================================

        private void MenuItem_LoadPlugin_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();

            openFileDialog.Filter = "dll files (*.dll)|*.dll|All files (*.*)|*.*";
            openFileDialog.FileName = "";

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                AtLoadPlugin(openFileDialog.FileName);
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
                AtUnLoadPlugin(item);
        }

        private void MenuItem_StartListener_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in lstViewDataHandle.SelectedItems) {
                DataHandleState dataHandle = item as DataHandleState;
                if (dataHandle == null)
                    continue;

                AtStartListener(dataHandle);
            }
        }

        private void MenuItem_StopListener_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in lstViewDataHandle.SelectedItems) {
                DataHandleState dataHandle = item as DataHandleState;
                if (dataHandle == null)
                    continue;

                AtStopListener(dataHandle);
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

                AtSetListener(dataHandle, Convert.ToInt32(input.textBox2.Text));
            }
        }

        private void MenuItem_StartTimer_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in lstViewDataHandle.SelectedItems) {
                DataHandleState dataHandle = item as DataHandleState;
                if (dataHandle == null)
                    continue;

                AtStartTimer(dataHandle);
            }
        }

        private void MenuItem_StopTimer_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in lstViewDataHandle.SelectedItems) {
                DataHandleState dataHandle = item as DataHandleState;
                if (dataHandle == null)
                    continue;

                AtStopTimer(dataHandle);
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

                if (input.textBox2.Text == "")
                    AtSetTimer(dataHandle, input.textBox1.Text, dataHandle.TimerInterval);
                else
                    AtSetTimer(dataHandle, input.textBox1.Text, Convert.ToDouble(input.textBox2.Text));
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

            foreach (var item in lstViewClientPoint.SelectedItems) {
                ClientPointState client = item as ClientPointState;
                if (client == null)
                    continue;

                AtClientSendMessage(client, input.textBox1.Text);
            }
        }

        private void MenuItem_ClientClose_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in lstViewClientPoint.SelectedItems) {
                ClientPointState client = item as ClientPointState;
                if (client == null)
                    continue;

                AtClientClose(client);
            }
        }

    }
}
