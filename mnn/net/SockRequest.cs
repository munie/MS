using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace mnn.net {
    public enum SockRequestType {
        binary = 0x0C00,// default
        plain = 0x0C01, // text/plain
        url = 0x0C02,   // text/url
        none = 0x0CFF,  // unknown
    }

    [Serializable]
    public class SockRequest {
        public IPEndPoint lep { get; set; }
        public IPEndPoint rep { get; set; }
        public SockRequestType type { get; set; }
        public int length { get; set; }
        public byte[] data { get; set; }

        public bool CheckType(byte[] raw)
        {
            int tmp = (raw[0] + (raw[1] << 8));
            if (!Enum.IsDefined(typeof(SockRequestType), tmp))
                return false;
            else if ((SockRequestType)tmp == SockRequestType.none)
                return false;
            else
                return true;
        }

        public bool CheckLength(byte[] raw)
        {
            if (raw.Length < raw[2] + (raw[3] << 8))
                return false;
            else
                return true;
        }

        public int ParseRawData(byte[] raw)
        {
            int identity = raw[0] + (raw[1] << 8);
            int total_len = raw[2] + (raw[3] << 8);

            this.type = (SockRequestType)identity;
            this.length = total_len;
            this.data = raw.Take(total_len).Skip(4).ToArray();
            return total_len;
        }
    }
}
