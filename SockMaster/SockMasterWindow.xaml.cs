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
using System.Reflection;
using System.Diagnostics;
using System.Xml;
using Mnn.MnnSock;

namespace SockMaster
{
    /// <summary>
    /// SockMasterWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SockMasterWindow : Window
    {
        public SockMasterWindow()
        {
            InitializeComponent();

            InitailizeWindowName();
            InitailizeStatusBar();

            init();
            config();

            // autorun socket
            foreach (var sock in SockTable) {
                if (sock.Autorun == true)
                    sock.State = SockState.Opening;
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

        private void InitailizeWindowName()
        {
            // Format Main Form's Name
            Assembly asm = Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(asm.Location);
            this.Title = string.Format("{0} {1}.{2}.{3}-{4} - Powered By {5}",
                fvi.ProductName,
                fvi.ProductMajorPart,
                fvi.ProductMinorPart,
                fvi.ProductBuildPart,
                fvi.ProductPrivatePart,
                fvi.CompanyName);
        }

        private void InitailizeStatusBar()
        {
            // Display TimeRun
            DateTime startTime = DateTime.Now;
            System.Windows.Threading.DispatcherTimer timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = new TimeSpan(0, 0, 1);
            timer.Tick += new EventHandler((s, ea) =>
            {
                txtTimeRun.Text = "运行时间 " + DateTime.Now.Subtract(startTime).ToString(@"dd\-hh\:mm\:ss");

                long memory = GC.GetTotalMemory(false) / 1000;
                long diff = memory - Convert.ToInt32(txtMemory.Text);
                txtMemory.Text = memory.ToString();
                if (diff >= 0)
                    txtMemoryDiff.Text = "+" + diff;
                else
                    txtMemoryDiff.Text = "-" + diff;
            });
            timer.Start();
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
                    sock.Type = (SockType)Enum.Parse(typeof(SockType), item.Attributes["type"].Value);
                    //sock.Type = item.Attributes["type"].Value == "listen" ? SockType.listen : SockType.connect;
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
            catch (Exception) {
                System.Windows.MessageBox.Show("配置文件读取错误" );
            }
            /// ** Initialize End ====================================================
        }

        private void perform_sock_table()
        {
            foreach (var item in SockTable) {
                /// ** first handle child(accept) socket
                SockUnit[] childArray;
                lock (SockTable) {
                    childArray = item.Childs.ToArray();
                }
                foreach (var child in childArray) {
                    if (child.SendBuffSize != 0 && child.State == SockState.Opened) {
                        sessmgr.SendSession(child.Sock, child.SendBuff);
                        //Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                            child.SendBuffSize = 0;
                        //}));
                    }
                    if (child.State == SockState.Closing && child.State != SockState.Closed) {
                        sessmgr.RemoveSession(child.Sock);
                        child.State = SockState.Closed;
                    }
                }

                /// ** second handle sending data
                if (item.SendBuffSize != 0 && item.State == SockState.Opened) {
                    sessmgr.SendSession(item.Sock, item.SendBuff);
                    //Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                        item.SendBuffSize = 0;
                    //}));
                }

                /// ** third handle open or close
                if (item.State == SockState.Opening) {
                    if (item.Type == SockType.listen) {
                        if ((item.Sock = sessmgr.AddListenSession(item.EP)) != null)
                            item.State = SockState.Opened;
                        else
                            item.State = SockState.Closed;
                    }
                    else if (item.Type == SockType.connect) {
                        if ((item.Sock = sessmgr.AddConnectSession(item.EP)) != null)
                            item.State = SockState.Opened;
                        else
                            item.State = SockState.Closed;
                    }
                }
                else if (item.State == SockState.Closing) {
                    sessmgr.RemoveSession(item.Sock);
                    item.State = SockState.Closed;
                }
            }
        }

        private void sessmgr_sess_parse(object sender, SockSess sess)
        {
            byte[] data = sess.rdata.Take(sess.rdata_size).ToArray();
            sess.rdata_size = 0;

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (txtBoxMsg.Text.Length >= 20 * 1024)
                    txtBoxMsg.Clear();

                StringBuilder sb = new StringBuilder();
                foreach (var item in data) {
                    if (item >= 0x20 && item < 0x7f) {
                        sb.Append(Convert.ToChar(item));
                        continue;
                    }
                    string s = Convert.ToString(item, 16);
                    if (s.Length == 1)
                        s = "0" + s;
                    sb.Append("(" + s + ")");
                }
                sb.Replace(")(", "");

                txtBoxMsg.AppendText(DateTime.Now + " (" +
                    sess.rep.ToString() + " => " + sess.lep.ToString() + ")\n");
                txtBoxMsg.AppendText(sb.ToString() + "\n");
                txtBoxMsg.ScrollToEnd();
            }));
        }

