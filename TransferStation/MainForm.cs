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
using MnnSocket;

namespace TransferStation
{
    public partial class MainFrom : Form
    {
        AsyncSocketListener sckListener = null;

        public MainFrom(AsyncSocketListener sl)
        {
            sckListener = sl;
            sckListener.clientConnect += sckListener_clientConnect;
            sckListener.clientDisconn += sckListener_clientDisconn;
            sckListener.clientMessage += sckListener_clientMessage;
            //sckListener.clientConnect += new ClientConnectEventHandler(sckListener_clientConnect);
            //sckListener.clientDisconn += new ClientDisconnEventHandler(sckListener_clientDisconn);
            //sckListener.clientMessage += new ClientMessageEventHandler(sckListener_clientMessage);

            InitializeComponent();
        }

        private void sckListener_clientConnect(object sender, ClientEventArgs e)
        {
            if (this.InvokeRequired) {
                this.Invoke(new EventHandler<ClientEventArgs>(sckListener_clientConnect),
                    new object[] { sender, e });
                return;
            }

            //AsyncSocketListener.ClientEventArgs args = e as AsyncSocketListener.ClientEventArgs;

            ListViewItem item = new ListViewItem();
            item.SubItems[0].Text = e.clientEP.ToString();
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
                if (lstClient.Items[i].SubItems[0].Text == e.clientEP.ToString()) {
                    lstClient.Items.Remove(lstClient.Items[i]);
                }
            }
        }

        private void sckListener_clientMessage(object sender, ClientEventArgs e)
        {
            if (this.InvokeRequired) {
                this.Invoke(new EventHandler<ClientEventArgs>(sckListener_clientMessage),
                    new object[] { sender, e });
                return;
            }

            txtMsg.AppendText("(" + e.clientEP.ToString() + " " + DateTime.Now.ToString() + ")：");
            txtMsg.AppendText(e.data + "\r\n");
        }

        private void MainFrom_Load(object sender, EventArgs e)
        {
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

            txtPort.Text = "5964";

            txtIP.Enabled = false;
            btnStopListen.Enabled = false;
        }

        private void btnStartListen_Click(object sender, EventArgs e)
        {
            List<IPEndPoint> ep = null;
            IPAddress[] ipAddr = Dns.GetHostAddresses(Dns.GetHostName());
            foreach (IPAddress ip in ipAddr) {
                if (ip.AddressFamily.Equals(AddressFamily.InterNetwork)) {
                    //ep = new List<IPEndPoint>() {new IPEndPoint(ip, Convert.ToInt32(txtPort.Text))};
                    ep = new List<IPEndPoint>() {new IPEndPoint(ip, Convert.ToInt32(txtPort.Text)),
                        new IPEndPoint(ip, 5963), new IPEndPoint(ip, 5962)};
                    break;
                }
            }

            // Start socket listener
            sckListener.Start(ep);

            txtPort.Enabled = false;
            btnStartListen.Enabled = false;
            btnStopListen.Enabled = true;
        }

        private void btnStopListen_Click(object sender, EventArgs e)
        {
            List<IPEndPoint> ep = null;
            IPAddress[] ipAddr = Dns.GetHostAddresses(Dns.GetHostName());
            foreach (IPAddress ip in ipAddr) {
                if (ip.AddressFamily.Equals(AddressFamily.InterNetwork)) {
                    ep = new List<IPEndPoint>() { new IPEndPoint(ip, Convert.ToInt32(txtPort.Text)) };
                    break;
                }
            }

            //sckListener.Stop(ep);
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
