using IoTSharp.Data.Taos;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Taos.Ado.Tests
{

    [TestClass]

    public class TestRemove
    {
        [TestMethod]
        [DataRow("test\0test", "test")]
        [DataRow("\0test", "")]
        [DataRow("test\0", "test")]
        [DataRow("", "")]
        [DataRow("\0\0", "")]
        [DataRow(null, null)]
        public void TestRemoveNull(string src, string exp)
        {
            var d = src.RemoveNull();
            Assert.AreEqual(exp, d);
        }

    }
}
