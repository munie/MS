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
            DataContext = new { SockTable = SockTable };
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
            }
            catch (Exception ex) {
                Mnn.MnnUtil.Logger.WriteException(ex);
                System.Windows.MessageBox.Show("配置文件读取错误" );
            }
            /// ** Initialize End ====================================================
        }

        private void sessmgr_sess_parse(object sender, SockSess sess)
        {
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
            SockUnit sock = TreeSock.SelectedItem as SockUnit;

            if (sock != null && sock.Type == SockUnit.TypeListen &&
                sock.Comment.Contains(SockUnit.StateClosed) && sessmgr.AddListenSession(sock.EP))
                sock.Comment = sock.Comment.Replace(SockUnit.StateClosed, SockUnit.StateListened);
        }

        private void MenuItem_Connect_Click(object sender, RoutedEventArgs e)
        {
            SockUnit sock = TreeSock.SelectedItem as SockUnit;

            if (sock != null && sock.Type == SockUnit.TypeConnect &&
                sock.Comment.Contains(SockUnit.StateClosed) && sessmgr.AddConnectSession(sock.EP))
                sock.Comment = sock.Comment.Replace(SockUnit.StateClosed, SockUnit.StateConnected);
        }

        private void MenuItem_Close_Click(object sender, RoutedEventArgs e)
        {
            SockUnit sock = TreeSock.SelectedItem as SockUnit;

            if (sock != null)
                sessmgr.RemoveSession(sock.EP);

            if (sock.Type == SockUnit.TypeListen)
                sock.Comment = sock.Comment.Replace(SockUnit.StateListened, SockUnit.StateClosed);
            else if (sock.Type == SockUnit.TypeConnect)
                sock.Comment = sock.Comment.Replace(SockUnit.StateConnected, SockUnit.StateClosed);
        }

        // other methods ============================================================================

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
    }
}
