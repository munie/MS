using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace Mnn.MnnSock
{
    public class SockPack
    {
        public enum PackName : ushort
        {
            alive = 0x000C,

            term_register = 0x0010,
            term_request = 0x0011,
            term_respond = 0x0012,

            svc_register = 0x0020,
        }

        /// <summary>
        /// Common Header
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct PackHeader
        {
            public PackName name;
            public UInt16 len;
        }

        /// <summary>
        /// Terminal Register
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct TermRegister
        {
            public PackHeader hdr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public char[] ccid;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public char[] info;
        }

        /// <summary>
        /// Terminal Request
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct TermRequest
        {
            public PackHeader hdr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public char[] ccid;
        }

        /// <summary>
        /// Terminal Respond
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct TermRespond
        {
            public PackHeader hdr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public char[] ccid;
        }

        /// <summary>
        /// Service Register
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct SvcRegister
        {
            public PackHeader hdr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public char[] term_info;
        }
    }
}
