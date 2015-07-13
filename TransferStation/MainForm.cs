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
using MnnSocket;
using TransferStationUtils;

namespace TransferStation
{
    public partial class MainForm : Form
    {
        AsyncSocketListener sckListener = null;

        public MainForm()
        {

            // Create socket listener & Start user interface model
            sckListener = new AsyncSocketListener();

            // Start data processing model
            DataProcess.DataConvertCenter center = new DataProcess.DataConvertCenter(sckListener);

            sckListener.clientConnect += sckListener_clientConnect;
            sckListener.clientDisconn += sckListener_clientDisconn;
            sckListener.clientReadMsg += sckListener_clientReadMsg;
            sckListener.clientSendMsg += sckListener_clientSendMsg;

            InitializeComponent();
        }

        private void sckListener_clientConnect(object sender, ClientEventArgs e)
        {
            if (this.InvokeRequired) {
                this.Invoke(new EventHandler<ClientEventArgs>(sckListener_clientConnect),
                    new object[] { sender, e });
                return;
            }

            ListViewItem item = new ListViewItem();
            item.SubItems[0].Text = e.remoteEP.ToString();
            item.SubItems.Add(DateTime.Now.ToString());
            item.SubItems.Add(Guid.NewGuid().ToString("N"));
            item.SubItems.Add("-");

            lstClient.Items.Add(item);
        }

        private void sckListener_clientDisconn(object sender, ClientEventArgs e)
        {
            if (this.InvokeRequired) {
                this.Invoke(new EventHandler<ClientEventArgs>(sckListener_clientDisconn),
                    new object[] { sender, e });
                return;
            }

            for (int i = 0; i < lstClient.Items.Count; i++) {
                if (lstClient.Items[i].SubItems[0].Text == e.remoteEP.ToString()) {
                    lstClient.Items.Remove(lstClient.Items[i]);
                }
            }
        }

        private void sckListener_clientReadMsg(object sender, ClientEventArgs e)
        {
            if (this.InvokeRequired) {
                this.Invoke(new EventHandler<ClientEventArgs>(sckListener_clientReadMsg),
                    new object[] { sender, e });
                return;
            }

            if (txtMsg.Text.Length >= 5 * 1024)
                txtMsg.Clear();

            string logFormat = e.remoteEP.ToString() + " " + DateTime.Now.ToString() + "接收数据：" + e.data;

            txtMsg.AppendText(logFormat + "\r\n");
            LogRecord.WriteInfoLog(logFormat);
        }

        private void sckListener_clientSendMsg(object sender, ClientEventArgs e)
        {
            if (this.InvokeRequired) {
                this.Invoke(new EventHandler<ClientEventArgs>(sckListener_clientSendMsg),
                    new object[] { sender, e });
                return;
            }

            string logFormat = e.remoteEP.ToString() + " " + DateTime.Now.ToString() + "发送数据：" + e.data;

            txtMsg.AppendText(logFormat + "\r\n");
            LogRecord.WriteInfoLog(logFormat);
        }

        private void MainFrom_Load(object sender, EventArgs e)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(asm.Location);

            this.Text = string.Format("{0} {1}.{2} - Powered By {3}",
                fvi.ProductName, fvi.ProductMajorPart, fvi.ProductMinorPart, fvi.CompanyName);     

            lstClient.Columns.Add("IP", 155, HorizontalAlignment.Center);
            lstClient.Columns.Add("连接时间", 159, HorizontalAlignment.Center);
            lstClient.Columns.Add("全局标识", 276, HorizontalAlignment.Center);
            lstClient.Columns.Add("CCID", 235, HorizontalAlignment.Center);
            IPAddress[] ipAddr = Dns.GetHostAddresses(Dns.GetHostName());

            foreach (IPAddress ip in ipAddr) {
                if (ip.AddressFamily.Equals(AddressFamily.InterNetwork)) {
                    txtIP.Text = ip.ToString();
                }
            }

