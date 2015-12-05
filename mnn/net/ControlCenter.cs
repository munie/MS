using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Net;
using mnn.util;

namespace mnn.net {
    public abstract class ControlCenter {
        public ControlCenter()
        {
            // init sesscer
            sessctl = new SessCtl();
            sessctl.sess_parse += new SessCtl.SessParseDelegate(sess_parse);
            sessctl.sess_create += new SessCtl.SessCreateDelegate(sess_create);
            sessctl.sess_delete += new SessCtl.SessDeleteDelegate(sess_delete);

            // init dispatcher
            dispatcher = new Dispatcher();
        }

        // session control
        protected SessCtl sessctl;
        // other request control
        protected Dispatcher dispatcher;

        // Session Event ==================================================================================

        protected virtual void sess_parse(object sender, SockSess sess)
        {
            PackRequest request = new PackRequest();
            PackResponse response = new PackResponse();
            dispatcher.handle(request, response);
            sess.sock.Send(new byte[] { 31, 32});
        }

        protected abstract void sess_create(object sender, SockSess sess);

        protected abstract void sess_delete(object sender, SockSess sess);
    }
}