        private void sessmgr_sess_create(object sender, SockSess sess)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (sess.type == SockType.accept) {
                    currentAcceptCount.Text = (Convert.ToInt32(currentAcceptCount.Text) + 1).ToString();
                    historyAcceptOpenCount.Text = (Convert.ToInt32(historyAcceptOpenCount.Text) + 1).ToString();

                    var subset = from s in SockTable
                                 where s.Type == SockType.listen && s.EP.Port == sess.lep.Port
                                 select s;
                    foreach (var item in subset) {
                        lock (SockTable) {
                            item.Childs.Add(new SockUnit()
                            {
                                ID = "-",
                                Name = "accept",
                                Type = sess.type,
                                Sock = sess.sock,
                                EP = sess.rep,
                                State = SockState.Opened,
                            });
                            break;
                        }
                    }
                }
            }));
        }

        private void sessmgr_sess_delete(object sender, SockSess sess)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (sess.type == SockType.accept) {
                    currentAcceptCount.Text = (Convert.ToInt32(currentAcceptCount.Text) - 1).ToString();
                    historyAcceptCloseCount.Text = (Convert.ToInt32(historyAcceptCloseCount.Text) + 1).ToString();

                    foreach (var item in SockTable) {
                        if (item.Childs.Count == 0)
                            continue;
                        foreach (var i in item.Childs) {
                            if (i.Sock == sess.sock) {
                                lock (SockTable) {
                                    item.Childs.Remove(i);
                                }
                                return;
                            }
                        }
                    }
                }
                else if (sess.type == SockType.connect) {
                    foreach (var item in SockTable) {
                        if (item.Sock == sess.sock) {
                            item.State = SockState.Closed;
                            return;
                        }
                    }
                }
                else if (sess.type == SockType.listen) {
                    foreach (var item in SockTable) {
                        if (item.Sock == sess.sock) {
                            item.State = SockState.Closed;
                            return;
                        }
                    }
                }
            }));
        }

        // treeview menu methods =====================================================================

        private void MenuItem_Open_Click(object sender, RoutedEventArgs e)
        {
            if (treeSock.SelectedItem != null && (treeSock.SelectedItem as SockUnit).State == SockState.Closed)
                (treeSock.SelectedItem as SockUnit).State = SockState.Opening;
        }

        private void MenuItem_Close_Click(object sender, RoutedEventArgs e)
        {
            if (treeSock.SelectedItem != null && (treeSock.SelectedItem as SockUnit).State == SockState.Opened)
                (treeSock.SelectedItem as SockUnit).State = SockState.Closing;
        }

        private void MenuItem_EditSock_Click(object sender, RoutedEventArgs e)
        {
            if (treeSock.SelectedItem == null)
                return;

            SockUnit sock = treeSock.SelectedItem as SockUnit;

            // only closed listen & connect can be modified
            if (sock.State != SockState.Closed)
                return;

            using (SockInputDialog input = new SockInputDialog()) {
                input.Owner = this;
                input.Title = "Edit";
                input.textBoxID.Text = sock.ID;
                input.textBoxName.Text = sock.Name;
                input.textBoxEP.Text = sock.EP.ToString();
                if (sock.Type == SockType.listen)
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
                    sock.Type = SockType.listen;
                else
                    sock.Type = SockType.connect;
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
                    sock.Type = SockType.listen;
                else
                    sock.Type = SockType.connect;
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
            if (sock.State != SockState.Closed)
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
                if (item.Type == SockType.accept)
                    continue;
                XmlElement sockitem = doc.CreateElement("sockitem");
                sockitem.SetAttribute("id", item.ID);
                sockitem.SetAttribute("name", item.Name);
                sockitem.SetAttribute("type", item.Type.ToString());
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
                unit.SendBuff = SockConvert.CmdstrToBytes(item.Cmd, '#');
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
