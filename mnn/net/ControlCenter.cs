using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace mnn.net {
    public class ControlCenter {
        // session control
        protected SessCtl sessctl;
        // other request control
        protected Dispatcher dispatcher;

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

        // Session Event ==================================================================================

        protected virtual void sess_parse(object sender, SockSess sess)
        {
            // get data
            byte[] data = sess.rdata.Take(sess.rdata_size).ToArray();

            // init request & response
            SockRequest request = new SockRequest() { lep = sess.lep, rep = sess.rep };
            SockResponse response = new SockResponse() { data = null };
            int num_read = request.ParseRawData(data);

            // request access
            if (num_read != -1) {
                sess.RfifoSkip(num_read);
                dispatcher.handle(request, response);
                if (response.data != null && response.data.Length != 0)
                    sessctl.SendSession(sess, response.data);
            // request error
            } else {
                sess.RfifoSkip(sess.rdata_size);
                // compatible with old protocol
                request.type = SockRequestType.unknown;
                request.length = -1;
                request.data = data;
                dispatcher.handle(request, response);
                if (response.data != null && response.data.Length != 0)
                    sessctl.SendSession(sess, response.data);
            }
        }

        protected virtual void sess_create(object sender, SockSess sess) { }

        protected virtual void sess_delete(object sender, SockSess sess) { }
    }
}
