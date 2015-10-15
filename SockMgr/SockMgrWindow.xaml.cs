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
            perform_once();
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
                    sock.Autorun = bool.Parse(item.Attributes["autorun"].Value);
                    sock.UpdateTitle();
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

        private void perform_once()
        {
            // autorun socket
            foreach (var sock in SockTable) {
                if (sock.Autorun == true)
                    sock.State = SockUnitState.Opening;
            }

            // new thread for running socket
            // from now on, we can't call motheds of sessmgr directly in this thread
            Thread thread = new Thread(() =>
            {
                while (true) {
                    perform_sock_table();
                    sessmgr.Perform(1000);
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        private void perform_sock_table()
        {
            foreach (var item in SockTable) {
                /// ** first handle child(accept) socket
                foreach (var child in item.Childs) {
                    if (child.SendBuffSize != 0 && child.State == SockUnitState.Opened) {
                        sessmgr.SendSession(child.EP, child.SendBuff);
                        //Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                            child.SendBuffSize = 0;
                        //}));
                    }
                    if (child.State == SockUnitState.Closing && child.State != SockUnitState.Closed) {
                        sessmgr.RemoveSession(child.EP);
                        child.State = SockUnitState.Closed;
                    }
                }
                /// ** second handle sending data
                if (item.SendBuffSize != 0 && item.State == SockUnitState.Opened) {
                    sessmgr.SendSession(item.EP, item.SendBuff);
                    //Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                        item.SendBuffSize = 0;
                    //}));
                }
                /// ** third handle open or close
                if (item.State == SockUnitState.Opening && item.State != SockUnitState.Opened) {
                    if (item.Type == SockUnit.TypeListen) {
                        if (sessmgr.AddListenSession(item.EP))
                            item.State = SockUnitState.Opened;
                        else
                            item.State = SockUnitState.Closed;
                    }
                    else if (item.Type == SockUnit.TypeConnect) {
                        if (sessmgr.AddConnectSession(item.EP))
                            item.State = SockUnitState.Opened;
                        else
                            item.State = SockUnitState.Closed;
                    }
                }
                else if (item.State == SockUnitState.Closing && item.State != SockUnitState.Closed) {
                    sessmgr.RemoveSession(item.EP);
                    item.State = SockUnitState.Closed;
                }
            }
        }

        private void sessmgr_sess_parse(object sender, SockSess sess)
        {
            byte[] data = sess.rdata.Take(sess.rdata_size).ToArray();
            sess.rdata_size = 0;

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (txtMsg.Text.Length >= 20 * 1024)
                    txtMsg.Clear();

                string hexstr = "";
                foreach (var item in data) {
                    if (item >= 0x20 && item < 0x7f) {
                        hexstr += Convert.ToChar(item);
                        continue;
                    }
                    string s = Convert.ToString(item, 16);
                    if (s.Length == 1)
                        s = "0" + s;
                    hexstr += "(" + s + ")";
                }
                hexstr = hexstr.Replace(")(", "");

                txtMsg.AppendText(DateTime.Now + " (" +
                    sess.sock.RemoteEndPoint.ToString() + " => " + sess.sock.LocalEndPoint.ToString() + ")\n");
                txtMsg.AppendText(hexstr + "\n");
                txtMsg.ScrollToEnd();
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
                            ID = "-",
                            Name = "accept",
                            EP = sess.ep,
                            Type = SockUnit.TypeAccept,
                            State = SockUnitState.Opened,
                        });
                        break;
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
                else if (sess.type == SessType.connect) {
                    foreach (var item in SockTable) {
                        if (item.EP.Equals(sess.ep)) {
                            item.State = SockUnitState.Closed;
                            return;
                        }
                    }
                }
                else if (sess.type == SessType.listen) {
                    foreach (var item in SockTable) {
                        if (item.EP.Equals(sess.ep)) {
                            item.State = SockUnitState.Closed;
                            return;
                        }
                    }
                }
            }));
        }

        // treeview menu methods =====================================================================

        private void MenuItem_Open_Click(object sender, RoutedEventArgs e)
        {
            if (treeSock.SelectedItem != null && (treeSock.SelectedItem as SockUnit).State != SockUnitState.Opened)
                (treeSock.SelectedItem as SockUnit).State = SockUnitState.Opening;
        }

        private void MenuItem_Close_Click(object sender, RoutedEventArgs e)
        {
            if (treeSock.SelectedItem != null && (treeSock.SelectedItem as SockUnit).State != SockUnitState.Closed)
                (treeSock.SelectedItem as SockUnit).State = SockUnitState.Closing;
        }

        private void MenuItem_EditSock_Click(object sender, RoutedEventArgs e)
        {
            if (treeSock.SelectedItem == null)
                return;

            SockUnit sock = treeSock.SelectedItem as SockUnit;

            // only closed listen & connect can be modified
            if (sock.State != SockUnitState.Closed)
                return;

            using (SockInputDialog input = new SockInputDialog()) {
                input.Owner = this;
                input.Title = "Edit";
                input.textBoxID.Text = sock.ID;
                input.textBoxName.Text = sock.Name;
                input.textBoxEP.Text = sock.EP.ToString();
                if (sock.Type == SockUnit.TypeListen)
                    input.radioButtonListen.IsChecked = true;
                else
                    input.radioButtonConnect.IsChecked = true;
                input.checkBoxAutorun.IsChecked = sock.Autorun;
                input.textBoxEP.Focus();
                input.textBoxEP.SelectionStart = input.textBoxEP.Text.Length;

                if (input.ShowDialog() == false)
                    return;

                sock.ID = input.textBoxID.Text;
                sock.Name = input.textBoxName.Text;
                string[] str = input.textBoxEP.Text.Split(':');
                if (str.Count() == 2)
                    sock.EP = new IPEndPoint(IPAddress.Parse(str[0]), int.Parse(str[1]));
                if (input.radioButtonListen.IsChecked == true)
                    sock.Type = SockUnit.TypeListen;
                else
                    sock.Type = SockUnit.TypeConnect;
                sock.Autorun = (bool)input.checkBoxAutorun.IsChecked;
                sock.UpdateTitle();
            }
        }

        private void MenuItem_NewSock_Click(object sender, RoutedEventArgs e)
        {
            using (SockInputDialog input = new SockInputDialog()) {
                input.Owner = this;
                input.Title = "New";
                input.textBoxID.Focus();

                if (input.ShowDialog() == false)
                    return;

                SockUnit sock = new SockUnit();
                sock.ID = input.textBoxID.Text;
                sock.Name = input.textBoxName.Text;
                string[] str = input.textBoxEP.Text.Split(':');
                if (str.Count() == 2)
                    sock.EP = new IPEndPoint(IPAddress.Parse(str[0]), int.Parse(str[1]));
                if (input.radioButtonListen.IsChecked == true)
                    sock.Type = SockUnit.TypeListen;
                else
                    sock.Type = SockUnit.TypeConnect;
                sock.Autorun = (bool)input.checkBoxAutorun.IsChecked;
                sock.UpdateTitle();
                SockTable.Add(sock);
            }
        }

        private void MenuItem_DelSock_Click(object sender, RoutedEventArgs e)
        {
            if (treeSock.SelectedItem == null)
                return;

            SockUnit sock = treeSock.SelectedItem as SockUnit;

            // only closed listen & connect can be modified
            if (sock.State != SockUnitState.Closed)
                return;

            SockTable.Remove(sock);
        }

        private void MenuItem_SaveSock_Click(object sender, RoutedEventArgs e)
        {
            XmlDocument doc = new XmlDocument();
            XmlNode config;

            if (File.Exists(base_dir + conf_name)) {
                doc.Load(base_dir + conf_name);
                config = doc.SelectSingleNode("/configuration/socket");
            }
            else {
                doc.AppendChild(doc.CreateXmlDeclaration("1.0", "utf-8", ""));
                XmlElement root = doc.CreateElement("configuration"); // 创建根节点album
                doc.AppendChild(root);
                config = doc.CreateElement("socket"); // 创建根节点album
                root.AppendChild(config);
            }

            config.RemoveAll();
            foreach (var item in SockTable) {
                if (item.Type == SockUnit.TypeAccept)
                    continue;
                XmlElement sockitem = doc.CreateElement("sockitem");
                sockitem.SetAttribute("id", item.ID);
                sockitem.SetAttribute("name", item.Name);
                sockitem.SetAttribute("type", item.Type);
                sockitem.SetAttribute("ep", item.EP.ToString());
                sockitem.SetAttribute("autorun", item.Autorun.ToString());
                config.AppendChild(sockitem);
            }

            doc.Save(base_dir + conf_name);
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

            SockUnit unit = treeSock.SelectedItem as SockUnit;

            // 发送所有选中的命令，目前只支持发送第一条命令...
            foreach (CmdUnit item in lstViewCmd.SelectedItems) {
                unit.SendBuff = Mnn.MnnUtil.ConvertUtil.CmdstrToBytes(item.Cmd, '|');
                unit.SendBuffSize = unit.SendBuff.Length;
                break;
            }
        }

        private void MenuItem_EditCmd_Click(object sender, RoutedEventArgs e)
        {
            if (lstViewCmd.SelectedItems.Count == 0)
                return;

            using (CmdInputDialog input = new CmdInputDialog()) {
                input.Owner = this;
                input.Title = "Eidt";
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
                input.Title = "New";
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
