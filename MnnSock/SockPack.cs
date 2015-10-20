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

            cnt_read,
            cnt_write,
            cnt_irq = 0x008F,
            cnt_reg_term = 0x0090,
            cnt_info_term = 0x0091,
            cnt_send_term = 0x0092,
            cnt_reg_svc = 0x00A0,
            cnt_login_user = 0x00B0,

            svc_handle = 0x0021,
            term_handle = 0x0022,
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

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct CntIRQ
        {
            public PackHeader hdr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public char[] ccid;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct CntRegTerm
        {
            public PackHeader hdr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public char[] ccid;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public char[] info;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct CntInfoTerm
        {
            public PackHeader hdr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public char[] ccid; // ccid == "0...0" mains all
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct CntSendTerm
        {
            public PackHeader hdr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public char[] ccid; // ccid == "0...0" mains all
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct CntRegSvc
        {
            public PackHeader hdr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public char[] term_info;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct CntLoginUser
        {
            public PackHeader hdr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 23 + 1)]
            public char[] userid;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32 + 1)]
            public char[] passwd;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct SvcHandle
        {
            public PackHeader hdr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public char[] ccid;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct TermHandle
        {
            public PackHeader hdr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public char[] ccid;
        }
    }
}
