using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace mnn.net {
    public enum SockRequestType {
        sock = 0x0C00,
        http = 0x0C01,
        unknown = 0x0CFF,
    }

    public class SockRequest {
        public IPEndPoint lep { get; set; }
        public IPEndPoint rep { get; set; }
        public SockRequestType type { get; set; }
        public int length { get; set; }
        public byte[] data { get; set; }

        public bool CheckType(byte[] raw)
        {
            if (!Enum.IsDefined(typeof(SockRequestType), raw[0] + (raw[1] << 8)))
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
