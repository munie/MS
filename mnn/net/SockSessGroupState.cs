using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace mnn.net {
    public class SockSessGroupState {
        public int ListenCount { get; set; }
        public int ConnectCount { get; set; }
        public int CurrentAcceptCount { get; set; }
        public int HistoryAcceptOpenCount { get; set; }
        public int HistoryAcceptCloseCount { get; set; }
        public int CurrentPackCount { get; set; }
        public int HistoryPackFetchedCount { get; set; }
        public int HistoryPackParsedCount { get; set; }

        public void AcceptIncrease()
        {
            CurrentAcceptCount++;
            HistoryAcceptOpenCount++;
        }

        public void AcceptDecrease()
        {
            CurrentAcceptCount--;
            HistoryAcceptCloseCount++;
        }

        public void PackIncrease()
        {
            CurrentPackCount++;
            HistoryPackFetchedCount++;
        }

        public void PackDecrease()
        {
            CurrentPackCount--;
            HistoryPackParsedCount++;
        }
    }
}
