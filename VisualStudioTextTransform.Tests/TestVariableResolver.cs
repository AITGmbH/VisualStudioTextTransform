using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AIT.Tools.VisualStudioTextTransform.Tests
{
    [TestClass]
    public class TestVariableResolver
    {
        [TestMethod]
        public void TestCombinator()
        {
            var res1 = new DefaultVariableResolver(null, null, "dir1");
            var res2 = new DefaultVariableResolver(null, null, "dir2");
            var comb = new CombiningVariableResolver(res1, res2);
            Assert.IsFalse(comb.ResolveVariable("ProjectDir").Any());
            var targetDirs = comb.ResolveVariable("TargetDir").ToList();
            Assert.AreEqual(2, targetDirs.Count);
            Assert.AreEqual(res1.TargetDir + Path.DirectorySeparatorChar, targetDirs[0]);
            Assert.AreEqual(res2.TargetDir + Path.DirectorySeparatorChar, targetDirs[1]);
        }
    }
}
