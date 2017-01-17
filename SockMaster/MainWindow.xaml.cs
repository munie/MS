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
using System.Net.Sockets;
using mnn.net;
using mnn.misc.service;

namespace SockMaster
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            Initailize();
            InitailizeWindowName();
            InitailizeStatusBar();
        }

        private static readonly string BASE_DIR = System.AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string CONF_NAME = "SockMaster.xml";
        private static readonly string CONF_PATH = BASE_DIR + CONF_NAME;
        private ObservableCollection<CmdUnit> cmdTable;
        private SockSessClient client;

        private void Initailize()
        {
            // init core
            Core core = new Core();

            // init cmdtable
            cmdTable = new ObservableCollection<CmdUnit>();
            try {
                if (File.Exists(BASE_DIR + CONF_NAME) == false) {
                    System.Windows.MessageBox.Show(CONF_NAME + ": can't find it.");
                    return;
                }

                XmlDocument doc = new XmlDocument();
                doc.Load(BASE_DIR + CONF_NAME);

                foreach (XmlNode item in doc.SelectNodes("/configuration/commands/cmditem")) {
                    CmdUnit cmd = new CmdUnit();
                    cmd.ID = item.Attributes["id"].Value;
                    cmd.Name = item.Attributes["name"].Value;
                    cmd.Cmd = item.Attributes["content"].Value;
                    cmd.Encrypt = bool.Parse(item.Attributes["encrypt"].Value);
                    cmd.ContentMode = (ServiceRequestContentMode)Enum.Parse(typeof(ServiceRequestContentMode), item.Attributes["content-mode"].Value);
                    cmdTable.Add(cmd);
                }
            } catch (Exception) {
                System.Windows.MessageBox.Show(CONF_NAME + ": syntax error.");
            }

            // init tcp
            client = new SockSessClient();
            client.Connect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), core.DataUI.Port));
            client.recv_event += new SockSessDelegate((s) => { (s as SockSessClient).rfifo.Take(); });
            this.txtPromote.Text += " At " + core.DataUI.Port;

            // init context
            DataContext = new { SockTable = core.DataUI.SockUnitGroup, CmdTable = cmdTable, DataUI = core.DataUI };
            core.DataUI.MsgBox = this.txtBoxMsg;
            this.currentAcceptCount.SetBinding(TextBlock.TextProperty, new Binding("DataUI.CurrentAcceptCount"));
            this.historyAcceptOpenCount.SetBinding(TextBlock.TextProperty, new Binding("DataUI.HistoryAcceptOpenCount"));
            this.historyAcceptCloseCount.SetBinding(TextBlock.TextProperty, new Binding("DataUI.HistoryAcceptCloseCount"));
        }

        private void InitailizeWindowName()
        {
            // Format Main Form's Name
            Assembly asm = Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(asm.Location);
            this.Title = string.Format("{0} {1}.{2}.{3} - Powered By {4}",
                fvi.ProductName,
                fvi.ProductMajorPart,
                fvi.ProductMinorPart,
                fvi.ProductBuildPart,
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

        // Menu methods for TreeView =============================================================

        private void MenuItem_SockOpen_Click(object sender, RoutedEventArgs e)
        {
            SockUnit sock = treeSock.SelectedItem as SockUnit;
            if (sock == null || sock.State != SockState.Closed) return;

            sock.State = SockState.Opening;

            IPEndPoint ep = sock.Type == SockType.listen ? sock.Lep : sock.Rep;
            byte[] buffer = Encoding.UTF8.GetBytes("/center/sockopen"
                + "?type=" + sock.Type.ToString()
                + "&ip=" + ep.Address.ToString()
                + "&port=" + ep.Port.ToString()
                + "&id=" + sock.ID);
            ServiceRequest.InsertHeader(ServiceRequestContentMode.url, ref buffer);

            client.wfifo.Append(buffer);
        }

        private void MenuItem_SockClose_Click(object sender, RoutedEventArgs e)
        {
            SockUnit sock = treeSock.SelectedItem as SockUnit;
            if (sock == null || sock.State != SockState.Opened) return;

            sock.State = SockState.Closing;

            IPEndPoint ep = sock.Type != SockType.accept ? sock.Lep : sock.Rep;
            byte[] buffer = Encoding.UTF8.GetBytes("/center/sockclose"
                + "?type=" + sock.Type.ToString()
                + "&ip=" + ep.Address.ToString()
                + "&port=" + ep.Port.ToString()
                + "&id=" + sock.ID);
            ServiceRequest.InsertHeader(ServiceRequestContentMode.url, ref buffer);

            client.wfifo.Append(buffer);
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
                if (sock.Type == SockType.listen) {
                    input.textBoxEP.Text = sock.Lep.ToString();
                    input.radioButtonListen.IsChecked = true;
                } else {
                    input.textBoxEP.Text = sock.Rep.ToString();
                    input.radioButtonConnect.IsChecked = true;
                }
                input.checkBoxAutorun.IsChecked = sock.Autorun;
                input.textBoxEP.Focus();
                input.textBoxEP.SelectionStart = input.textBoxEP.Text.Length;

                if (input.ShowDialog() == false)
                    return;

                sock.ID = input.textBoxID.Text;
                sock.Name = input.textBoxName.Text;

                string[] str = input.textBoxEP.Text.Split(':');
                if (str.Count() != 2)
                    return;
                if (input.radioButtonListen.IsChecked == true) {
                    sock.Type = SockType.listen;
                    sock.Lep = new IPEndPoint(IPAddress.Parse(str[0]), int.Parse(str[1]));
                } else {
                    sock.Type = SockType.connect;
                    var host = Dns.GetHostEntry(str[0]);
                    sock.Rep = new IPEndPoint(host.AddressList[0], int.Parse(str[1]));
                }

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
                if (str.Count() != 2)
                    return;
                if (input.radioButtonListen.IsChecked == true) {
                    sock.Type = SockType.listen;
                    sock.Lep = new IPEndPoint(IPAddress.Parse(str[0]), int.Parse(str[1]));
                } else {
                    sock.Type = SockType.connect;
                    sock.Rep = new IPEndPoint(IPAddress.Parse(str[0]), int.Parse(str[1]));
                }

                sock.Autorun = (bool)input.checkBoxAutorun.IsChecked;
                sock.UpdateTitle();
                (treeSock.ItemsSource as ObservableCollection<SockUnit>).Add(sock);
            }
        }

        private void MenuItem_SockDel_Click(object sender, RoutedEventArgs e)
        {
            SockUnit sock = treeSock.SelectedItem as SockUnit;
            if (sock == null || sock.State != SockState.Closed) return;

            (treeSock.ItemsSource as ObservableCollection<SockUnit>).Remove(sock);
        }

        private void MenuItem_SockSave_Click(object sender, RoutedEventArgs e)
        {
            XmlDocument doc = new XmlDocument();
            XmlNode config;

            if (File.Exists(Core.CONF_PATH)) {
                doc.Load(Core.CONF_PATH);
                config = doc.SelectSingleNode("/configuration/sockets");
            } else {
                doc.AppendChild(doc.CreateXmlDeclaration("1.0", "utf-8", ""));
                XmlElement root = doc.CreateElement("configuration"); // 创建根节点album
                doc.AppendChild(root);
                config = doc.CreateElement("sockets"); // 创建根节点album
                root.AppendChild(config);
            }

            config.RemoveAll();
            foreach (var item in (treeSock.ItemsSource as ObservableCollection<SockUnit>)) {
                if (item.Type == SockType.accept) continue;

                XmlElement sockitem = doc.CreateElement("sockitem");
                sockitem.SetAttribute("id", item.ID);
                sockitem.SetAttribute("name", item.Name);
                sockitem.SetAttribute("type", item.Type.ToString());
                sockitem.SetAttribute("ep", item.Rep.ToString());
                sockitem.SetAttribute("autorun", item.Autorun.ToString());
                config.AppendChild(sockitem);
            }

            doc.Save(Core.CONF_PATH);
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
                // init data which is sended out to remote ep
                byte[] data = SockConvert.ParseCmdstrToBytes(item.Cmd, '#');
                if (item.Encrypt)
                    data = Encoding.UTF8.GetBytes(Convert.ToBase64String(EncryptSym.AESEncrypt(data)));
                if (item.ContentMode != ServiceRequestContentMode.none)
                    ServiceRequest.InsertHeader(item.ContentMode, ref data);

                // add internal header just for translating in SockMaster
                IPEndPoint ep = sock.Type != SockType.accept ? sock.Lep : sock.Rep;
                byte[] buffer = Encoding.UTF8.GetBytes("/center/socksend"
                    + "?type=" + sock.Type.ToString()
                    + "&ip=" + ep.Address.ToString()
                    + "&port=" + ep.Port.ToString()
                    + "&data=");
                buffer = buffer.Concat(data).ToArray();
                ServiceRequest.InsertHeader(ServiceRequestContentMode.url, ref buffer);

                client.wfifo.Append(buffer);
                break;
            }
        }

        private void MenuItem_CmdGet_Click(object sender, RoutedEventArgs e)
        {
            SockUnit sock = treeSock.SelectedItem as SockUnit;
            //if (sock == null || sock.State != SockState.Opened) return;

            // 发送所有选中的命令，目前只支持发送第一条命令...
            foreach (CmdUnit item in lstViewCmd.SelectedItems) {
                try {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(item.Cmd);
                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                    using (StreamReader reader = new StreamReader(response.GetResponseStream())) {
                        String buffer = "";
                        while ((buffer = reader.ReadLine()) != null)
                            txtBoxMsg.AppendText(buffer);
                    }
                } catch (Exception excption) {
                    txtBoxMsg.AppendText(excption.Message + "\n");
                }

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
                input.checkBoxEncrypt.IsChecked = (lstViewCmd.SelectedItems[0] as CmdUnit).Encrypt;
                input.comboBoxContentMode.ItemsSource = Enum.GetNames(typeof(ServiceRequestContentMode));
                string[] tmp = input.comboBoxContentMode.ItemsSource as string[];
                for (int i = 0; i < tmp.Length; i++) {
                    if (tmp[i] == (lstViewCmd.SelectedItems[0] as CmdUnit).ContentMode.ToString()) {
                        input.comboBoxContentMode.SelectedIndex = i;
                        break;
                    }
                }
                input.textBoxCmd.Focus();
                input.textBoxCmd.SelectionStart = input.textBoxCmd.Text.Length;

                if (input.ShowDialog() == false) return;

                foreach (CmdUnit item in lstViewCmd.SelectedItems) {
                    item.ID = input.textBoxID.Text;
                    item.Name = input.textBoxName.Text;
                    item.Cmd = input.textBoxCmd.Text;
                    item.Encrypt = input.checkBoxEncrypt.IsChecked == true ? true : false;
                    item.ContentMode = (ServiceRequestContentMode)Enum.Parse(typeof(ServiceRequestContentMode), input.comboBoxContentMode.SelectedItem.ToString());
                    break;
                }
            }
        }

        private void MenuItem_CmdAdd_Click(object sender, RoutedEventArgs e)
        {
            using (CmdInputDialog input = new CmdInputDialog()) {
                input.Owner = this;
                input.Title = "Add";
                input.comboBoxContentMode.ItemsSource = Enum.GetNames(typeof(ServiceRequestContentMode));
                input.comboBoxContentMode.SelectedIndex = 1;
                input.textBoxID.Focus();

                if (input.ShowDialog() == false) return;

                CmdUnit cmd = new CmdUnit();
                cmd.ID = input.textBoxID.Text;
                cmd.Name = input.textBoxName.Text;
                cmd.Cmd = input.textBoxCmd.Text;
                cmd.Encrypt = input.checkBoxEncrypt.IsChecked == true ? true : false;
                cmd.ContentMode = (ServiceRequestContentMode)Enum.Parse(typeof(ServiceRequestContentMode), input.comboBoxContentMode.SelectedItem.ToString()); 
                cmdTable.Add(cmd);
            }
        }

        private void MenuItem_CmdDel_Click(object sender, RoutedEventArgs e)
        {
            List<CmdUnit> tmp = new List<CmdUnit>();

            foreach (CmdUnit item in lstViewCmd.SelectedItems)
                tmp.Add(item);

            foreach (var item in tmp)
                cmdTable.Remove(item);
        }

        private void MenuItem_CmdSave_Click(object sender, RoutedEventArgs e)
        {
            XmlDocument doc = new XmlDocument();
            XmlNode config;

            if (File.Exists(Core.CONF_PATH)) {
                doc.Load(Core.CONF_PATH);
                config = doc.SelectSingleNode("/configuration/commands");
            } else {
                doc.AppendChild(doc.CreateXmlDeclaration("1.0", "utf-8", ""));
                XmlElement root = doc.CreateElement("configuration"); // 创建根节点album
                doc.AppendChild(root);
                config = doc.CreateElement("commands"); // 创建根节点album
                root.AppendChild(config);
            }

            config.RemoveAll();
            foreach (var item in cmdTable) {
                XmlElement cmd = doc.CreateElement("cmditem");
                cmd.SetAttribute("id", item.ID);
                cmd.SetAttribute("name", item.Name);
                cmd.SetAttribute("encrypt", item.Encrypt.ToString());
                cmd.SetAttribute("content-mode", item.ContentMode.ToString());
                cmd.SetAttribute("content", item.Cmd);
                config.AppendChild(cmd);
            }

            doc.Save(Core.CONF_PATH);
        }

        //private void MenuItem_CmdOpen_Click(object sender, RoutedEventArgs e)
        //{
        //    System.Diagnostics.Process.Start("Explorer.exe", MainWindow.BASE_DIR);
        //}

        private void MenuItem_MsgClear_Click(object sender, RoutedEventArgs e)
        {
            txtBoxMsg.Text = "";
        }
    }
}
