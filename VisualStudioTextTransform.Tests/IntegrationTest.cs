using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AIT.Tools.VisualStudioTextTransform.Tests
{
    [TestClass]
    public class IntegrationTest
    {
        [TestMethod]
        public void TestMainRun()
        {
            var testEnv = new TestEnv();
            var result = Program.Main(new[] {testEnv.SolutionFile});
            Assert.IsTrue(result == 0);
        }
    }
}
