using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AIT.Tools.VisualStudioTextTransform.Tests
{
    /// <summary>
    /// Test the variable resolver code
    /// </summary>
    [TestClass]
    public class TestFindVariable
    {
        /// <summary>
        /// Test if we can find ProjectDir variable.
        /// </summary>
        [TestMethod]
        public void TestFindProjectDir()
        {
            var res = VisualStudioTextTemplateHost.FindVariables("$(ProjectDir)").ToArray();
            Assert.AreEqual(1, res.Length);
            Assert.AreEqual("ProjectDir", res[0]);
        }

        /// <summary>
        /// Test if we can find multiple variables.
        /// </summary>
        [TestMethod]
        public void TestFindMultipleVariables()
        {
            var res = VisualStudioTextTemplateHost.FindVariables("$(ProjectDir) $(HomagGroupResourceHelperIncludeDir)").ToArray();
            Assert.AreEqual(2, res.Length);
            Assert.AreEqual("ProjectDir", res[0]);
            Assert.AreEqual("HomagGroupResourceHelperIncludeDir", res[1]);
        }


        /// <summary>
        /// Test if everything works in the middle.
        /// </summary>
        [TestMethod]
        public void TestFindMiddle()
        {
            var res = VisualStudioTextTemplateHost.FindVariables("Some prefix $(ProjectDir) some postfix").ToArray();
            Assert.AreEqual(1, res.Length);
            Assert.AreEqual("ProjectDir", res[0]);
        }
    }
}
