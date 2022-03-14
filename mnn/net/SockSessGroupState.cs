using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace mnn.net {
    public class SockSessGroupState {
        public int ListenCount { get; set; }
        public int ConnectCount { get; set; }
        public int AcceptOpenCount { get; set; }
        public int AcceptCloseCount { get; set; }
        public int AcceptTotalCount { get { return AcceptOpenCount + AcceptCloseCount; } }
        public int PackFetchedCount { get; set; }
        public int PackParsedCount { get; set; }
        public int PackTotalCount { get { return PackFetchedCount + PackParsedCount; } }

        public void AcceptIncrease()
        {
            AcceptOpenCount++;
        }

        public void AcceptDecrease()
        {
            AcceptOpenCount--;
            AcceptCloseCount++;
        }

        public void PackIncrease()
        {
            PackFetchedCount++;
        }

        public void PackDecrease()
        {
            PackFetchedCount--;
            PackParsedCount++;
        }
    }
}
