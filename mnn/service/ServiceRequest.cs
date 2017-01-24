using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using Newtonsoft.Json.Linq;

namespace mnn.service {
    [Serializable]
    public abstract class ServiceRequest {
        public string id { get; protected set; }
        public int packlen { get; protected set; }
        public object data { get; protected set; }
        [NonSerialized]
        public object user_data;

        public abstract bool IsMatch(byte[] raw);
        public abstract void Unpack(byte[] raw);
        public abstract void Pack(ref byte[] buffer);

        public static ServiceRequest Parse(byte[] raw)
        {
            ServiceRequest retval = new UnknownRequest();
            BinaryRequest binary = new BinaryRequest();
            JsonRequest json = new JsonRequest();

            if (binary.IsMatch(raw))
                retval = binary;
            else if (json.IsMatch(raw))
                retval = json;

            retval.Unpack(raw);
            return retval;
        }

        public static ServiceRequest Parse(string raw)
        {
            return Parse(Encoding.UTF8.GetBytes(raw));
        }
    }

    [Serializable]
    public class UnknownRequest : ServiceRequest {
        public override bool IsMatch(byte[] raw)
        {
            throw new NotImplementedException();
        }

        public override void Unpack(byte[] raw)
        {
            packlen = raw.Length;
            data = raw;
            id = "";
        }

        public override void Pack(ref byte[] buffer)
        {
            throw new NotImplementedException();
        }
    }

    [Serializable]
    public class BinaryRequest : ServiceRequest {
        private static readonly int MAGIC = 0x0512;
        private static readonly int MAGIC_BYTES = 2;
        private static readonly int PACKLEN_BYTES = 2;
        private static readonly int ID_BYTES = 2;

        public override bool IsMatch(byte[] raw)
        {
            if (raw.Length < 4)
                return false;

            if (raw[0] + (raw[1] << 8) != MAGIC)
                return false;

            return true;
        }

        public override void Unpack(byte[] raw)
        {
            if (raw.Length < 4)
                throw new Exception("raw package's length must larger than 4");

            if (raw[0] + (raw[1] << 8) != MAGIC)
                throw new Exception("magic check failed, not a binary package");

            int len = raw[2] + (raw[3] << 8);
            if (len < raw.Length)
                throw new Exception("this binary package isn't integrity");

            packlen = len;
            data = raw.Skip(MAGIC_BYTES + PACKLEN_BYTES).ToArray();
            id = Encoding.UTF8.GetString((data as byte[]).Take(ID_BYTES).ToArray());
        }

        public override void Pack(ref byte[] buffer)
        {
            int len = MAGIC_BYTES + PACKLEN_BYTES + ID_BYTES + buffer.Length;
            buffer = new byte[] { (byte)(MAGIC & 0xff), (byte)(MAGIC >> 8 & 0xff),
                (byte)(len & 0xff), (byte)(len >> 8 & 0xff) }.Concat(buffer).ToArray();
        }
    }

    [Serializable]
    public class JsonRequest : ServiceRequest {
        private static readonly int PARSE_FAIL_MAX_LEN = 1024;

        public override bool IsMatch(byte[] raw)
        {
            int whitespace_len = 0;
            while (Char.IsWhiteSpace((Char)raw[whitespace_len++]))
                ;
            whitespace_len--;

            if (raw[whitespace_len] != '{')
                return false;
            else
                return true;
        }

        public override void Unpack(byte[] raw)
        {
            int whitespace_len = 0;
            while (Char.IsWhiteSpace((Char)raw[whitespace_len++]))
                ;
            whitespace_len--;

            if (raw[whitespace_len] != '{')
                throw new Exception("it's not a json package");

            byte[] new_raw = raw.Skip(whitespace_len).ToArray();

            for (int i = 0, count = 0; i < new_raw.Length; i++) {
                if (new_raw[i] == '{') {
                    count++;
                } else if (new_raw[i] == '}') {
                    if (--count == 0) {
                        packlen = whitespace_len + i + 1;
                        data = Encoding.UTF8.GetString(new_raw.Take(i + 1).ToArray());

                        JObject jo = JObject.Parse((string)data);
                        id = (string)jo["id"];
                        return;
                    }
                }
            }

            // parse failed, truncate if length of raw is greater than PARSE_FAIL_MAX_LEN
            if (raw.Length > PARSE_FAIL_MAX_LEN)
                packlen = raw.Length;
            else
                packlen = 0;
            data = "{}";
            id = "";
        }

        public override void Pack(ref byte[] buffer)
        {
            throw new NotImplementedException();
        }
    }
}
