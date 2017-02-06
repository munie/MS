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
using Newtonsoft.Json;
using mnn.net;
using mnn.service;
using mnn.misc.glue;
using SockMaster.Backend;

namespace SockMaster
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly string BASE_DIR = System.AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string CONF_NAME = "SockMaster.xml";
        private static readonly string CONF_PATH = BASE_DIR + CONF_NAME;

        private UIData uidata;
        private BaseLayer core;

        public MainWindow()
        {
            InitializeComponent();

            InitailizeWindowName();
            InitailizeStatusBar();

            Initailize();
            Config();
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
            timer.Tick += new EventHandler((s, ea) => {
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

        private void Initailize()
        {
            // uidata
            uidata = new UIData();
            DataContext = new { SockTable = uidata.SockUnitGroup, CmdTable = uidata.CmdTable, DataUI = uidata };
            uidata.MsgBox = this.txtBoxMsg;
            this.currentAcceptCount.SetBinding(TextBlock.TextProperty, new Binding("DataUI.CurrentAcceptCount"));
            this.historyAcceptOpenCount.SetBinding(TextBlock.TextProperty, new Binding("DataUI.HistoryAcceptOpenCount"));
            this.historyAcceptCloseCount.SetBinding(TextBlock.TextProperty, new Binding("DataUI.HistoryAcceptCloseCount"));

            // init core
            core = new BaseLayer();
            core.servctl.AddServiceDone("service.sesslisten", OnSessListenDone);
            core.servctl.AddServiceDone("service.sessconnect", OnSessConnectDone);
            core.servctl.ReplaceDefaultService(DefaultService, null);
            core.sess_listen_event += new BaseLayer.SockSessOpenDelegate(OnSessOpen);
            core.sess_connect_event += new BaseLayer.SockSessOpenDelegate(OnSessOpen);
            core.sess_accept_event += new BaseLayer.SockSessOpenDelegate(OnSessOpen);
            core.sess_close_event += new BaseLayer.SockSessCloseDelegate(OnSessClose);
            core.Run();
        }

        public void Config()
        {
            if (File.Exists(BASE_DIR + CONF_NAME) == false) {
                System.Windows.MessageBox.Show(CONF_NAME + ": can't find it.");
                return;
            }

            try {
                XmlDocument doc = new XmlDocument();
                doc.Load(BASE_DIR + CONF_NAME);

                // cmdtable
                foreach (XmlNode item in doc.SelectNodes("/configuration/commands/cmditem")) {
                    CmdUnit cmd = new CmdUnit();
                    cmd.ID = item.Attributes["id"].Value;
                    cmd.Name = item.Attributes["name"].Value;
                    cmd.Cmd = item.Attributes["content"].Value;
                    cmd.Encrypt = bool.Parse(item.Attributes["encrypt"].Value);
                    cmd.ContentMode = (ServiceRequestContentMode)Enum.Parse(typeof(ServiceRequestContentMode), item.Attributes["content-mode"].Value);
                    uidata.CmdTable.Add(cmd);
                }

                /// sockunit
                foreach (XmlNode item in doc.SelectNodes("/configuration/sockets/sockitem")) {
                    string[] str = item.Attributes["ep"].Value.Split(':');
                    IPEndPoint ep = new IPEndPoint(IPAddress.Parse(str[0]), int.Parse(str[1]));
                    SockType sockType = (SockType)Enum.Parse(typeof(SockType), item.Attributes["type"].Value);
                    SockUnit sockUnit = new SockUnit() {
                        ID = item.Attributes["id"].Value,
                        Name = item.Attributes["name"].Value,
                        Type = sockType,
                        Lep = sockType == SockType.listen ? ep : null,
                        Rep = sockType == SockType.connect ? ep : null,
                        State = SockState.Closed,
                        Autorun = bool.Parse(item.Attributes["autorun"].Value),
                    };
                    uidata.AddSockUnit(sockUnit);

                    if (sockUnit.Autorun) {
                        string id = "service.sesslisten";
                        if (sockType == SockType.connect)
                            id = "service.sessconnect";
                        object req = new {
                            id = id,
                            ip = ep.Address.ToString(),
                            port = ep.Port,
                        };
                        core.servctl.AddServiceRequest(ServiceRequest.Parse(JsonConvert.SerializeObject(req)));
                    }
                }
            } catch (Exception) {
                System.Windows.MessageBox.Show(CONF_NAME + ": syntax error.");
            }
        }

        // new baselayer event

        private void OnSessListenDone(ServiceRequest request, ServiceResponse response)
        {
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>((string)request.data);

            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), Convert.ToInt32(dc["port"]));

            if (response.errcode != 0)
                uidata.CloseSockUnit(SockType.listen, ep, ep);
        }

        private void OnSessConnectDone(ServiceRequest request, ServiceResponse response)
        {
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>((string)request.data);

            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), Convert.ToInt32(dc["port"]));

            if (response.errcode != 0)
                uidata.CloseSockUnit(SockType.connect, ep, ep);
        }

        private void OnSessOpen(object sender, SockSess sess)
        {
            if (sess is SockSessServer) {
                uidata.OpenSockUnit(SockType.listen, sess.lep, sess.rep, sess.id);
            } else if (sess is SockSessClient) {
                uidata.OpenSockUnit(SockType.connect, sess.lep, sess.rep, sess.id);
            } else if (sess is SockSessAccept) {
                SockUnit sockUnit = new SockUnit() {
                    ID = "at" + sess.rep.ToString(),
                    SESSID = sess.id,
                    Name = "accept",
                    Type = SockType.accept,
                    Lep = sess.lep,
                    Rep = sess.rep,
                    State = SockState.Opened,
                };
                uidata.AddSockUnit(sockUnit);
            }
        }

        private void OnSessClose(object sender, SockSess sess)
        {
            if (sess is SockSessServer)
                uidata.CloseSockUnit(SockType.listen, sess.lep, sess.rep);
            else if (sess is SockSessClient)
                uidata.CloseSockUnit(SockType.connect, sess.lep, sess.rep);
            else/* if (sess is SockSessAccept)*/
                uidata.DelSockUnit(SockType.accept, sess.lep, sess.rep);
        }

        // default service

        private void DefaultService(ServiceRequest request, ref ServiceResponse response)
        {
            string log = DateTime.Now + " (" + request.sessdata["rep"]
                + " => " + request.sessdata["lep"] + ")" + Environment.NewLine;

            if (request is BinaryRequest)
                log += SockConvert.ParseBytesToString(Encoding.UTF8.GetBytes((string)request.data));
            else if (request is JsonRequest)
                log += (string)request.data;
            else if (request is UnknownRequest)
                log += SockConvert.ParseBytesToString((byte[])request.data);

            log += Environment.NewLine + Environment.NewLine;
            uidata.Logger(log);

            response = null;
            //throw new Exception("bad request");
        }

        // Menu methods for TreeView =============================================================

        private void MenuItem_SockOpen_Click(object sender, RoutedEventArgs e)
        {
            SockUnit sock = treeSock.SelectedItem as SockUnit;
            if (sock == null || sock.State != SockState.Closed) return;

            sock.State = SockState.Opening;

            IPEndPoint ep = sock.Type == SockType.listen ? sock.Lep : sock.Rep;
            string id = "service.sesslisten";
            if (sock.Type == SockType.connect)
                id = "service.sessconnect";
            object req = new {
                id = id,
                ip = ep.Address.ToString(),
                port = ep.Port,
            };
            core.servctl.AddServiceRequest(ServiceRequest.Parse(JsonConvert.SerializeObject(req)));
        }

        private void MenuItem_SockClose_Click(object sender, RoutedEventArgs e)
        {
            SockUnit sock = treeSock.SelectedItem as SockUnit;
            if (sock == null || sock.State != SockState.Opened) return;

            sock.State = SockState.Closing;

            IPEndPoint ep = sock.Type != SockType.accept ? sock.Lep : sock.Rep;
            object req = new {
                id = "service.sessclose",
                sessid = sock.SESSID,
            };
            core.servctl.AddServiceRequest(ServiceRequest.Parse(JsonConvert.SerializeObject(req)));
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

            if (File.Exists(CONF_PATH)) {
                doc.Load(CONF_PATH);
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

            doc.Save(CONF_PATH);
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
                byte[] raw_data = SockConvert.ParseCmdstrToBytes(item.Cmd, '#');
                string str_data = Convert.ToBase64String(raw_data);
                //if (item.Encrypt)
                //    raw_data = Encoding.UTF8.GetBytes(Convert.ToBase64String(EncryptSym.AESEncrypt(raw_data)));

                // add internal header just for translating in SockMaster
                IPEndPoint ep = sock.Type != SockType.accept ? sock.Lep : sock.Rep;
                object req = new {
                    id = "service.sesssend",
                    sessid = sock.SESSID,
                    data = str_data,
                };
                core.servctl.AddServiceRequest(ServiceRequest.Parse(JsonConvert.SerializeObject(req)));
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
                uidata.CmdTable.Add(cmd);
            }
        }

        private void MenuItem_CmdDel_Click(object sender, RoutedEventArgs e)
        {
            List<CmdUnit> tmp = new List<CmdUnit>();

            foreach (CmdUnit item in lstViewCmd.SelectedItems)
                tmp.Add(item);

            foreach (var item in tmp)
                uidata.CmdTable.Remove(item);
        }

        private void MenuItem_CmdSave_Click(object sender, RoutedEventArgs e)
        {
            XmlDocument doc = new XmlDocument();
            XmlNode config;

            if (File.Exists(CONF_PATH)) {
                doc.Load(CONF_PATH);
                config = doc.SelectSingleNode("/configuration/commands");
            } else {
                doc.AppendChild(doc.CreateXmlDeclaration("1.0", "utf-8", ""));
                XmlElement root = doc.CreateElement("configuration"); // 创建根节点album
                doc.AppendChild(root);
                config = doc.CreateElement("commands"); // 创建根节点album
                root.AppendChild(config);
            }

            config.RemoveAll();
            foreach (var item in uidata.CmdTable) {
                XmlElement cmd = doc.CreateElement("cmditem");
                cmd.SetAttribute("id", item.ID);
                cmd.SetAttribute("name", item.Name);
                cmd.SetAttribute("encrypt", item.Encrypt.ToString());
                cmd.SetAttribute("content-mode", item.ContentMode.ToString());
                cmd.SetAttribute("content", item.Cmd);
                config.AppendChild(cmd);
            }

            doc.Save(CONF_PATH);
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