            txtPort.Text = "3006";

            txtIP.Enabled = false;
            btnStopListen.Enabled = false;
        }

        private void MainFrom_FormClosing(object sender, FormClosingEventArgs e)
        {
            string psward = Microsoft.VisualBasic.Interaction.InputBox("请输入密码", "密码框", "", 200, 200);
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
            List<IPEndPoint> ep = null;
            IPAddress[] ipAddr = Dns.GetHostAddresses(Dns.GetHostName());
            foreach (IPAddress ip in ipAddr) {
                if (ip.AddressFamily.Equals(AddressFamily.InterNetwork)) {
                    ep = new List<IPEndPoint>() {new IPEndPoint(ip, Convert.ToInt32(txtPort.Text))};
                    //ep = new List<IPEndPoint>() {new IPEndPoint(ip, Convert.ToInt32(txtPort.Text)),
                    //    new IPEndPoint(ip, 5963), new IPEndPoint(ip, 5962)};
                    break;
                }
            }

            try {
                // Start socket listener
                sckListener.Start(ep);

                txtPort.Enabled = false;
                btnStartListen.Enabled = false;
                btnStopListen.Enabled = true;
            }
            catch (Exception ex) {
                MessageBox.Show(ex.Message, "启动监听失败");
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

            txtPort.Enabled = true;
            btnStartListen.Enabled = true;
            btnStopListen.Enabled = false;
        }

        private void lstClient_MouseClick(object sender, MouseEventArgs e)
        {
            MouseButtons mb = e.Button;

            //判断是否为右击事件
            if (MouseButtons.Right.Equals(mb)) {
                if (lstClient.SelectedItems.Count != 1) {
                    MessageBox.Show("请选择一个基站");
                    return;
                }
                string test = Microsoft.VisualBasic.Interaction.InputBox("请输入要发送的16进制命令", "手动命令发送框", "", 200, 200);
                if (test == "") {
                    return;
                }

                string addr = lstClient.SelectedItems[0].SubItems[0].Text;
                IPEndPoint ep = new IPEndPoint(System.Net.IPAddress.Parse(addr.Split(':')[0]),
                    int.Parse(addr.Split(':')[1]));
                sckListener.Send(ep, test);
                
                //SocketClient con = socketClientlst.Where(x => x.Identify == listView1.SelectedItems[0].SubItems[2].Text).FirstOrDefault();
                //if (con != null) {
                //    con.Send16(test);
                //}
            }
        }

        private void lstClient_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            string sendmsg = Microsoft.VisualBasic.Interaction.InputBox("请输入要发送的命令", "手动命令发送框", "!A1?", 200, 200);
            if (sendmsg == "") {
                return;
            }

            string addr = lstClient.SelectedItems[0].SubItems[0].Text;
            IPEndPoint ep = new IPEndPoint(System.Net.IPAddress.Parse(addr.Split(':')[0]),
                int.Parse(addr.Split(':')[1]));
            sckListener.Send(ep, sendmsg);

            //try {
                //SocketClient conn = socketClientlst.Where(x => x.Identify == listView1.SelectedItems[0].SubItems[2].Text).FirstOrDefault();
                //if (conn != null) {
                //    conn.Send(sendmsg);
                //}
            //}
            //catch (Exception ex) {
                //Program.writeLog(ex);
            //}
        }

        private void txtIP_KeyPress(object sender, KeyPressEventArgs e)
        {
            // 特殊键(含空格), 不处理 & 非数字键, 放弃该输入 
            // 32及以下属于特殊字符
            if ((int)e.KeyChar != 8 &&
                (int)e.KeyChar != 46 &&
                !char.IsDigit(e.KeyChar))
                e.Handled = true;
        }

        private void txtPort_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((int)e.KeyChar != 8 & !char.IsDigit(e.KeyChar))    
                e.Handled = true;
        }

    }
}
