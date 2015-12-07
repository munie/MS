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

        public int ParseRawData(byte[] raw)
        {
            int identity = raw[0] + (raw[1] << 8);
            int total_len = raw[2] + (raw[3] << 8);
            if (!Enum.IsDefined(typeof(SockRequestType), identity))
                return -1;

            if (length > raw.Length)
                return -1;

            this.type = (SockRequestType)identity;
            this.length = total_len;
            this.data = raw.Take(length).Skip(4).ToArray();
            return length;
        }
    }
}
