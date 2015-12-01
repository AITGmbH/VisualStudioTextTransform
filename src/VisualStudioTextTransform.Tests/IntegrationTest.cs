using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AIT.Tools.VisualStudioTextTransform.Tests
{
    /// <summary>
    /// Some integration tests
    /// </summary>
    [TestClass]
    public class IntegrationTest
    {
        /// <summary>
        /// Call the Application on ourself.
        /// </summary>
        [TestMethod]
        public void TestMainRun()
        {
            var testEnv = new TestEnv();
            var result = Program.Main(new[] { testEnv.SolutionFile, "--properties", "KnownProperty:" + Path.Combine(testEnv.SolutionDir, "..", "build", "test", "net45") + Path.DirectorySeparatorChar });
            Assert.IsTrue(result == 0);
        }
    }
}
