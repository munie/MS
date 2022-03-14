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

namespace SockMaster
{
    class Conference
    {
        public Conference(string name)
        {
            Name = name;
        }

        public string Name { get; set; }
        public List<Team> Teams { get; set; }
    }

    class Team
    {
        public Team(string name)
        {
            Name = name;
        }

        public string Name { get; set; }
    }

    /// <summary>
    /// TrialWindow.xaml 的交互逻辑
    /// </summary>
    public partial class TrialWindow : Window
    {
        public TrialWindow()
        {
            InitializeComponent();

            //init();
            MainWindow mgrwindow = new MainWindow();
            //mgrwindow.Owner = this;
            mgrwindow.Show();
            this.Hide();
        }

        private void init()
        {
            //List<sess> mgr_table = new List<sess>();
            //mgr_table.Add(new sess() { id = "1", name = "cmd", type = 0 });
            //mgr_table.Add(new sess() { id = "1", name = "zls", type = 0 });
            //mgr_table.Add(new sess() { id = "1", name = "sk", type = 0 });

            //treeViewSock.ItemsSource = mgr_table.ToArray();

            var western = new Conference("Western")
            {
                Teams = new List<Team>()
                {
                    new Team("Club Deportivo Chivas USA"),
                    new Team("Colorado Rapids"),
                    new Team("FC Dallas"),
                    new Team("Houston Dynamo"),
                    new Team("Los Angeles Galaxy"),
                    new Team("Real Salt Lake"),
                    new Team("San Jose Earthquakes"),
                    new Team("Seattle Sounders FC"),
                    new Team("Portland 2011"),
                    new Team("Vancouver 2011")
                }
            };
            var eastern = new Conference("Eastern")
            {
                Teams =  new List<Team>()
                {
                    new Team("Chicago Fire"),
                    new Team("Columbus Crew"),
                    new Team("D.C. United"),
                    new Team("Kansas City Wizards"),
                    new Team("New York Red Bulls"),
                    new Team("New England Revolution"),
                    new Team("Toronto FC"),
                    new Team("Philadelphia Union 2010")
                }
            };
            var league = new Collection<Conference>() { western, eastern };
            DataContext = new
            {
                WesternConference = western,
                EasternConference = eastern,
                League = league
            };
        }
    }
}
