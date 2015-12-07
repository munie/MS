using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace mnn.net {
    public class Controller {
        public delegate void ControllerDelegate(SockRequest request, SockResponse response);

        public byte[] keyname;
        public ControllerDelegate func;

        public Controller(byte[] key, ControllerDelegate func)
        {
            this.keyname = key;
            this.func = func;
        }
    }

    public class Dispatcher {
        private Dictionary<string, Controller> ctler_table;
        private Controller default_controller;

        public Dispatcher()
        {
            ctler_table = new Dictionary<string, Controller>();
            default_controller = null;
        }

        // Methods =============================================================================

        private bool byteArrayCmp(byte[] first, byte[] second)
        {
            if (first.Length != second.Length)
                return false;

            for (int i = 0; i < first.Length; i++) {
                if (first[i] != second[i])
                    return false;
            }

            return true;
        }

        public virtual void handle(SockRequest request, SockResponse response)
        {
            try {
                if (request.type == SockRequestType.unknown) {
                    default_controller.func(request, response);
                    return;
                }

                foreach (var item in ctler_table) {
                    if (byteArrayCmp(item.Value.keyname, request.data.Take(item.Value.keyname.Length).ToArray())) {
                        item.Value.func(request, response);
                        break;
                    }
                }
            } catch (Exception) { }
        }

        public void Register(string name, Controller.ControllerDelegate func, byte[] key)
        {
            if (!ctler_table.ContainsKey(name))
                ctler_table.Add(name, new Controller(key, func));
        }

        public void RegisterDefaultController(string name, Controller.ControllerDelegate func)
        {
            default_controller = new Controller(new byte[] {0}, func);
        }

        public void Deregister(string name)
        {
            if (ctler_table.ContainsKey(name))
                ctler_table.Remove(name);
        }
    }
}
