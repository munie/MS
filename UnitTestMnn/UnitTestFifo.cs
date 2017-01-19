using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using mnn.util;

namespace UnitTestMnn {
    [TestClass]
    public class UnitTestFifo {
        [TestMethod]
        public void TestAppend()
        {
            int RDATA_SIZE = 256;
            Fifo<byte> rdata = new Fifo<byte>(RDATA_SIZE);
            byte[] inData = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99 };
            byte[] inData2 = new byte[] { 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0xA6, 0xA7, 0xA8, 0xA9 };

            rdata.Append(inData);
            Assert.AreEqual(rdata.FreeSpace(), RDATA_SIZE - inData.Length);
            Assert.AreEqual(rdata.Size(), inData.Length);
            for (int i = 0; i < inData.Length; i++)
                Assert.AreEqual(rdata.Peek()[i], inData[i]);

            rdata.Append(inData2);
            byte[] inDataAll = inData.Concat(inData2).ToArray();
            Assert.AreEqual(rdata.FreeSpace(), RDATA_SIZE - inDataAll.Length);
            Assert.AreEqual(rdata.Size(), inDataAll.Length);
            for (int i = 0; i < inDataAll.Length; i++)
                Assert.AreEqual(rdata.Peek()[i], inDataAll[i]);

            rdata.Resize(RDATA_SIZE * 2);
            Assert.AreEqual(rdata.FreeSpace(), RDATA_SIZE * 2 - inDataAll.Length);
            Assert.AreEqual(rdata.Size(), inDataAll.Length);
            for (int i = 0; i < inDataAll.Length; i++)
                Assert.AreEqual(rdata.Peek()[i], inDataAll[i]);

            byte[] outData = rdata.Take();
            Assert.AreEqual(outData.Length, inDataAll.Length);
            for (int i = 0; i < inDataAll.Length; i++)
                Assert.AreEqual(outData[i], inDataAll[i]);
        }
    }
}
