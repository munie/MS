using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace mnn.util
{
    public class AtCmd
    {
        public delegate void AtCmdDelegate(object arg);

        public string name;
        public AtCmdDelegate func;
        public List<object> args;

        public AtCmd(string name, AtCmdDelegate func)
        {
            this.name = name;
            this.func = func;
            this.args = new List<object>();
        }
    }

    public class AtCmdCenter
    {
        private List<AtCmd> atcmd_table;
        private int current_atcmd_count;

        public AtCmdCenter()
        {
            atcmd_table = new List<AtCmd>();
            current_atcmd_count = 0;
        }

        public void Perform(int next)
        {
            if (current_atcmd_count == 0) {
                System.Threading.Thread.Sleep(next);
                return;
            }

            lock (atcmd_table) {
                foreach (var item in atcmd_table) {
                    foreach (var arg in item.args.ToArray()) {
                        item.func(arg);
                        item.args.Remove(arg);
                        current_atcmd_count--;
                    }
                }
            }
        }

        public void Add(string name, AtCmd.AtCmdDelegate func)
        {
            lock (atcmd_table) {
                atcmd_table.Add(new AtCmd(name, func));
            }
        }

        public void Del(string name)
        {
            lock (atcmd_table) {
                foreach (var item in atcmd_table) {
                    if (item.name.Equals(name))
                        atcmd_table.Remove(item);
                }
            }
        }

        public void AppendCommand(string name, object arg)
        {
            lock (atcmd_table) {
                foreach (var item in atcmd_table) {
                    if (item.name.Equals(name)) {
                        item.args.Add(arg);
                        current_atcmd_count++;
                    }
                }
            }
        }
    }
}
