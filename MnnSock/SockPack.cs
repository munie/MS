using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace Mnn.MnnSock
{
    public class SockPack
    {
        public enum PackName : byte
        {
            alive = 0x0C,

            term_register = 0x10,
            term_request = 0x11,

            svc_register = 0x20,
            svc_trans = 0x0D,
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct PackHeader
        {
            public byte id_type;
            public PackName name;
            public UInt16 len;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct TermRegister
        {
            public PackHeader hdr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public char[] ccid;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public char[] info;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct TermRequest
        {
            public PackHeader hdr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public char[] ccid;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct SvcRegister
        {
            public PackHeader hdr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public char[] term_info;
        }
    }
}
