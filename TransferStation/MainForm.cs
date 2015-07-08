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
            sckListener.clientConnect += new AsyncSocketListener.ClientConnectEventHandler(sckListener_clientConnect);
            sckListener.clientDisconn += new AsyncSocketListener.ClientDisconnEventHandler(sckListener_clientDisconn);
            sckListener.clientMessage += new AsyncSocketListener.ClientMessageEventHandler(sckListener_clientMessage);

            InitializeComponent();
        }

        private void sckListener_clientConnect(object sender, AsyncSocketListener.ClientEventArgs e)
        {
            if (this.InvokeRequired) {
                this.Invoke(new AsyncSocketListener.ClientConnectEventHandler(sckListener_clientConnect),
                    new object[] { sender, e });
                return;
            }

            //AsyncSocketListener.ClientEventArgs args = e as AsyncSocketListener.ClientEventArgs;

            ListViewItem item = new ListViewItem();
            item.SubItems[0].Text = e.ipEndPoint.ToString();
            item.SubItems.Add(DateTime.Now.ToString());
            item.SubItems.Add(Guid.NewGuid().ToString("N"));
            item.SubItems.Add("-");

            lstClient.Items.Add(item);
        }

        private void sckListener_clientDisconn(object sender, AsyncSocketListener.ClientEventArgs e)
        {
            if (this.InvokeRequired) {
                this.Invoke(new AsyncSocketListener.ClientDisconnEventHandler(sckListener_clientDisconn),
                    new object[] { sender, e });
                return;
            }

            for (int i = 0; i < lstClient.Items.Count; i++) {
                if (lstClient.Items[i].SubItems[0].Text == e.ipEndPoint.ToString()) {
                    lstClient.Items.Remove(lstClient.Items[i]);
                }
            }
        }

        private void sckListener_clientMessage(object sender, AsyncSocketListener.ClientEventArgs e)
        {
            if (this.InvokeRequired) {
                this.Invoke(new AsyncSocketListener.ClientMessageEventHandler(sckListener_clientMessage),
                    new object[] { sender, e });
                return;
            }

            txtMsg.AppendText("(" + e.ipEndPoint.ToString() + " " + DateTime.Now.ToString() + ")：");
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
            // Start socket listener
            sckListener.Start(Convert.ToInt32(txtPort.Text));

            btnStartListen.Enabled = false;
            btnStopListen.Enabled = true;
        }

        private void btnStopListen_Click(object sender, EventArgs e)
        {
            sckListener.Stop();

            lstClient.Items.Clear();

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
    }
}
