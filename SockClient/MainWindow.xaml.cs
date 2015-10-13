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
using System.Collections.ObjectModel;
using System.Net;
using System.Threading;
using Mnn.MnnSocket;

namespace SockClient
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            cmd_table = new ObservableCollection<CmdUnit>();
            cmd_table.Add(new CmdUnit() { ID = "1", CMD = "30 20 05 00 40" });
            cmd_table.Add(new CmdUnit() { ID = "2", CMD = "20 40 04 00" });
            lstViewCommand.ItemsSource = cmd_table;

            sessmgr = new SockSessManager();
            sessmgr.sess_parse += new SockSessManager.SessParseDelegate(sessmgr_sess_parse);
            sessmgr.AddConnectSession(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2000));

            Thread thread = new Thread(() =>
            {
                while (true) {
                    sessmgr.Perform(1000);
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        ObservableCollection<CmdUnit> cmd_table;
        SockSessManager sessmgr;

        private void sessmgr_sess_parse(object sender, SockSess sess)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (txtMsg.Text.Length >= 20 * 1024) {
                    txtMsg.Clear();
                }

                txtMsg.AppendText(sess.rdata.Take(sess.rdata_size).ToArray() + "\r\n\r\n");
                txtMsg.ScrollToEnd();

                sess.rdata_size = 0;
            }));
        }

        private byte[] CmdParse(string cmd)
        {
            byte[] retval = cmd.Split(' ').Select(
                t => Convert.ToInt32(t) / 10 * 16 + Convert.ToInt32(t) % 10
                ).Select(t => Convert.ToByte(t)).ToArray();

            return retval;
        }

        private void MenuItem_SendCommand_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in lstViewCommand.SelectedItems) {
                CmdUnit cmd = item as CmdUnit;
                if (cmd == null)
                    continue;

                sessmgr.sess_table[0].sock.Send(CmdParse(cmd.CMD));
            }
        }

        private void MenuItem_EditCommand_Click(object sender, RoutedEventArgs e)
        {
            if (lstViewCommand.SelectedItems.Count == 0)
                return;

            InputDialog input = new InputDialog();
            input.Owner = this;
            input.Title = "编辑命令";
            input.textBlock1.Text = "序号：";
            input.textBlock2.Text = "命令：";
            input.textBox1.Text = (lstViewCommand.SelectedItems[0] as CmdUnit).ID;
            input.textBox2.Text = (lstViewCommand.SelectedItems[0] as CmdUnit).CMD;
            input.textBox1.Focus();

            if (input.ShowDialog() == false)
                return;

            foreach (var item in lstViewCommand.SelectedItems) {
                CmdUnit cmd = item as CmdUnit;
                if (cmd == null)
                    continue;

                cmd.ID = input.textBox1.Text;
                cmd.CMD = input.textBox2.Text;
            }
        }

        private void MenuItem_AddCommand_Click(object sender, RoutedEventArgs e)
        {
            InputDialog input = new InputDialog();
            input.Owner = this;
            input.Title = "新增命令";
            input.textBlock1.Text = "序号：";
            input.textBlock2.Text = "命令：";
            input.textBox1.Focus();

            if (input.ShowDialog() == false)
                return;

            CmdUnit cmd = new CmdUnit();
            cmd.ID = input.textBox1.Text;
            cmd.CMD = input.textBox2.Text;
            cmd_table.Add(cmd);
        }

        private void MenuItem_RemoveCommand_Click(object sender, RoutedEventArgs e)
        {
            List<CmdUnit> tmp = new List<CmdUnit>();

            foreach (var item in lstViewCommand.SelectedItems) {
                CmdUnit cmd = item as CmdUnit;
                if (cmd == null)
                    continue;

                tmp.Add(cmd);
            }

            foreach (var item in tmp)
                cmd_table.Remove(item);
        }
    }
}
