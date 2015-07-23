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
using System.IO;
using MnnSocket;
using MnnUtils;
using MnnPlugin;

namespace TransferStation
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        private AsyncSocketListener sckListener = null;
        private PluginManager pluginManager = null;
        private BindingList<DataHandleState> dataHandleTable = new BindingList<DataHandleState>();

        // Methods ============================================================================

        private void initailizeWindowName()
        {
            // Format Main Form's Name
            Assembly asm = Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(asm.Location);
            this.Text = string.Format("{0} {1}.{2} - Powered By {3}",
                fvi.ProductName, fvi.ProductMajorPart, fvi.ProductMinorPart, fvi.CompanyName);   
        }

        private void initailizeAsyncSocketListener()
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

        private void initailizePluginManager()
        {
            // Start data processing model
            pluginManager = new PluginManager();

            // Get all files in directory "DataHandles"
            string pluginPath = System.AppDomain.CurrentDomain.BaseDirectory + @"\DataHandles";

            if (Directory.Exists(pluginPath)) {
                string[] files = Directory.GetFiles(pluginPath);

                // Load dll files one by one
                foreach (string file in files) {
                    string assemblyName = null;
                    try {
                        assemblyName = pluginManager.LoadPlugin(file);
                    }
                    catch (Exception ex) {
                        LogRecord.writeLog(ex);
                        continue;
                    }

                    // Verify modules
                    try {
                        pluginManager.Invoke(assemblyName, "IDataHandle", "GetIdentity", null);
                    }
                    catch (Exception ex) {
                        pluginManager.UnLoadPlugin(assemblyName);
                        LogRecord.writeLog(ex);
                        continue;
                    }
                }
            }
        }

        private void initailizeDataHandleState()
        {
            Dictionary<string, string> pluginStatus = pluginManager.GetPluginStatus();

            foreach (var item in pluginStatus) {
                FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(item.Value);
                dataHandleTable.Add(new DataHandleState
                {
                    // 加载模块时已经验证过了
                    Port = (int)pluginManager.Invoke(item.Key, "IDataHandle", "GetIdentity", null),
                    IsPermitListen = false,
                    ListenState = "未启动",
                    ChineseName = fvi.ProductName,
                    FileName = item.Key
                });
            }

            DataHandleState.PropertyChanged += dataHandleTable_PropertyChanged;

            dgvStation.DataSource = dataHandleTable;
            //dgvStation.DataBindings.Add("DataSource", this, "dataHandleTable", false, DataSourceUpdateMode.OnPropertyChanged);
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

        // Events for AsyncSocketListener =====================================================

        private void sckListener_ListenerStarted(object sender, ListenerEventArgs e)
        {
            if (this.InvokeRequired) {
                this.Invoke(new EventHandler<ListenerEventArgs>(sckListener_ListenerStarted),
                    new object[] { sender, e });
                return;
            }

            var subset = from s in dataHandleTable where s.Port.Equals(e.ListenEP.Port) select s;
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

            var subset = from s in dataHandleTable where s.Port.Equals(e.ListenEP.Port) select s;
            foreach (var item in subset) {
                item.ListenState = "未启动";
            }

            sckListener.CloseClientByListener(e.ListenEP);

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

            lstClient.Items.Add(item);
        }

        private void sckListener_ClientDisconn(object sender, ClientEventArgs e)
        {
            if (this.InvokeRequired) {
                this.Invoke(new EventHandler<ClientEventArgs>(sckListener_ClientDisconn),
                    new object[] { sender, e });
                return;
            }

            for (int i = 0; i < lstClient.Items.Count; i++) {
                if (lstClient.Items[i].SubItems[0].Text.Equals(e.RemoteEP.ToString())) {
                    lstClient.Items.Remove(lstClient.Items[i]);
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


            /// @@ 没有办法的办法，必须删改
            string[] str = e.Data.Split("|".ToArray());
            foreach (var item in str) {
            if (item.StartsWith("CCID=")) {
                    // 更新CCID
                    for (int i = 0; i < lstClient.Items.Count; i++) {
                        if (lstClient.Items[i].SubItems[0].Text.Equals(e.RemoteEP.ToString())) {
                            lstClient.Items[i].SubItems[3].Text = item.Substring(5);
                            break;
                        }
                    }

                    // 基站不会自动断开前一次连接...相同CCID的连上来后，断开前面的连接
                    for (int i = 0; i < lstClient.Items.Count; i++) {
                        if (lstClient.Items[i].SubItems[3].Text.Equals(item.Substring(5))
                            && !lstClient.Items[i].SubItems[0].Text.Equals(e.RemoteEP.ToString())) {
                            string[] s = lstClient.Items[i].SubItems[0].Text.Split(":".ToArray());
                            sckListener.CloseClient(new IPEndPoint(IPAddress.Parse(s[0]), Convert.ToInt32(s[1])));
                            break;
                        }
                    }
                }
            }
            /// @@ 没有办法的办法，必须删改


            /// 调用数据处理插件 ======================================================
            try {
                var subset = from s in dataHandleTable where s.Port == e.LocalEP.Port select s;
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
            if (this.InvokeRequired) {
                this.Invoke(new EventHandler<ClientEventArgs>(sckListener_ClientSendMsg),
                    new object[] { sender, e });
                return;
            }

            string logFormat = e.RemoteEP.ToString() + " " + DateTime.Now.ToString() + "发送数据：" + e.Data;

            txtMsg.AppendText(logFormat + "\r\n");
            LogRecord.WriteInfoLog(logFormat);
        }

        private void dataHandleTable_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (btnStartListen.Enabled == true)
                return;

            DataHandleState ss = sender as DataHandleState;

            List<IPEndPoint> ep = new List<IPEndPoint>();
            IPAddress[] ipAddr = Dns.GetHostAddresses(Dns.GetHostName());
            foreach (IPAddress ip in ipAddr) {
                if (ip.AddressFamily.Equals(AddressFamily.InterNetwork)) {
                    ep.Add(new IPEndPoint(ip, ss.Port));
                    break;
                }
            }

            try {
                if (ss.ListenState == null ||
                    ss.IsPermitListen && ss.ListenState.Equals("已启动") ||
                    !ss.IsPermitListen && ss.ListenState.Equals("未启动"))
                    return;

                if (ss.IsPermitListen)
                    sckListener.Start(ep);
                else
                    sckListener.Stop(ep);
            }
            catch (ApplicationException ex) {
                LogRecord.writeLog(ex);
            }
            catch (Exception ex) {
                LogRecord.writeLog(ex);
            }
        }

        // Events for itself ==================================================================

        private void MainForm_Load(object sender, EventArgs e)
        {
            initailizeWindowName();
            initailizeAsyncSocketListener();
            initailizePluginManager();
            initailizeDataHandleState();

            // Initialize list of client
            lstClient.Columns.Add("基站IP", 155, HorizontalAlignment.Center);
            lstClient.Columns.Add("监听端口", 80, HorizontalAlignment.Center);
            lstClient.Columns.Add("连接时间", 159, HorizontalAlignment.Center);
            lstClient.Columns.Add("CCID", 180, HorizontalAlignment.Center);

            btnStopListen.Enabled = false;
        }

        private void MainFrom_FormClosing(object sender, FormClosingEventArgs e)
        {
            /*
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
             * */
        }

        private void btnStartListen_Click(object sender, EventArgs e)
        {
            List<IPEndPoint> ep = new List<IPEndPoint>();
            IPAddress[] ipAddr = Dns.GetHostAddresses(Dns.GetHostName());
            foreach (IPAddress ip in ipAddr) {
                if (ip.AddressFamily.Equals(AddressFamily.InterNetwork)) {
                    foreach (DataHandleState item in dataHandleTable) {
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

            lstClient.Items.Clear();

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

        private void dgvStation_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right) {
                if (e.RowIndex >= 0) {
                    //若行已是选中状态就不再进行设置
                    if (dgvStation.Rows[e.RowIndex].Selected == false) {
                        dgvStation.ClearSelection();
                        dgvStation.Rows[e.RowIndex].Selected = true;
                    }
                    //只选中一行时设置活动单元格
                    if (dgvStation.SelectedRows.Count == 1) {
                        dgvStation.CurrentCell = dgvStation.Rows[e.RowIndex].Cells[e.ColumnIndex];
                    }
                    //弹出操作菜单
                    contextMenuStrip2.Show(MousePosition.X, MousePosition.Y);
                }
            }
        }

        private void 发送命令ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string sendmsg = Microsoft.VisualBasic.Interaction.InputBox("请输入要发送的命令", "手动命令发送框", "!A1?", 200, 200);
            if (sendmsg == "") {
                return;
            }

            for (int i = 0; i < lstClient.SelectedItems.Count; i++) {
                string[] s = lstClient.SelectedItems[i].SubItems[0].Text.Split(":".ToArray());

                sckListener.Send(new IPEndPoint(IPAddress.Parse(s[0]), Convert.ToInt32(s[1])),
                    sendmsg);
            }
        }

        private void 关闭连接ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < lstClient.SelectedItems.Count; i++) {
                string[] s = lstClient.SelectedItems[i].SubItems[0].Text.Split(":".ToArray());

                sckListener.CloseClient(new IPEndPoint(IPAddress.Parse(s[0]), Convert.ToInt32(s[1])));
            }
        }

        private void 载入模块ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.openFileDialog1.Filter = "dll files (*.dll)|*.dll|All files (*.*)|*.*";
            this.openFileDialog1.FileName = "";

            string assemblyName = null;
            int port = 0;

            if (this.openFileDialog1.ShowDialog() == DialogResult.OK) {
                try {
                    assemblyName = pluginManager.LoadPlugin(this.openFileDialog1.FileName);
                }
                catch (ApplicationException ex) {
                    MessageBox.Show(ex.Message, "Error");
                    return;
                }

                try  {	        
		            port = (int)pluginManager.Invoke(assemblyName, "IDataHandle", "GetIdentity", null);
	            }
	            catch (Exception ex) {
		            pluginManager.UnLoadPlugin(assemblyName);
                    MessageBox.Show(ex.Message, "Error");
                    return;
	            }

                // 加载模块已经成功
                FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(this.openFileDialog1.FileName);
                dataHandleTable.Add(new DataHandleState
                {
                    Port = port,
                    IsPermitListen = false,
                    ListenState = "未启动",
                    ChineseName = fvi.ProductName,
                    FileName = assemblyName
                });
            }
        }

        private void 卸载模块ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < dgvStation.SelectedRows.Count; i++) {
                int port = Convert.ToInt32(dgvStation.SelectedRows[i].Cells[0].Value.ToString());
                pluginManager.UnLoadPlugin(dgvStation.SelectedRows[i].Cells[4].Value.ToString());

                List<IPEndPoint> ep = new List<IPEndPoint>();
                IPAddress[] ipAddr = Dns.GetHostAddresses(Dns.GetHostName());
                foreach (IPAddress ip in ipAddr) {
                    if (ip.AddressFamily.Equals(AddressFamily.InterNetwork)) {
                        ep.Add(new IPEndPoint(ip, port));
                        break;
                    }
                }
                if (dgvStation.SelectedRows[i].Cells[2].Value.ToString().Equals("已启动"))
                    sckListener.Stop(ep);

                dgvStation.Rows.Remove(dgvStation.SelectedRows[i]);
            }
        }

    }
}
