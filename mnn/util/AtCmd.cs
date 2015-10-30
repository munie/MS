using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

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
        private Dictionary<string, object> cmd_dic;
        private Thread thread;

        public AtCmdCenter()
        {
            atcmd_table = new List<AtCmd>();
            cmd_dic = new Dictionary<string, object>();
        }

        public void Perform(int next)
        {
            ThreadCheck(true);

            // ** none
            if (cmd_dic.Count == 0) {
                System.Threading.Thread.Sleep(next);
                return;
            }

            // ** read
            lock (cmd_dic) {
                foreach (var item in cmd_dic) {
                    foreach (var atcmd in atcmd_table) {
                        if (atcmd.name.Equals(item.Key)) {
                            atcmd.args.Add(item.Value);
                            break;
                        }
                    }
                }
                cmd_dic.Clear();
            }

            // ** func
            foreach (var item in atcmd_table) {
                foreach (var arg in item.args.ToArray()) {
                    item.func(arg);
                    item.args.Remove(arg);
                }
            }
        }

        public void Add(string name, AtCmd.AtCmdDelegate func)
        {
            ThreadCheck(false);

            atcmd_table.Add(new AtCmd(name, func));
        }

        public void Del(string name)
        {
            ThreadCheck(false);

            foreach (var item in atcmd_table) {
                if (item.name.Equals(name))
                    atcmd_table.Remove(item);
            }
        }

        public void AppendCommand(string name, object arg)
        {
            lock (cmd_dic) {
                cmd_dic.Add(name, arg);
            }
        }

        // Self Methods ========================================================================

        private void ThreadCheck(bool isSockThread)
        {
            if (isSockThread && thread == null)
                thread = Thread.CurrentThread;

            if (thread != null && thread != Thread.CurrentThread)
                throw new ApplicationException("Only socket thread can call this function!");
        }
    }
}
