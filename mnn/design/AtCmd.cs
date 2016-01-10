using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace mnn.design {
    public delegate void AtCmdDelegate(object[] args);

    public class AtCmd {
        public string Name { get; private set; }
        public object[] Args { get; private set; }
        public DateTime Timeout { get; /*private*/ set; }
        public bool Repeat { get; private set; }
        public int Tick { get; private set; }

        //public AtCmd() { }
        public AtCmd(string name, object[] args, DateTime timeout, bool repeat = false, int tick = 1)
        {
            Name = name;
            Args = args;
            Timeout = timeout;
            Repeat = repeat;
            Tick = tick >= 1 ? tick : 1;
        }
    }

    public class AtCmdCtl {
        private Dictionary<string, AtCmdDelegate> func_table;
        private List<AtCmd> cmd_table;
        private Thread thread;

        public AtCmdCtl()
        {
            func_table = new Dictionary<string, AtCmdDelegate>();
            cmd_table = new List<AtCmd>();
        }

        // Methods =============================================================================

        public void Perform()
        {
            ThreadCheck(true);

            lock (cmd_table) {
                foreach (var item in cmd_table.ToArray()) {
                    // break the loop if encounter the first one which has not timeout
                    if (item.Timeout > DateTime.Now)
                        break;
                    // execute command
                    else {
                        func_table[item.Name].Invoke(item.Args);
                        cmd_table.Remove(item);
                        if (item.Repeat) {
                            do {
                                item.Timeout += new TimeSpan(item.Tick);
                            } while (item.Timeout > DateTime.Now);
                            AddCommand(item);
                        }
                    }
                }
            }
        }

        public void Register(string name, AtCmdDelegate func)
        {
            ThreadCheck(false);

            if (func_table.ContainsKey(name)) return;
            func_table.Add(name, func);
        }

        public void Deregister(string name)
        {
            ThreadCheck(false);

            if (!func_table.ContainsKey(name)) return;
            func_table.Remove(name);
        }

        public void AddCommand(AtCmd cmd)
        {
            lock (cmd_table) {
                // do nothing if cmd's timeout is earlier than DateTime.Now
                if (cmd.Timeout < DateTime.Now)
                    return;
                // insert at first if cmd's timeout is the earliest
                else if (cmd.Timeout < cmd_table.First().Timeout)
                    cmd_table.Insert(0, cmd);
                // insert behind the one whose timeout is earlier than cmd's
                else {
                    for (int i = cmd_table.Count - 1; i >= 0; i--) {
                        if (cmd_table[i].Timeout <= cmd.Timeout) {
                            cmd_table.Insert(i + 1, cmd);
                            break;
                        }
                    }
                }
            }
        }

        public void DelCommand(AtCmd cmd)
        {
            lock (cmd_table) {
                cmd_table.Remove(cmd);    
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
