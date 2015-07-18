using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Diagnostics;
using System.Configuration;
using MnnSocket;
using MnnUtils;
using DataProcess;

namespace TransferStation
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            // Create socket listener & Start user interface model
            sckListener = new AsyncSocketListener();

            // Start data processing model
            dataCenter = new DataConvertCenter(sckListener);

            sckListener.ListenerStarted += sckListener_ListenerStarted;
            sckListener.ListenerStopped += sckListener_ListenerStopped;
            sckListener.ClientConnect += sckListener_ClientConnect;
            sckListener.ClientDisconn += sckListener_ClientDisconn;
            sckListener.ClientReadMsg += sckListener_ClientReadMsg;
            sckListener.ClientSendMsg += sckListener_ClientSendMsg;

            dataCenter.DataHandleSuccess += dataCenter_DataHandleSuccess;

            InitializeComponent();
        }

        private AsyncSocketListener sckListener = null;
        private DataConvertCenter dataCenter = null;
        private BindingList<StationSetting> stationTable = new BindingList<StationSetting>();

        // Methods ============================================================================

        private void initailizeWindowName()
        {
            // Format Main Form's Name
            Assembly asm = Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(asm.Location);
            this.Text = string.Format("{0} {1}.{2} - Powered By {3}",
                fvi.ProductName, fvi.ProductMajorPart, fvi.ProductMinorPart, fvi.CompanyName);   
        }

        private void initailizeStationSetting()
        {
            Dictionary<int, string> dataHandleStatus = dataCenter.GetDataHandleStatus();

            foreach (var item in dataHandleStatus) {
                FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(item.Value);
                stationTable.Add(new StationSetting
                {
                    Port = item.Key,
                    IsPermitListen = false,
                    ListenState = "未启动",
                    NameChinese = fvi.ProductName,
                    Name = fvi.InternalName
                });
            }

            StationSetting.PropertyChanged += stationTable_PropertyChanged;

            dgvStation.DataSource = stationTable;
            //dgvStation.DataBindings.Add("DataSource", this, "stationTable", false, DataSourceUpdateMode.OnPropertyChanged);
            dgvStation.Columns[0].HeaderText = "端口";
            dgvStation.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            dgvStation.Columns[0].ReadOnly = true;
            dgvStation.Columns[1].HeaderText = "允许监听";
            dgvStation.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            dgvStation.Columns[2].HeaderText = "状态";
            dgvStation.Columns[2].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            dgvStation.Columns[2].ReadOnly = true;
            dgvStation.Columns[3].HeaderText = "模块名";
            dgvStation.Columns[3].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            dgvStation.Columns[3].ReadOnly = true;
            dgvStation.Columns[4].HeaderText = "文件名";
            dgvStation.Columns[4].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            dgvStation.Columns[4].ReadOnly = true;
        }

        private Dictionary<string, string> ConfigTranslate(string str)
        {
            Dictionary<string, string> dct = new Dictionary<string, string>();

            string[] strSplit = str.Split(";".ToArray());
            foreach (string s in strSplit) {
                string[] sSplit = s.Split("=".ToArray());
                dct.Add(sSplit[0], sSplit[1]);
            }

            return dct;
        }

        // Events for AsyncSocketListener =====================================================

        private void sckListener_ListenerStarted(object sender, ListenerEventArgs e)
        {
            if (this.InvokeRequired) {
                this.Invoke(new EventHandler<ListenerEventArgs>(sckListener_ListenerStarted),
                    new object[] { sender, e });
                return;
            }

            var subset = from s in stationTable where s.Port.Equals(e.ListenEP.Port) select s;
            foreach (var item in subset) {
                item.ListenState = "已启动";
            }

            dgvStation.Refresh();
        }

        private void sckListener_ListenerStopped(object sender, ListenerEventArgs e)
        {
            if (this.InvokeRequired) {
                this.Invoke(new EventHandler<ListenerEventArgs>(sckListener_ListenerStopped),
                    new object[] { sender, e });
                return;
            }

            var subset = from s in stationTable where s.Port.Equals(e.ListenEP.Port) select s;
            foreach (var item in subset) {
                item.ListenState = "未启动";

                sckListener.CloseClientByListener(e.ListenEP);
            }

            dgvStation.Refresh();
        }

        private void sckListener_ClientConnect(object sender, ClientEventArgs e)
        {
            if (this.InvokeRequired) {
                this.Invoke(new EventHandler<ClientEventArgs>(sckListener_ClientConnect),
                    new object[] { sender, e });
                return;
            }

            ListViewItem item = new ListViewItem();
            item.SubItems[0].Text = e.RemoteEP.ToString();
            item.SubItems.Add(e.LocalEP.Port.ToString());
            item.SubItems.Add(DateTime.Now.ToString());
            item.SubItems.Add("-");

            lock (lstClient.Items) {
                lstClient.Items.Add(item);
            }
        }

        private void sckListener_ClientDisconn(object sender, ClientEventArgs e)
        {
            if (this.InvokeRequired) {
                this.Invoke(new EventHandler<ClientEventArgs>(sckListener_ClientDisconn),
                    new object[] { sender, e });
                return;
            }

            lock (lstClient.Items) {
                for (int i = 0; i < lstClient.Items.Count; i++) {
                    if (lstClient.Items[i].SubItems[0].Text.Equals(e.RemoteEP.ToString())) {
                        lstClient.Items.Remove(lstClient.Items[i]);
                    }
                }
            }
        }

        private void sckListener_ClientReadMsg(object sender, ClientEventArgs e)
        {
            if (this.InvokeRequired) {
                this.Invoke(new EventHandler<ClientEventArgs>(sckListener_ClientReadMsg),
                    new object[] { sender, e });
                return;
            }

            if (txtMsg.Text.Length >= 5 * 1024)
                txtMsg.Clear();

            string logFormat = e.RemoteEP.ToString() + " " + DateTime.Now.ToString() + "接收数据：" + e.Data;

            txtMsg.AppendText(logFormat + "\r\n");
            LogRecord.WriteInfoLog(logFormat);
        }

        private void sckListener_ClientSendMsg(object sender, ClientEventArgs e)
        {
            if (this.InvokeRequired) {
                this.Invoke(new EventHandler<ClientEventArgs>(sckListener_ClientSendMsg),
                    new object[] { sender, e });
                return;
            }

            string logFormat = e.RemoteEP.ToString() + " " + DateTime.Now.ToString() + "发送数据：" + e.Data;

            txtMsg.AppendText(logFormat + "\r\n");
            LogRecord.WriteInfoLog(logFormat);
        }

        private void dataCenter_DataHandleSuccess(object sender, DataHandleSuccessEventArgs e)
        {
            if (this.InvokeRequired) {
                this.Invoke(new EventHandler<DataHandleSuccessEventArgs>(dataCenter_DataHandleSuccess),
                    new object[] { sender, e });
                return;
            }

            lock (lstClient) {
                for (int i = 0; i < lstClient.Items.Count; i++) {
                    if (lstClient.Items[i].SubItems[0].Text.Equals(e.EP.ToString()))
                        lstClient.Items[i].SubItems[3].Text = e.CCID;
                    
                }
            }
        }

        private void stationTable_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (btnStartListen.Enabled == true)
                return;

            StationSetting ss = sender as StationSetting;

            List<IPEndPoint> ep = new List<IPEndPoint>();
            IPAddress[] ipAddr = Dns.GetHostAddresses(Dns.GetHostName());
            foreach (IPAddress ip in ipAddr) {
                if (ip.AddressFamily.Equals(AddressFamily.InterNetwork)) {
                    ep.Add(new IPEndPoint(ip, ss.Port));
                    break;
                }
            }

            try {
                if (ss.IsPermitListen && ss.ListenState.Equals("已启动") ||
                    !ss.IsPermitListen && ss.ListenState.Equals("未启动"))
                    return;

                if (ss.IsPermitListen)
                    sckListener.Start(ep);
                else
                    sckListener.Stop(ep);
            }
            catch (ApplicationException ex) {
                LogRecord.writeLog(ex);
                //Console.WriteLine(ex.ToString());
            }
            catch (Exception ex) {
                LogRecord.writeLog(ex);
                //Console.WriteLine(ex.ToString());
            }
        }

        // Events for itself ==================================================================

        private void MainForm_Load(object sender, EventArgs e)
        {
            initailizeWindowName();
            initailizeStationSetting();

            // Initialize list of client
            lstClient.Columns.Add("基站IP", 155, HorizontalAlignment.Center);
            lstClient.Columns.Add("监听端口", 80, HorizontalAlignment.Center);
            lstClient.Columns.Add("连接时间", 159, HorizontalAlignment.Center);
            lstClient.Columns.Add("CCID", 180, HorizontalAlignment.Center);

            btnStopListen.Enabled = false;
        }

        private void MainFrom_FormClosing(object sender, FormClosingEventArgs e)
        {

            string psward = Microsoft.VisualBasic.Interaction.InputBox(
                "请输入密码", "密码框", "",
                this.Left + 200, this.Top + 150);

            if (psward != "zjhtht") {
                MessageBox.Show("密码错误");
                e.Cancel = true;
            }
            else {
                e.Cancel = false;
            }
        }

        private void btnStartListen_Click(object sender, EventArgs e)
        {
            List<IPEndPoint> ep = new List<IPEndPoint>();
            IPAddress[] ipAddr = Dns.GetHostAddresses(Dns.GetHostName());
            foreach (IPAddress ip in ipAddr) {
                if (ip.AddressFamily.Equals(AddressFamily.InterNetwork)) {
                    foreach (StationSetting item in stationTable) {
                        if (item.IsPermitListen)
                            ep.Add(new IPEndPoint(ip, item.Port));
                    }
                    break;
                }
            }

            try {
                // Start socket listener
                sckListener.Start(ep);

                btnStartListen.Enabled = false;
                btnStopListen.Enabled = true;
            }
            catch (Exception ex) {
                MessageBox.Show(ex.Message, "启动监听失败");
                LogRecord.writeLog(ex);
            }
        }

        private void btnStopListen_Click(object sender, EventArgs e)
        {
            /*
            List<IPEndPoint> ep = null;
            IPAddress[] ipAddr = Dns.GetHostAddresses(Dns.GetHostName());
            foreach (IPAddress ip in ipAddr) {
                if (ip.AddressFamily.Equals(AddressFamily.InterNetwork)) {
                    ep = new List<IPEndPoint>() { new IPEndPoint(ip, Convert.ToInt32(txtPort.Text)) };
                    break;
                }
            }

            //sckListener.Stop(ep);
             * */
            sckListener.Stop();
            sckListener.CloseClient();

            lock (lstClient.Items) {
                lstClient.Items.Clear();
            }

            btnStartListen.Enabled = true;
            btnStopListen.Enabled = false;
        }

        private void lstClient_MouseClick(object sender, MouseEventArgs e)
        {
            //判断是否为右击事件
            if (e.Button == MouseButtons.Right) {
                var hitTestInfo = lstClient.HitTest(e.X, e.Y);
                if (hitTestInfo.Item != null) {
                    Point loc = e.Location;
                    loc.Offset(lstClient.Location);
                    // Adjust context menu (or it's contents) based on hitTestInfo details     
                    this.contextMenuStrip1.Show(this, loc);
                }
            }
        }

        private void 发送命令ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string sendmsg = Microsoft.VisualBasic.Interaction.InputBox("请输入要发送的命令", "手动命令发送框", "!A1?", 200, 200);
            if (sendmsg == "") {
                return;
            }

            lock (lstClient.Items) {
                for (int i = 0; i < lstClient.SelectedItems.Count; i++) {
                    string[] s = lstClient.SelectedItems[i].SubItems[0].Text.Split(":".ToArray());

                    sckListener.Send(new IPEndPoint(IPAddress.Parse(s[0]), Convert.ToInt32(s[1])),
                        sendmsg);
                }
            }
        }

        private void 关闭连接ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            lock (lstClient.Items) {
                for (int i = 0; i < lstClient.SelectedItems.Count; i++) {
                    string[] s = lstClient.SelectedItems[i].SubItems[0].Text.Split(":".ToArray());

                    sckListener.CloseClient(new IPEndPoint(IPAddress.Parse(s[0]), Convert.ToInt32(s[1])));
                }
            }
        }

        private void 载入新模块ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.openFileDialog1.Filter = "dll files (*.dll)|*.dll|All files (*.*)|*.*";
            this.openFileDialog1.FileName = "";

            if (this.openFileDialog1.ShowDialog() == DialogResult.OK) {
                try {
                    dataCenter.LoadSpecialPlugins(this.openFileDialog1.FileName);
                }
                catch (ApplicationException ex) {
                    MessageBox.Show(ex.Message, "Error");
                    LogRecord.writeLog(ex);
                }

                Dictionary<int, string> dataHandleStatus = dataCenter.GetDataHandleStatus();
                List<int> ports = new List<int>();

                foreach (var item in stationTable)
                    ports.Add(item.Port);
                var subset = from s in dataHandleStatus where !ports.Contains(s.Key) select s;

                foreach (var item in subset) {
                    FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(item.Value);
                    stationTable.Add(new StationSetting
                    {
                        Port = item.Key,
                        IsPermitListen = false,
                        ListenState = "未启动",
                        NameChinese = fvi.ProductName,
                        Name = fvi.InternalName
                    });
                }
            }
        }

    }
}
