using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace mnn.net {
    public class Controller {
        public delegate void ControllerDelegate(SockRequest request, SockResponse response);

        public string name;
        public ControllerDelegate func;
        public int key;

        public Controller(string name, ControllerDelegate func, int key)
        {
            this.name = name;
            this.func = func;
            this.key = key;
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

        public virtual void handle(SockRequest request, SockResponse response)
        {
            try {
                foreach (var item in ctler_table) {
                    if ((item.Value.key & 0xff) == request.data[0] && (item.Value.key >> 8 & 0xff) == request.data[1]
                        && request.data[2] + (request.data[3] << 8) == request.data.Length) {
                        item.Value.func(request, response);
                        return;
                    }
                }

                if (default_controller != null)
                    default_controller.func(request, response);
            } catch (Exception) { }
        }

        public void Register(string name, Controller.ControllerDelegate func, int key)
        {
            if (!ctler_table.ContainsKey(name))
                ctler_table.Add(name, new Controller(name, func, key));
        }

        public void RegisterDefaultController(string name, Controller.ControllerDelegate func, int key = 0)
        {
            default_controller = new Controller(name, func, key);
        }

        public void Deregister(string name)
        {
            if (ctler_table.ContainsKey(name))
                ctler_table.Remove(name);
        }
    }
}
