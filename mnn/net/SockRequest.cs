using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace mnn.net {
    // none/unknow => 0xFF
    public enum SockRequestContentMode {
        binary = 0x0C00,// default
        text = 0x0C01, // text/plain
        url = 0x0C02,   // text/url
    }

    [Serializable]
    public class SockRequest {
        public IPEndPoint lep { get; set; }
        public IPEndPoint rep { get; set; }
        public /*short*/ SockRequestContentMode type { get; set; }
        public short length { get; set; }
        public byte[] data { get; set; }

        public bool CheckHeader(byte[] raw)
        {
            int tmp = (raw[0] + (raw[1] << 8));
            if (!Enum.IsDefined(typeof(SockRequestContentMode), tmp))
                return false;
            else if (raw.Length < raw[2] + (raw[3] << 8))
                return false;
            else
                return true;
        }

        public int ParseRawData(byte[] raw)
        {
            short identity = (short)(raw[0] + (raw[1] << 8));
            short total_len = (short)(raw[2] + (raw[3] << 8));

            this.type = (SockRequestContentMode)identity;
            this.length = total_len;
            this.data = raw.Take(total_len).Skip(4).ToArray();
            return total_len;
        }
    }
}
