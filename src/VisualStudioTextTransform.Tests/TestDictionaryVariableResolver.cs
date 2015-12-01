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
    public class TestDictionaryVariableResolver
    {
        /// <summary>
        /// Test if the resolver combinator is working.
        /// </summary>
        [TestMethod]
        public void TestCreation()
        {
            var r = DictionaryVariableResolver.FromOptionsValue("test:C:\\testfolder");
            var resolved = r.ResolveVariable("test").ToArray();
            Assert.AreEqual(1, resolved.Length);
            Assert.AreEqual("C:\\testfolder", resolved[0]);
        }
    }
}
