using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace mnn.service {
    public enum ServiceRequestContentMode {
        none = 0x2421,   // $! => none
        binary = 0x2422,    // $" => default
        text = 0x2323,      // ## => text/plain
        uri = 0x2324,       // #$ => text/url
        json = 0x277b,      // {' => text/json
        json2 = 0x227b,      // {" => text/json
    }

    [Serializable]
    public abstract class ServiceRequest {
        public /*short*/ ServiceRequestContentMode content_mode { get; protected set; }
        public /*short*/ int packlen { get; protected set; }
        public byte[] raw_data { get; set; }
        public object user_data { get; set; }

        protected abstract void InnerParse(byte[] raw);

        // Methods ==================================================================

        private static ServiceRequestContentMode CheckContentMode(byte[] raw)
        {
            if (raw.Length < 2)
                return ServiceRequestContentMode.none;
            else {
                int tmp = raw[0] + (raw[1] << 8);

                if (!Enum.IsDefined(typeof(ServiceRequestContentMode), tmp))
                    tmp = (int)ServiceRequestContentMode.none;

                return (ServiceRequestContentMode)tmp;
            }
        }

        public static ServiceRequest Parse(byte[] raw)
        {
            ServiceRequest retval = null;
            ServiceRequestContentMode mode = CheckContentMode(raw);

            switch (mode) {
                case ServiceRequestContentMode.binary:
                    retval = new BinaryRequest();
                    retval.InnerParse(raw);
                    break;

                case ServiceRequestContentMode.text:
                case ServiceRequestContentMode.uri:
                    retval = new UriRequest();
                    retval.InnerParse(raw);
                    break;

                case ServiceRequestContentMode.json:
                case ServiceRequestContentMode.json2:
                    retval = new JsonRequest();
                    retval.InnerParse(raw);
                    break;
                
                case ServiceRequestContentMode.none:
                default:
                    retval = new UnknownRequest();
                    retval.InnerParse(raw);
                    break;
            }

            return retval;
        }

        public static ServiceRequest Parse(string raw)
        {
            return Parse(Encoding.UTF8.GetBytes(raw));
        }
    }

    [Serializable]
    public class UnknownRequest : ServiceRequest {
        protected override void InnerParse(byte[] raw)
        {
            content_mode = ServiceRequestContentMode.none;
            packlen = raw.Length;
            raw_data = raw;
            user_data = null;
        }
    }

    [Serializable]
    public class BinaryRequest : ServiceRequest {
        private static readonly int CONTENT_MODE_BYTES = 2;
        private static readonly int BINARY_LENGTH_BYTES = 2;

        protected override void InnerParse(byte[] raw)
        {
            content_mode = ServiceRequestContentMode.binary;
            user_data = null;

            if (raw.Length < 4) {
                this.packlen = 0;
                this.raw_data = new byte[0];
            } else {
                this.packlen = System.Math.Min(raw[2] + (raw[3] << 8), raw.Length);
                this.raw_data = raw.Take(this.packlen).Skip(CONTENT_MODE_BYTES + BINARY_LENGTH_BYTES).ToArray();
            }
        }

        public static void InsertHeader(ref byte[] buffer)
        {
            int mode = (int)ServiceRequestContentMode.binary;

            int len = CONTENT_MODE_BYTES + BINARY_LENGTH_BYTES + buffer.Length;
            buffer = new byte[] { (byte)(mode & 0xff), (byte)(mode >> 8 & 0xff),
                        (byte)(len & 0xff), (byte)(len >> 8 & 0xff) }
                .Concat(buffer).ToArray();
        }
    }

    [Serializable]
    public class UriRequest : ServiceRequest {
        private static readonly int CONTENT_MODE_BYTES = 2;
        private static readonly int TEXT_LENGTH_BYTES = 4;

        protected override void InnerParse(byte[] raw)
        {
            content_mode = ServiceRequestContentMode.uri;
            user_data = null;

            byte[] tmp = raw.Skip(CONTENT_MODE_BYTES).Take(TEXT_LENGTH_BYTES).ToArray();
            this.packlen = int.Parse(Encoding.ASCII.GetString(tmp)); // ascii is better
            this.raw_data = raw.Take(this.packlen).Skip(CONTENT_MODE_BYTES + TEXT_LENGTH_BYTES).ToArray();
        }

        public static void InsertHeader(ref byte[] buffer)
        {
            int mode = (int)ServiceRequestContentMode.binary;

            int len = CONTENT_MODE_BYTES + TEXT_LENGTH_BYTES + buffer.Length;
            len += 10000;
            byte[] len_byte = Encoding.ASCII.GetBytes(len.ToString());
            len_byte = len_byte.Skip(len_byte.Length - 4).ToArray();
            buffer = new byte[] { (byte)(mode & 0xff), (byte)(mode >> 8 & 0xff),
                        len_byte[0], len_byte[1], len_byte[2], len_byte[3] }
                .Concat(buffer).ToArray();
        }
    }

    [Serializable]
    public class JsonRequest : ServiceRequest {
        private static readonly int PARSE_FAIL_MAX_LEN = 1024;

        protected override void InnerParse(byte[] raw)
        {
            content_mode = ServiceRequestContentMode.json;
            user_data = null;

            __InnerParse(raw);
        }

        private void __InnerParse(byte[] raw)
        {
            if (raw[0] != '{') return;

            for (int i = 1, count = 1; i < raw.Length; i++) {
                if (raw[i] == '{') {
                    count++;
                } else if (raw[i] == '}') {
                    if (--count == 0) {
                        packlen = i + 1;
                        raw_data = raw.Take(i+1).ToArray();
                        // skip \r\n
                        while (++i < raw.Length && (raw[i] == '\r' || raw[i] == '\n'))
                            packlen++;
                        return;
                    }
                }
            }

            // parse failed, truncate if length of raw is greater than PARSE_FAIL_MAX_LEN
            if (raw.Length > PARSE_FAIL_MAX_LEN) {
                packlen = raw.Length;
                raw_data = raw;
            } else {
                packlen = 0;
                raw_data = new byte[0];
            }
        }
    }
}
