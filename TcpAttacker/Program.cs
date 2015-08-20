using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Xml;

namespace TcpAttacker
{
    class Program
    {
        static IPAddress ipAddress;
        static Random random = new Random();
        static List<TcpAttacker> attackerTable = new List<TcpAttacker>();

        static void Main(string[] args)
        {
            // 读取配置文件
            XmlDocument xdoc = new XmlDocument();
            xdoc.Load("config\\message.xml");

            XmlNode ipNode = xdoc.SelectSingleNode("/configuration/ipaddress");
            ipAddress = IPAddress.Parse(ipNode.InnerText);

            XmlNodeList nodes = xdoc.GetElementsByTagName("station");
            foreach (XmlNode item in nodes) {
                int Count = Convert.ToInt32(item.Attributes["count"].InnerText);

                for (int i = 0; i < Count; i++) {
                    TcpAttacker stationInfo = new TcpAttacker();
                    stationInfo.Rand = new Random(random.Next(0,10000));
                    stationInfo.Name = item.Attributes["name"].Value;
                    stationInfo.EP = new IPEndPoint(ipAddress, Convert.ToInt32(item.Attributes["port"].Value));
                    stationInfo.Interval = 1000 * random.Next(1, Convert.ToInt32(item.Attributes["max_interval"].Value));
                    XmlNodeList msg = item.SelectNodes("message");
                    foreach (XmlNode node in msg) {
                        stationInfo.MessageTable.Add(node.Attributes["content"].Value);
                    }
                    attackerTable.Add(stationInfo);
                }
            }

            Console.WriteLine("Attacker Command: start | stop | quit");

            // 循环读取命令
            while (true) {
                string cmd = Console.ReadLine().ToLower();

                if (cmd == "quit" || cmd == "exit") {
                    Console.WriteLine("Attacker is quiting...");
                    break;
                }
                else if (cmd == "stop") {
                    Console.WriteLine("Attacker has stopped.");
                    foreach (var item in attackerTable)
                        item.Stop();
                }
                else if (cmd == "start") {
                    Console.WriteLine("Attacker has started.");
                    foreach (var item in attackerTable)
                        item.Start();
                }
            }
        }
    }
}
