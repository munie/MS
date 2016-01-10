using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace mnn.net.deprecated {
    public class SockPack {
        public enum PackName : ushort {
            alive = 0x000C,

            cnt_read,
            cnt_write,
            cnt_irq = 0x008F,
            cnt_reg_term = 0x0090, // +1 for respond
            cnt_send_term = 0x0092,
            cnt_info_term = 0x0094,
            cnt_reg_svc = 0x00A0,
            cnt_login_user = 0x00B0,
            cnt_info_user = 0x00B2,

            svc_handle = 0x0021,
            term_handle = 0x0022,
        }

        /// <summary>
        /// Common Header
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct PackHeader {
            public PackName name;
            public UInt16 len;
        }

        /// <summary>
        /// IRQ
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct CntIRQ {
            public PackHeader hdr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public char[] ccid;
        }

        /// <summary>
        /// Register Terminal
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct CntRegTerm {
            public PackHeader hdr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public char[] ccid;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public char[] info;
        }

        /// <summary>
        /// Send Terminal
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct CntSendTerm {
            public PackHeader hdr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public char[] ccid;
        }

        /// <summary>
        /// Info Terminal
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct CntInfoTerm {
            public PackHeader hdr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public char[] ccid; // "0...0" mains all
        }

        /// <summary>
        /// Register Service
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct CntRegSvc {
            public PackHeader hdr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public char[] term_info;
        }

        /// <summary>
        /// Login User
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct CntLoginUser {
            public PackHeader hdr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 23 + 1)]
            public char[] userid;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32 + 1)]
            public char[] passwd;
        }

        /// <summary>
        /// Info User
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct CntInfoUser {
            public PackHeader hdr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 23 + 1)]
            public char[] userid;  // "0...0" mains all
        }

        /// <summary>
        /// Service Handle
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct SvcHandle {
            public PackHeader hdr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public char[] ccid;
        }

        /// <summary>
        /// Terminal Handle
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct TermHandle {
            public PackHeader hdr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public char[] ccid;
        }
    }
}
