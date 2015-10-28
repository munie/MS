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
using mnn.net;
using mnn.util;

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

        private void Window_Closed(object sender, EventArgs e)
        {
            (this.Owner as MainWindow).Close();
        }

        // Menu methods for TreeView =============================================================

        private void MenuItem_SockOpen_Click(object sender, RoutedEventArgs e)
        {
            SockUnit sock = treeSock.SelectedItem as SockUnit;
            if (sock == null || sock.State != SockState.Closed) return;

            sock.State = SockState.Opening;
            (this.Owner as MainWindow).cmdcer.AppendCommand(MainWindow.SOCK_OPEN, treeSock.SelectedItem);
        }

        private void MenuItem_SockClose_Click(object sender, RoutedEventArgs e)
        {
            SockUnit sock = treeSock.SelectedItem as SockUnit;
            if (sock == null || sock.State != SockState.Opened) return;

            sock.State = SockState.Closing;
            (this.Owner as MainWindow).cmdcer.AppendCommand(MainWindow.SOCK_CLOSE, treeSock.SelectedItem);
        }

        private void MenuItem_SockEdit_Click(object sender, RoutedEventArgs e)
        {
            SockUnit sock = treeSock.SelectedItem as SockUnit;
            if (sock == null || sock.State != SockState.Closed) return;

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

        private void MenuItem_SockAdd_Click(object sender, RoutedEventArgs e)
        {
            using (SockInputDialog input = new SockInputDialog()) {
                input.Owner = this;
                input.Title = "Add";
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
                (this.Owner as MainWindow).SockTable.Add(sock);
            }
        }

        private void MenuItem_SockDel_Click(object sender, RoutedEventArgs e)
        {
            SockUnit sock = treeSock.SelectedItem as SockUnit;
            if (sock == null || sock.State != SockState.Closed) return;

            (this.Owner as MainWindow).SockTable.Remove(sock);
        }

        private void MenuItem_SockSave_Click(object sender, RoutedEventArgs e)
        {
            XmlDocument doc = new XmlDocument();
            XmlNode config;

            if (File.Exists(MainWindow.CONF_PATH)) {
                doc.Load(MainWindow.CONF_PATH);
                config = doc.SelectSingleNode("/configuration/socket");
            } else {
                doc.AppendChild(doc.CreateXmlDeclaration("1.0", "utf-8", ""));
                XmlElement root = doc.CreateElement("configuration"); // 创建根节点album
                doc.AppendChild(root);
                config = doc.CreateElement("socket"); // 创建根节点album
                root.AppendChild(config);
            }

            config.RemoveAll();
            foreach (var item in (this.Owner as MainWindow).SockTable) {
                if (item.Type == SockType.accept) continue;

                XmlElement sockitem = doc.CreateElement("sockitem");
                sockitem.SetAttribute("id", item.ID);
                sockitem.SetAttribute("name", item.Name);
                sockitem.SetAttribute("type", item.Type.ToString());
                sockitem.SetAttribute("ep", item.EP.ToString());
                sockitem.SetAttribute("autorun", item.Autorun.ToString());
                config.AppendChild(sockitem);
            }

            doc.Save(MainWindow.CONF_PATH);
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

        // Menu methods for ListView ================================================================

        private void MenuItem_CmdSend_Click(object sender, RoutedEventArgs e)
        {
            SockUnit sock = treeSock.SelectedItem as SockUnit;
            if (sock == null || sock.State != SockState.Opened) return;

            // 发送所有选中的命令，目前只支持发送第一条命令...
            foreach (CmdUnit item in lstViewCmd.SelectedItems) {
                sock.SendBuff = SockConvert.ParseCmdstrToBytes(item.Cmd, '#');
                (this.Owner as MainWindow).cmdcer.AppendCommand(MainWindow.SOCK_SEND, sock);
                break;
            }
        }

        private void MenuItem_CmdEdit_Click(object sender, RoutedEventArgs e)
        {
            if (lstViewCmd.SelectedItems.Count == 0) return;

            using (CmdInputDialog input = new CmdInputDialog()) {
                input.Owner = this;
                input.Title = "Eidt";
                input.textBoxID.Text = (lstViewCmd.SelectedItems[0] as CmdUnit).ID;
                input.textBoxName.Text = (lstViewCmd.SelectedItems[0] as CmdUnit).Name;
                input.textBoxCmd.Text = (lstViewCmd.SelectedItems[0] as CmdUnit).Cmd;
                input.textBoxComment.Text = (lstViewCmd.SelectedItems[0] as CmdUnit).Comment;
                input.textBoxCmd.Focus();
                input.textBoxCmd.SelectionStart = input.textBoxCmd.Text.Length;

                if (input.ShowDialog() == false) return;

                foreach (CmdUnit item in lstViewCmd.SelectedItems) {
                    item.ID = input.textBoxID.Text;
                    item.Name = input.textBoxName.Text;
                    item.Cmd = input.textBoxCmd.Text;
                    item.Comment = input.textBoxComment.Text;
                    break;
                }
            }
        }

        private void MenuItem_CmdAdd_Click(object sender, RoutedEventArgs e)
        {
            using (CmdInputDialog input = new CmdInputDialog()) {
                input.Owner = this;
                input.Title = "Add";
                input.textBoxID.Focus();

                if (input.ShowDialog() == false) return;

                CmdUnit cmd = new CmdUnit();
                cmd.ID = input.textBoxID.Text;
                cmd.Name = input.textBoxName.Text;
                cmd.Cmd = input.textBoxCmd.Text;
                cmd.Comment = input.textBoxComment.Text;
                (this.Owner as MainWindow).CmdTable.Add(cmd);
            }
        }

        private void MenuItem_CmdDel_Click(object sender, RoutedEventArgs e)
        {
            List<CmdUnit> tmp = new List<CmdUnit>();

            foreach (CmdUnit item in lstViewCmd.SelectedItems)
                tmp.Add(item);

            foreach (var item in tmp)
                (this.Owner as MainWindow).CmdTable.Remove(item);
        }

        private void MenuItem_CmdSave_Click(object sender, RoutedEventArgs e)
        {
            XmlDocument doc = new XmlDocument();
            XmlNode config;

            if (File.Exists(MainWindow.CONF_PATH)) {
                doc.Load(MainWindow.CONF_PATH);
                config = doc.SelectSingleNode("/configuration/command");
            } else {
                doc.AppendChild(doc.CreateXmlDeclaration("1.0", "utf-8", ""));
                XmlElement root = doc.CreateElement("configuration"); // 创建根节点album
                doc.AppendChild(root);
                config = doc.CreateElement("command"); // 创建根节点album
                root.AppendChild(config);
            }

            config.RemoveAll();
            foreach (var item in (this.Owner as MainWindow).CmdTable) {
                XmlElement cmd = doc.CreateElement("cmditem");
                cmd.SetAttribute("id", item.ID);
                cmd.SetAttribute("name", item.Name);
                cmd.SetAttribute("content", item.Cmd);
                cmd.SetAttribute("comment", item.Comment);
                config.AppendChild(cmd);
            }

            doc.Save(MainWindow.CONF_PATH);
        }

        //private void MenuItem_CmdOpen_Click(object sender, RoutedEventArgs e)
        //{
        //    System.Diagnostics.Process.Start("Explorer.exe", MainWindow.BASE_DIR);
        //}
    }
}
