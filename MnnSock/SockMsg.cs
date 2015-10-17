using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace Mnn.MnnSock
{
    public class SockMsg
    {
        public enum MsgType
        {
            alive = 0x0C,
            trans = 0x0D,
            
            // module
            register = 0x20,
            register_respond = 0x21,

        }

        [Serializable] // 指示可序列化
        [StructLayout(LayoutKind.Sequential, Pack = 1)] // 按1字节对齐
        public struct msghdr
        {
            public byte id_type;
            public byte msg_type;
            public UInt16 len;
        }

        [Serializable] // 指示可序列化
        [StructLayout(LayoutKind.Sequential, Pack = 1)] // 按1字节对齐
        public struct termhdr
        {
            public msghdr hdr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public char[] ccid;
        }
    }
}
