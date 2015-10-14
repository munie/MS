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
using System.Windows.Shapes;
using System.Collections.ObjectModel;
using System.Threading;
using System.Net;
using System.IO;
using System.Xml;
using Mnn.MnnSocket;

namespace SockMgr
{
    /// <summary>
    /// SockMgrWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SockMgrWindow : Window
    {
        public SockMgrWindow()
        {
            InitializeComponent();

            init();
            config();
        }

        public static readonly string base_dir = System.AppDomain.CurrentDomain.BaseDirectory + @"\";
        public static readonly string conf_name = "sockmgr.xml";
        private SockSessManager sessmgr;
        public ObservableCollection<SockUnit> SockTable { get; set; }
        public ObservableCollection<CmdUnit> CmdTable { get; set; }

        private void init()
        {
            // init sessmgr
            sessmgr = new SockSessManager();
            sessmgr.sess_parse += new SockSessManager.SessParseDelegate(sessmgr_sess_parse);
            sessmgr.sess_create += new SockSessManager.SessCreateDelegate(sessmgr_sess_create);
            sessmgr.sess_delete += new SockSessManager.SessDeleteDelegate(sessmgr_sess_delete);
            Thread thread = new Thread(() => { while (true) sessmgr.Perform(1000); });
            thread.IsBackground = true;
            thread.Start();

            // init SockTable
            SockTable = new ObservableCollection<SockUnit>();
            CmdTable = new ObservableCollection<CmdUnit>();
            DataContext = new { SockTable = SockTable, CmdTable = CmdTable };
        }

        private void config()
        {
            if (File.Exists(base_dir + conf_name) == false) {
                System.Windows.MessageBox.Show("未找到配置文件" );
                return;
            }

            /// ** Initialize Start ====================================================
            try {
                XmlDocument doc = new XmlDocument();
                doc.Load(base_dir + conf_name);

                // socket
                foreach (XmlNode item in doc.SelectNodes("/configuration/socket/sockitem")) {
                    SockUnit sock = new SockUnit();
                    sock.ID = item.Attributes["id"].Value;
                    sock.Name = item.Attributes["name"].Value;
                    sock.Type = item.Attributes["type"].Value;
                    string[] str = item.Attributes["ep"].Value.Split(':');
                    if (str.Count() == 2)
                        sock.EP = new IPEndPoint(IPAddress.Parse(str[0]), int.Parse(str[1]));
                    if (sock.Type == SockUnit.TypeListen)
                        sock.Comment = sock.EP.ToString() + " L " + SockUnit.StateClosed;
                    else if (sock.Type == SockUnit.TypeConnect)
                        sock.Comment = sock.EP.ToString() + " C " + SockUnit.StateClosed;
                    SockTable.Add(sock);
                }

                // command
                foreach (XmlNode item in doc.SelectNodes("/configuration/command/cmditem")) {
                    CmdUnit cmd = new CmdUnit();
                    cmd.ID = item.Attributes["id"].Value;
                    cmd.Name = item.Attributes["name"].Value;
                    cmd.Cmd = item.Attributes["content"].Value;
                    cmd.Comment = item.Attributes["comment"].Value;
                    CmdTable.Add(cmd);
                }
            }
            catch (Exception ex) {
                Mnn.MnnUtil.Logger.WriteException(ex);
                System.Windows.MessageBox.Show("配置文件读取错误" );
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
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (sess.type == SessType.accept) {
                    var subset = from s in SockTable
                                 where s.Type == SockUnit.TypeListen && s.EP.Port == (sess.sock.LocalEndPoint as IPEndPoint).Port
                                 select s;
                    foreach (var item in subset) {
                        item.Childs.Add(new SockUnit()
                        {
                            Name = "accept",
                            EP = sess.ep,
                            Type = SockUnit.TypeAccept,
                            Comment = sess.ep.ToString() + " A",
                        });
                    }
                }
            }));
        }

        private void sessmgr_sess_delete(object sender, SockSess sess)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (sess.type == SessType.accept) {
                    foreach (var item in SockTable) {
                        if (item.Childs.Count == 0)
                            continue;

                        foreach (var i in item.Childs) {
                            if (i.EP.Equals(sess.ep)) {
                                item.Childs.Remove(i);
                                return;
                            }
                        }
                    }
                }
            }));
        }

        // treeview menu methods =====================================================================

        private void MenuItem_Listen_Click(object sender, RoutedEventArgs e)
        {
            SockUnit sock = treeSock.SelectedItem as SockUnit;

            if (sock != null && sock.Type == SockUnit.TypeListen &&
                sock.Comment.Contains(SockUnit.StateClosed) && sessmgr.AddListenSession(sock.EP))
                sock.Comment = sock.Comment.Replace(SockUnit.StateClosed, SockUnit.StateListened);
        }

        private void MenuItem_Connect_Click(object sender, RoutedEventArgs e)
        {
            SockUnit sock = treeSock.SelectedItem as SockUnit;

            if (sock != null && sock.Type == SockUnit.TypeConnect &&
                sock.Comment.Contains(SockUnit.StateClosed) && sessmgr.AddConnectSession(sock.EP))
                sock.Comment = sock.Comment.Replace(SockUnit.StateClosed, SockUnit.StateConnected);
        }

        private void MenuItem_Close_Click(object sender, RoutedEventArgs e)
        {
            SockUnit sock = treeSock.SelectedItem as SockUnit;

            if (sock != null)
                sessmgr.RemoveSession(sock.EP);

            if (sock.Type == SockUnit.TypeListen)
                sock.Comment = sock.Comment.Replace(SockUnit.StateListened, SockUnit.StateClosed);
            else if (sock.Type == SockUnit.TypeConnect)
                sock.Comment = sock.Comment.Replace(SockUnit.StateConnected, SockUnit.StateClosed);
        }

        private void TreeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var treeViewItem = VisualUpwardSearch<TreeViewItem>(e.OriginalSource as DependencyObject) as TreeViewItem;
            if (treeViewItem != null) {
                treeViewItem.Focus();
                e.Handled = true;
            }
        }

        static DependencyObject VisualUpwardSearch<T>(DependencyObject source)
        {
            while (source != null && source.GetType() != typeof(T))
                source = VisualTreeHelper.GetParent(source);

            return source;
        }

        // Cmd methods ======================================================================

        private void MenuItem_SendCmd_Click(object sender, RoutedEventArgs e)
        {
            if (treeSock.SelectedItem == null)
                return;

            SockUnit sock = treeSock.SelectedItem as SockUnit;

            foreach (CmdUnit item in lstViewCmd.SelectedItems) {
                foreach (var i in sessmgr.sess_table) {
                    if (i.ep.Equals(sock.EP)) {
                        if (i.type != SessType.listen)
                            i.sock.Send(Mnn.MnnUtil.ConvertUtil.CmdstrToBytes(item.Cmd));
                        break;
                    }
                }
            }
        }

        private void MenuItem_EditCmd_Click(object sender, RoutedEventArgs e)
        {
            if (lstViewCmd.SelectedItems.Count == 0)
                return;

            using (CmdInputDialog input = new CmdInputDialog()) {
                input.Owner = this;
                input.Title = "编辑命令";
                input.textBoxID.Text = (lstViewCmd.SelectedItems[0] as CmdUnit).ID;
                input.textBoxName.Text = (lstViewCmd.SelectedItems[0] as CmdUnit).Name;
                input.textBoxCmd.Text = (lstViewCmd.SelectedItems[0] as CmdUnit).Cmd;
                input.textBoxComment.Text = (lstViewCmd.SelectedItems[0] as CmdUnit).Comment;
                input.textBoxCmd.Focus();
                input.textBoxCmd.SelectionStart = input.textBoxCmd.Text.Length;

                if (input.ShowDialog() == false)
                    return;

                foreach (CmdUnit item in lstViewCmd.SelectedItems) {
                    item.ID = input.textBoxID.Text;
                    item.Name = input.textBoxName.Text;
                    item.Cmd = input.textBoxCmd.Text;
                    item.Comment = input.textBoxComment.Text;
                    break;
                }
            }
        }

        private void MenuItem_NewCmd_Click(object sender, RoutedEventArgs e)
        {
            using (CmdInputDialog input = new CmdInputDialog()) {
                input.Owner = this;
                input.Title = "新增命令";
                input.textBoxID.Focus();

                if (input.ShowDialog() == false)
                    return;

                CmdUnit cmd = new CmdUnit();
                cmd.ID = input.textBoxID.Text;
                cmd.Name = input.textBoxName.Text;
                cmd.Cmd = input.textBoxCmd.Text;
                cmd.Comment = input.textBoxComment.Text;
                CmdTable.Add(cmd);
            }
        }

        private void MenuItem_DelCmd_Click(object sender, RoutedEventArgs e)
        {
            List<CmdUnit> tmp = new List<CmdUnit>();

            foreach (CmdUnit item in lstViewCmd.SelectedItems)
                tmp.Add(item);

            foreach (var item in tmp)
                CmdTable.Remove(item);
        }

        private void MenuItem_SaveCmd_Click(object sender, RoutedEventArgs e)
        {
            XmlDocument doc = new XmlDocument();
            XmlNode config;

            if (File.Exists(base_dir + conf_name)) {
                doc.Load(base_dir + conf_name);
                config = doc.SelectSingleNode("/configuration/command");
            }
            else {
                doc.AppendChild(doc.CreateXmlDeclaration("1.0", "utf-8", ""));
                XmlElement root = doc.CreateElement("configuration"); // 创建根节点album
                doc.AppendChild(root);
                config = doc.CreateElement("command"); // 创建根节点album
                root.AppendChild(config);
            }

            config.RemoveAll();
            foreach (var item in CmdTable) {
                XmlElement cmd = doc.CreateElement("cmditem");
                cmd.SetAttribute("id", item.ID);
                cmd.SetAttribute("name", item.Name);
                cmd.SetAttribute("content", item.Cmd);
                cmd.SetAttribute("comment", item.Comment);
                config.AppendChild(cmd);
            }

            doc.Save(base_dir + conf_name);
        }
    }
}
