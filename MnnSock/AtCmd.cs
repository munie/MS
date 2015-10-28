using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mnn.MnnSock
{
    public delegate void AtCmdDelegate(object arg);

    public class AtCmd
    {
        public string name;
        public AtCmdDelegate func;
        public List<object> args = new List<object>();
    }

    public class AtCmdCenter
    {
        List<AtCmd> atcmd_table;

        public AtCmdCenter()
        {
            atcmd_table = new List<AtCmd>();
        }

        public void Perform()
        {
            lock (atcmd_table) {
                foreach (var item in atcmd_table) {
                    foreach (var arg in item.args.ToArray()) {
                        item.func(arg);
                        item.args.Remove(arg);
                    }
                }
            }
        }

        public void Add(AtCmd cmd)
        {
            atcmd_table.Add(cmd);
        }

        public void Add(string name, AtCmdDelegate func)
        {
            atcmd_table.Add(new AtCmd() { name = name, func = func });
        }

        public void Del(AtCmd cmd)
        {
            atcmd_table.Remove(cmd);
        }

        public void AppendCommand(string name, object arg)
        {
            lock (atcmd_table) {
                foreach (var item in atcmd_table) {
                    if (item.name.Equals(name))
                        item.args.Add(arg);
                }
            }
        }
    }
}
