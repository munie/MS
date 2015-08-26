using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SocketTransfer
{
    public interface IDataHandle
    {
        int GetDefaultListenPort();

        void InitializeSource();

        void StartListener(System.Net.IPEndPoint ep);

        void StopListener();

        void StartTimerCommand(double interval, string command);

        void StopTimerCommand();

        void SendClientCommand(System.Net.IPEndPoint ep, byte[] data);

        void CloseClient(System.Net.IPEndPoint ep);
    }
}
