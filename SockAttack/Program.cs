using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Xml;
using System.IO;

namespace SockAttack
{
    class Program
    {
        static void Main(string[] args)
        {
            Encoding coding;
            IPAddress ipAddress;
            Random random = new Random();
            List<SockAttack> attackerTable = new List<SockAttack>();


            // 读取配置文件
            if (File.Exists(System.AppDomain.CurrentDomain.BaseDirectory + "\\config.xml") == false) {
                Console.WriteLine("Can't find message.xml at directory config...");
                Console.WriteLine("Application will be closed in 5 seconds.");
                System.Threading.Thread.Sleep(5000);
                return;
            }

            try {
                XmlDocument xdoc = new XmlDocument();
                xdoc.Load(System.AppDomain.CurrentDomain.BaseDirectory + "\\config.xml");

                XmlNode codingNode = xdoc.SelectSingleNode("/configuration/encoding");
                coding = Encoding.GetEncoding(codingNode.InnerText);

                XmlNode ipNode = xdoc.SelectSingleNode("/configuration/ipaddress");
                IPAddress[] ips;
                ips = Dns.GetHostAddresses(ipNode.InnerText);
                ipAddress = ips[0];
                //ipAddress = IPAddress.Parse(ipNode.InnerText);

                XmlNodeList nodes = xdoc.GetElementsByTagName("station");
                foreach (XmlNode item in nodes) {
                    int Count = Convert.ToInt32(item.Attributes["count"].InnerText);

                    for (int i = 0; i < Count; i++) {
                        SockAttack attacker = new SockAttack();
                        attacker.Coding = coding;
                        attacker.Rand = new Random(random.Next(0, 10000));
                        attacker.Name = item.Attributes["name"].Value;
                        attacker.Protocol = item.Attributes["protocol"].Value;
                        attacker.EP = new IPEndPoint(ipAddress, Convert.ToInt32(item.Attributes["port"].Value));
                        attacker.Interval = 1000 * random.Next(1, Convert.ToInt32(item.Attributes["max_interval"].Value));
                        XmlNodeList msg = item.SelectNodes("message");
                        foreach (XmlNode node in msg) {
                            attacker.MessageTable.Add(node.Attributes["content"].Value);
                        }
                        attackerTable.Add(attacker);
                    }
                }
            }
            catch (Exception ex) {
                Console.Write(ex.ToString());
                Console.WriteLine("");
                Console.WriteLine("Error: Reading message.xml Fail.");
                Console.WriteLine("Application will be closed in 5 seconds.");
                System.Threading.Thread.Sleep(5000);
                return;
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
