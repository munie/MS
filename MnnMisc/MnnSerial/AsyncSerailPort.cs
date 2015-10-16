using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;
using System.Threading;

namespace Mnn.MnnMisc.MnnSerial
{
    public class AsyncSerailPort
    {
        public event EventHandler<AsyncSerailPortEventArgs> PortOpen;
        public event EventHandler<AsyncSerailPortEventArgs> PortClose;
        public event EventHandler<AsyncSerailPortEventArgs> PortReadMsg;
        public event EventHandler<AsyncSerailPortEventArgs> PortSendMsg;

        SerialPort serialPort = new SerialPort();
        byte[] buffer = new byte[8192];
        StringBuilder sb = new StringBuilder();

        public void Start(string protName, int baudRate, int dataBits, int stopBits, int parity)
        {
            serialPort.PortName = protName;
            serialPort.BaudRate = baudRate;
            serialPort.Parity = (Parity)parity;
            serialPort.DataBits = dataBits;
            serialPort.StopBits = (StopBits)stopBits;
            //sp.Handshake = ;
            serialPort.ReadTimeout = 500;

            serialPort.DataReceived += new SerialDataReceivedEventHandler((s, ea) =>
            {
                Thread.Sleep(500);
                int bytesRead = serialPort.Read(buffer, 0, buffer.Count());
                sb.Append(Encoding.GetEncoding(936).GetString(buffer, 0, bytesRead));

                if (PortReadMsg != null) {
                    PortReadMsg.Invoke(this, new AsyncSerailPortEventArgs()
                    {
                        PortName = serialPort.PortName,
                        BaudRate = serialPort.BaudRate,
                        DataBits = serialPort.DataBits,
                        StopBits = (int)serialPort.StopBits,
                        Parity = (int)serialPort.Parity,
                        Data = sb.ToString(),
                    });
                }

                sb.Clear();
            });

            serialPort.Open();
            if (PortOpen != null) {
                PortOpen.Invoke(this, new AsyncSerailPortEventArgs()
                {
                    PortName = serialPort.PortName,
                    BaudRate = serialPort.BaudRate,
                    DataBits = serialPort.DataBits,
                    StopBits = (int)serialPort.StopBits,
                    Parity = (int)serialPort.Parity,
                    Data = "",
                });
            }
        }

        public void Stop()
        {
            serialPort.Close();
            if (PortClose != null) {
                PortClose.Invoke(this, new AsyncSerailPortEventArgs()
                {
                    PortName = serialPort.PortName,
                    BaudRate = serialPort.BaudRate,
                    DataBits = serialPort.DataBits,
                    StopBits = (int)serialPort.StopBits,
                    Parity = (int)serialPort.Parity,
                    Data = "",
                });
            }
        }

        public void Send(string data)
        {
            serialPort.Write(data);
            if (PortSendMsg != null) {
                PortSendMsg.Invoke(this, new AsyncSerailPortEventArgs()
                {
                    PortName = serialPort.PortName,
                    BaudRate = serialPort.BaudRate,
                    DataBits = serialPort.DataBits,
                    StopBits = (int)serialPort.StopBits,
                    Parity = (int)serialPort.Parity,
                    Data = data,
                });
            }
        } 
    }
}
