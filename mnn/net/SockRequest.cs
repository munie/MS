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
        public IPEndPoint lep { get; private set; }
        public IPEndPoint rep { get; private set; }
        public bool has_header { get; private set; }
        public bool encrypt { get; private set; }
        public /*short*/ SockRequestContentMode content_mode { get; private set; }
        public short length { get; private set; }
        public byte[] data { get; private set; }

        public SockRequest() { }
        public SockRequest(IPEndPoint lep, IPEndPoint rep, byte[] raw)
        {
            this.lep = lep;
            this.rep = rep;
            this.has_header = CheckHeader(raw);
            this.encrypt = (raw[1] & 0x80) == 0x80 ? true : false;

            if (has_header) {
                this.content_mode = (SockRequestContentMode)(raw[0] + (raw[1] << 8));
                this.length = (short)(raw[2] + (raw[3] << 8));
                this.data = raw.Take(this.length).Skip(4).ToArray();
            } else {
                this.content_mode = SockRequestContentMode.binary;
                this.length = (short)raw.Length;
                this.data = raw;
            }
        }

        public void Decrypt()
        {
            if (!this.encrypt) return;

            try {
                byte[] result = Convert.FromBase64String(Encoding.UTF8.GetString(this.data));
                result = EncryptSym.AESDecrypt(result);
                if (result != null) SetData(result);
            } catch (Exception) { }
        }

        public void SetData(byte[] data)
        {
            if (data == null) return;

            this.length = (short)data.Length;
            this.data = data;
        }

        private bool CheckHeader(byte[] raw)
        {
            int tmp = (raw[0] + (raw[1] << 8)) & 0x7fff;
            if (!Enum.IsDefined(typeof(SockRequestContentMode), tmp))
                return false;
            else if (raw.Length < raw[2] + (raw[3] << 8))
                return false;
            else
                return true;
        }
    }
}
