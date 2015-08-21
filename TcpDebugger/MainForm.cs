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
using System.Security.Cryptography;
using System.Xml;
using Mnn.MnnSocket;

namespace TcpDebugger
{
    public partial class MainForm : Form
    {
        private Encoding coding;
        private IPAddress ipaddress;
        private int port;
        private AsyncSocketSender client;

        public MainForm()
        {
            InitializeComponent();

            XmlDocument xdoc = new XmlDocument();
            xdoc.Load(System.AppDomain.CurrentDomain.BaseDirectory + "\\config.xml");

            XmlNode node = xdoc.SelectSingleNode("/configuration/encoding");
            coding = Encoding.GetEncoding(node.InnerText);

            node = xdoc.SelectSingleNode("/configuration/ipaddress");
            ipaddress = IPAddress.Parse(node.InnerText);

            node = xdoc.SelectSingleNode("/configuration/port");
            port = int.Parse(node.InnerText);
            
            client = new AsyncSocketSender();
            client.messageReceiver += new AsyncSocketSender.MessageReceiverDelegate(socketHandle);

            txtIP.Text = ipaddress.ToString();
            txtPort.Text = port.ToString();
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            IPAddress ip = IPAddress.Parse(txtIP.Text.Trim());
            IPEndPoint ep = new IPEndPoint(ip, int.Parse(txtPort.Text.Trim()));

            client.Connect(ep);
            btnConnect.Enabled = false;
        }

        private void btnDisconn_Click(object sender, EventArgs e)
        {
            client.Close();
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            if (txtSend.Text.Length != 0) {
                //MD5 md5 = MD5.Create();
                //byte[] hashdata = md5.ComputeHash(coding.GetBytes(txtSend.Text));
                //byte[] data = hashdata.Concat(coding.GetBytes(txtSend.Text)).ToArray();
                //client.Send(data);

                client.Send(coding.GetBytes(txtSend.Text));
            }
        }

        private void socketHandle(byte[] data, AsyncSocketSender.AsyncState state)
        {
            if (this.InvokeRequired) {
                this.Invoke(new AsyncSocketSender.MessageReceiverDelegate(socketHandle), new object[] { data, state });
                return;
            }

            switch (state)
            {
                case AsyncSocketSender.AsyncState.ConnectSuccess:
                    txtMessage.AppendText("Connection Successful.\n");
                    txtIP.Enabled = false;
                    txtPort.Enabled = false;
                    btnConnect.Enabled = false;
                    btnDisconn.Enabled = true;
                    btnSend.Enabled = true;
                    break;
                case AsyncSocketSender.AsyncState.ConnectFail:
                case AsyncSocketSender.AsyncState.Disconncted:
                    txtMessage.AppendText("Connection Failed.\n");
                    txtIP.Enabled = true;
                    txtPort.Enabled = true;
                    btnConnect.Enabled = true;
                    btnDisconn.Enabled = false;
                    btnSend.Enabled = false;
                    break;
                case AsyncSocketSender.AsyncState.SendSuccess:
                    txtMessage.AppendText("Send Successful.\n");
                    break;
                case AsyncSocketSender.AsyncState.SendFail:
                    txtMessage.AppendText("Send Failed.\n");
                    break;
                case AsyncSocketSender.AsyncState.ReadMessage:
                    txtMessage.AppendText("(" + txtIP.Text + " " + DateTime.Now.ToString() + ")\r\n" + coding.GetString(data) + "\n");
                    break;
            }
        }

        private void txtIP_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((int)e.KeyChar != 8 &&
                (int)e.KeyChar != 46 &&
                !char.IsDigit(e.KeyChar))
                e.Handled = true;
        }

        private void txtPort_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((int)e.KeyChar != 8 && !char.IsDigit(e.KeyChar))
                e.Handled = true;
        }
    }
}
