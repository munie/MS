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
using System.IO;
using System.Xml;
using Mnn.MnnSocket;
using Mnn.MnnUtil;

namespace SockConn
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // init
            init();

            // config after init
            config();

            // autorun connects
            foreach (var item in cnn_table) {
                if (item.Autorun == true)
                    sessmgr.AddConnectSession(new IPEndPoint(IPAddress.Parse(item.IP), int.Parse(item.Port)));
            }
        }

        ObservableCollection<CmdUnit> cmd_table;
        ObservableCollection<CnnUnit> cnn_table;
        SockSessManager sessmgr;

        private void init()
        {
            // init cmd_table
            cmd_table = new ObservableCollection<CmdUnit>();
            lstViewCommand.ItemsSource = cmd_table;

            // init cnn_table
            cnn_table = new ObservableCollection<CnnUnit>();
            lstViewConnect.ItemsSource = cnn_table;

            // init socksessmgr
            sessmgr = new SockSessManager();
            sessmgr.sess_parse += new SockSessManager.SessParseDelegate(sessmgr_sess_parse);
            sessmgr.sess_create += new SockSessManager.SessCreateDelegate(sessmgr_sess_create);
            sessmgr.sess_delete += new SockSessManager.SessDeleteDelegate(sessmgr_sess_delete);
            Thread thread = new Thread(() =>
            {
                while (true) {
                    sessmgr.Perform(1000);
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        private void config()
        {
            if (File.Exists(System.AppDomain.CurrentDomain.BaseDirectory + @"\sockclient.xml") == false) {
                System.Windows.MessageBox.Show("未找到配置文件： sockclient.xml");
                return;
            }

            /// ** Initialize Start ====================================================
            try {
                XmlDocument xdoc = new XmlDocument();
                xdoc.Load(System.AppDomain.CurrentDomain.BaseDirectory + @"\sockclient.xml");

                // cmd config
                foreach (XmlNode item in xdoc.SelectNodes("/configuration/cmdconfig/cmd")) {
                    CmdUnit cmd = new CmdUnit();
                    cmd.ID = item.Attributes["id"].Value;
                    cmd.Name = item.Attributes["name"].Value;
                    cmd.CNNS = item.Attributes["cnns"].Value;
                    cmd.CMD = item.Attributes["content"].Value;
                    cmd.Comment = item.Attributes["comment"].Value;
                    cmd_table.Add(cmd);
                }

                // cnn config
                foreach (XmlNode item in xdoc.SelectNodes("/configuration/cnnconfig/cnn")) {
                    CnnUnit cnn = new CnnUnit();
                    cnn.ID = item.Attributes["id"].Value;
                    cnn.IP = item.Attributes["ip"].Value;
                    cnn.Port = item.Attributes["port"].Value;
                    cnn.State = CnnUnit.StateDisconned;
                    cnn.Autorun = bool.Parse(item.Attributes["autorun"].Value);
                    cnn_table.Add(cnn);
                }
            }
            catch (Exception ex) {
                Mnn.MnnUtil.Logger.WriteException(ex);
                System.Windows.MessageBox.Show("配置文件读取错误： sockclient.xml");
            }
            /// ** Initialize End ====================================================
        }

        private void sessmgr_sess_parse(object sender, SockSess sess)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (txtMsg.Text.Length >= 20 * 1024) {
                    txtMsg.Clear();
                }

                string hexstr = "";
                foreach (var item in sess.rdata.Take(sess.rdata_size).ToArray()) {
                    string s = Convert.ToString(item, 16);
                    if (s.Length == 1)
                        s = "0" + s;
                    if (hexstr.Length != 0)
                        hexstr += " " + s;
                    else
                        hexstr += s;
                }

                txtMsg.AppendText(hexstr + "\n");
                txtMsg.ScrollToEnd();

                sess.rdata_size = 0;
            }));
        }

        private void sessmgr_sess_create(object sender, SockSess sess)
        {
            var subset = from s in cnn_table
                         where s.IP.Equals(sess.ep.Address.ToString()) && int.Parse(s.Port) == sess.ep.Port
                         select s;
            foreach (var item in subset)
                item.State = CnnUnit.StateConnected;
        }

        private void sessmgr_sess_delete(object sender, SockSess sess)
        {
            var subset = from s in cnn_table
                         where s.IP.Equals(sess.ep.Address.ToString()) && int.Parse(s.Port) == sess.ep.Port
                         select s;
            foreach (var item in subset)
                item.State = CnnUnit.StateDisconned;
        }

        private void MenuItem_Connect_Click(object sender, RoutedEventArgs e)
        {
            foreach (CnnUnit item in lstViewConnect.SelectedItems) {
                if (item.State == CnnUnit.StateConnected)
                    continue;

                sessmgr.AddConnectSession(new IPEndPoint(IPAddress.Parse(item.IP), int.Parse(item.Port)));
            }
        }

        private void MenuItem_Disconn_Click(object sender, RoutedEventArgs e)
        {
            foreach (CnnUnit item in lstViewConnect.SelectedItems) {
                if (item.State == CnnUnit.StateDisconned)
                    continue;

                IPEndPoint ep = new IPEndPoint(IPAddress.Parse(item.IP), int.Parse(item.Port));
                var subset = from s in sessmgr.sess_table where s.sock.RemoteEndPoint.Equals(ep) select s;
                foreach (var i in subset)
                    i.eof = true;
            }
        }

        private void MenuItem_SendCommand_Click(object sender, RoutedEventArgs e)
        {
            foreach (CmdUnit item in lstViewCommand.SelectedItems) {
                // 从 cnn_table 中找到符合CNNS的连接
                string[] str = item.CNNS.Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                var subset = from s in cnn_table
                             where s.State == CnnUnit.StateConnected && str.Contains(s.ID)
                             select s;
                // 根据连接找到对应的socket，发送命令
                foreach (CnnUnit i in subset) {
                    IPEndPoint ep = new IPEndPoint(IPAddress.Parse(i.IP), int.Parse(i.Port));
                    var set = from s in sessmgr.sess_table where s.sock.RemoteEndPoint.Equals(ep) select s;
                    if (set.Count() != 0)
                        set.First().sock.Send(ConvertUtil.CmdstrToBytes(item.CMD));
                }
            }
        }

        private void MenuItem_SelectSendCommand_Click(object sender, RoutedEventArgs e)
        {
            // 没有连接
            if (sessmgr.sess_table.Count == 0)
                return;

            // 只有一个连接
            if (sessmgr.sess_table.Count == 1) {
                foreach (CmdUnit item in lstViewCommand.SelectedItems)
                    sessmgr.sess_table[0].sock.Send(ConvertUtil.CmdstrToBytes(item.CMD));
                return;
            }

            // 多个连接
            using (SelectDialog select = new SelectDialog()) {
                // 新建对话框进行选择
                select.Owner = this;
                select.Title = "选择发送连接";
                select.lstViewConnect.ItemsSource = cnn_table;
                select.lstViewConnect.SelectedItems.Add(select.lstViewConnect.Items[0]);
                if (select.ShowDialog() == false)
                    return;

                // 对话框返回，根据选择的信息找到对应的连接，发送命令
                foreach (CnnUnit item in select.lstViewConnect.SelectedItems) {
                    // 根据选择的信息找到对应的连接
                    IPEndPoint ep = new IPEndPoint(IPAddress.Parse(item.IP), int.Parse(item.Port));
                    var subset = from s in sessmgr.sess_table where s.sock.RemoteEndPoint.Equals(ep) select s;
                    if (subset.Count() == 0)
                        continue;

                    // 发送命令
                    foreach (CmdUnit i in lstViewCommand.SelectedItems)
                        subset.First().sock.Send(ConvertUtil.CmdstrToBytes(i.CMD));
                }
            }
        }

        private void MenuItem_EditCommand_Click(object sender, RoutedEventArgs e)
        {
            if (lstViewCommand.SelectedItems.Count == 0)
                return;

            using (InputDialog input = new InputDialog()) {
                input.Owner = this;
                input.Title = "编辑命令";
                input.textBlockID.Text = "ID：";
                input.textBlockName.Text = "Name：";
                input.textBlockCNNS.Text = "CNNS：";
                input.textBlockCMD.Text = "命令：";
                input.textBlockComment.Text = "说明：";
                input.textBoxID.Text = (lstViewCommand.SelectedItems[0] as CmdUnit).ID;
                input.textBoxName.Text = (lstViewCommand.SelectedItems[0] as CmdUnit).Name;
                input.textBoxCNNS.Text = (lstViewCommand.SelectedItems[0] as CmdUnit).CNNS;
                input.textBoxCMD.Text = (lstViewCommand.SelectedItems[0] as CmdUnit).CMD;
                input.textBoxComment.Text = (lstViewCommand.SelectedItems[0] as CmdUnit).Comment;
                input.textBoxCMD.Focus();
                input.textBoxCMD.SelectionStart = input.textBoxCMD.Text.Length;

                if (input.ShowDialog() == false)
                    return;

                foreach (CmdUnit item in lstViewCommand.SelectedItems) {
                    item.ID = input.textBoxID.Text;
                    item.Name = input.textBoxName.Text;
                    item.CNNS = input.textBoxCNNS.Text;
                    item.CMD = input.textBoxCMD.Text;
                    item.Comment = input.textBoxComment.Text;
                }
            }
        }

        private void MenuItem_AddCommand_Click(object sender, RoutedEventArgs e)
        {
            using (InputDialog input = new InputDialog()) {
                input.Owner = this;
                input.Title = "新增命令";
                input.textBlockID.Text = "ID：";
                input.textBlockName.Text = "Name：";
                input.textBlockCNNS.Text = "CNNS：";
                input.textBlockCMD.Text = "命令：";
                input.textBlockComment.Text = "说明：";
                input.textBoxID.Focus();

                if (input.ShowDialog() == false)
                    return;

                CmdUnit cmd = new CmdUnit();
                cmd.ID = input.textBoxID.Text;
                cmd.Name = input.textBoxName.Text;
                cmd.CNNS = input.textBoxCNNS.Text;
                cmd.CMD = input.textBoxCMD.Text;
                cmd.Comment = input.textBoxComment.Text;
                cmd_table.Add(cmd);
            }
        }

        private void MenuItem_RemoveCommand_Click(object sender, RoutedEventArgs e)
        {
            List<CmdUnit> tmp = new List<CmdUnit>();

            foreach (CmdUnit item in lstViewCommand.SelectedItems)
                tmp.Add(item);

            foreach (var item in tmp)
                cmd_table.Remove(item);
        }

        private void MenuItem_SaveCommand_Click(object sender, RoutedEventArgs e)
        {
            XmlDocument doc = new XmlDocument();
            XmlNode cmdconfig;

            if (File.Exists(System.AppDomain.CurrentDomain.BaseDirectory + @"\sockclient.xml")) {
                doc.Load(System.AppDomain.CurrentDomain.BaseDirectory + @"\sockclient.xml");
                cmdconfig = doc.SelectSingleNode("/configuration/cmdconfig");
            }
            else {
                doc.AppendChild(doc.CreateXmlDeclaration("1.0", "utf-8", ""));  
                XmlElement root = doc.CreateElement("configuration"); // 创建根节点album
                doc.AppendChild(root);
                cmdconfig = doc.CreateElement("cmdconfig"); // 创建根节点album
                root.AppendChild(cmdconfig);
            }

            cmdconfig.RemoveAll();
            foreach (var item in cmd_table) {
                XmlElement cmd = doc.CreateElement("cmd");
                cmd.SetAttribute("id", item.ID);
                cmd.SetAttribute("name", item.Name);
                cmd.SetAttribute("cnns", item.CNNS);
                cmd.SetAttribute("content", item.CMD);
                cmd.SetAttribute("comment", item.Comment);
                cmdconfig.AppendChild(cmd);
            }

            doc.Save(System.AppDomain.CurrentDomain.BaseDirectory + @"\sockclient.xml");
        }
    }
}
