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
using System.Xml;
using System.IO;
using System.Net;
using System.Threading;
using mnn.net;
using mnn.util;

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
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Hide();
            this.init();
            this.config();

            // New thread for running socket. From now on, we can't call motheds of sesscer & cmdcer directly in other thread
            Thread thread = new Thread(() =>
            {
                while (true) {
                    cmdcer.Perform();
                    sesscer.Perform(1000);
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        // Fields & init ==============================================================================

        public static readonly string BASE_DIR = System.AppDomain.CurrentDomain.BaseDirectory;
        public static readonly string CONF_NAME = "SockMaster.xml";
        public static readonly string CONF_PATH = BASE_DIR + CONF_NAME;
        public static readonly string SOCK_OPEN = "sock_open";
        public static readonly string SOCK_CLOSE = "sock_close";
        public static readonly string SOCK_SEND = "sock_send";

        private SessCenter sesscer;
        public AtCmdCenter cmdcer;
        public ObservableCollection<SockUnit> SockTable { get; set; }
        public ObservableCollection<CmdUnit> CmdTable { get; set; }
        public SockMasterWindow SockWindow { get; set; }

        private void init()
        {
            // init sesscer
            sesscer = new SessCenter();
            sesscer.sess_parse += new SessCenter.SessParseDelegate(sesscer_sess_parse);
            sesscer.sess_create += new SessCenter.SessCreateDelegate(sesscer_sess_create);
            sesscer.sess_delete += new SessCenter.SessDeleteDelegate(sesscer_sess_delete);

            // init cmdcer
            cmdcer = new AtCmdCenter();
            cmdcer.Add(SOCK_OPEN, cmdcer_sock_open);
            cmdcer.Add(SOCK_CLOSE, cmdcer_sock_close);
            cmdcer.Add(SOCK_SEND, cmdcer_sock_send);

            // init SockTable & CmdTable
            SockTable = new ObservableCollection<SockUnit>();
            CmdTable = new ObservableCollection<CmdUnit>();

            // init SockWindow
            SockWindow = new SockMasterWindow();
            SockWindow.Owner = this;
            SockWindow.DataContext = new { SockTable = SockTable, CmdTable = CmdTable };
            SockWindow.Show();
        }

        private void config()
        {
            if (File.Exists(BASE_DIR + CONF_NAME) == false) {
                System.Windows.MessageBox.Show("未找到配置文件");
                return;
            }

            /// ** Initialize Start ====================================================
            try {
                XmlDocument doc = new XmlDocument();
                doc.Load(BASE_DIR + CONF_NAME);

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

                    if (sock.Autorun) {
                        sock.State = SockState.Opening;
                        cmdcer.AppendCommand(SOCK_OPEN, sock);
                    }
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
            } catch (Exception) {
                System.Windows.MessageBox.Show("配置文件读取错误");
            }
            /// ** Initialize End ====================================================
        }

        // Perform ==================================================================================

        private void sesscer_sess_parse(object sender, SockSess sess)
        {
            byte[] data = sess.rdata.Take(sess.rdata_size).ToArray();
            sess.rdata_size = 0;

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (SockWindow.txtBoxMsg.Text.Length >= 20 * 1024)
                    SockWindow.txtBoxMsg.Clear();

                SockWindow.txtBoxMsg.AppendText(DateTime.Now + " (" + sess.rep.ToString() + " => " + sess.lep.ToString() + ")\n");
                SockWindow.txtBoxMsg.AppendText(SockConvert.ParseBytesToString(data) + "\n\n");
                SockWindow.txtBoxMsg.ScrollToEnd();
            }));
        }

        private void sesscer_sess_create(object sender, SockSess sess)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (sess.type == SockType.accept) {
                    SockWindow.currentAcceptCount.Text = (Convert.ToInt32(SockWindow.currentAcceptCount.Text) + 1).ToString();
                    SockWindow.historyAcceptOpenCount.Text = (Convert.ToInt32(SockWindow.historyAcceptOpenCount.Text) + 1).ToString();

                    var subset = from s in SockTable
                                 where s.Type == SockType.listen && s.EP.Port == sess.lep.Port
                                 select s;
                    foreach (var item in subset) {
                        item.Childs.Add(new SockUnit()
                        {
                            ID = "-",
                            Name = "accept",
                            Type = sess.type,
                            Sess = sess,
                            EP = sess.rep,
                            State = SockState.Opened,
                        });
                        break;
                    }
                }
            }));
        }

        private void sesscer_sess_delete(object sender, SockSess sess)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (sess.type == SockType.accept) {
                    SockWindow.currentAcceptCount.Text = (Convert.ToInt32(SockWindow.currentAcceptCount.Text) - 1).ToString();
                    SockWindow.historyAcceptCloseCount.Text = (Convert.ToInt32(SockWindow.historyAcceptCloseCount.Text) + 1).ToString();

                    foreach (var item in SockTable) {
                        if (item.Childs.Count == 0) continue;

                        foreach (var child in item.Childs) {
                            if (child.Sess == sess) {
                                item.Childs.Remove(child);
                                return;
                            }
                        }
                    }
                } else if (sess.type == SockType.connect) {
                    foreach (var item in SockTable) {
                        if (item.Sess == sess) {
                            item.State = SockState.Closed;
                            break;
                        }
                    }
                }
            }));
        }

        private void cmdcer_sock_open(object arg)
        {
            SockUnit sock = arg as SockUnit;
            if (sock == null || sock.State == SockState.Opened) return;

            SockSess sess = null;
            if (sock.Type == SockType.listen) {
                sess = sesscer.MakeListen(sock.EP);
            } else if (sock.Type == SockType.connect) {
                sess = sesscer.AddConnect(sock.EP);
            }

            if (sess != null) {
                sock.Sess = sess;
                sock.State = SockState.Opened;
            } else {
                sock.State = SockState.Closed;
            }
        }

        private void cmdcer_sock_close(object arg)
        {
            SockUnit sock = arg as SockUnit;
            if (sock == null || sock.State == SockState.Closed) return;

            sesscer.DelSession(sock.Sess);
            sock.State = SockState.Closed;
        }

        private void cmdcer_sock_send(object arg)
        {
            SockUnit sock = arg as SockUnit;
            if (sock == null || sock.State != SockState.Opened) return;

            if (sock.SendBuff != null) {
                sesscer.SendSession(sock.Sess, sock.SendBuff);
                sock.SendBuff = null;
            }
        }
    }
}
