using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace mnn.util {
    public class AtCmd {
        public delegate void AtCmdDelegate(object[] args);

        public string name;
        public AtCmdDelegate func;
        public List<object[]> args_table;

        public AtCmd(string name, AtCmdDelegate func)
        {
            this.name = name;
            this.func = func;
            this.args_table = new List<object[]>();
        }
    }

    public class AtCmdCtl {
        private List<AtCmd> atcmd_table;
        class UserCmd {
            public string name;
            public object[] args;
        }
        private List<UserCmd> user_cmd_table;
        private Thread thread;

        public AtCmdCtl()
        {
            atcmd_table = new List<AtCmd>();
            user_cmd_table = new List<UserCmd>();
        }

        // Methods =============================================================================

        public void Perform(int next)
        {
            ThreadCheck(true);

            // ** none
            if (user_cmd_table.Count == 0) {
                System.Threading.Thread.Sleep(next);
                return;
            }

            // ** read
            lock (user_cmd_table) {
                foreach (var item in user_cmd_table) {
                    foreach (var atcmd in atcmd_table) {
                        if (atcmd.name.Equals(item.name)) {
                            atcmd.args_table.Add(item.args);
                            break;
                        }
                    }
                }
                user_cmd_table.Clear();
            }

            // ** func
            foreach (var item in atcmd_table) {
                foreach (var args in item.args_table.ToArray()) {
                    item.func(args);
                    item.args_table.Remove(args);
                }
            }
        }

        public void Register(string name, AtCmd.AtCmdDelegate func)
        {
            ThreadCheck(false);

            // Check if same AtCmd is already added
            foreach (var item in atcmd_table) {
                if (item.name.Equals(name))
                    return;
            }

            atcmd_table.Add(new AtCmd(name, func));
        }

        public void Deregister(string name)
        {
            ThreadCheck(false);

            foreach (var item in atcmd_table) {
                if (item.name.Equals(name))
                    atcmd_table.Remove(item);
            }
        }

        public void AppendCommand(string name, object[] args)
        {
            lock (user_cmd_table) {
                user_cmd_table.Add(new UserCmd() { name = name, args = args });
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
