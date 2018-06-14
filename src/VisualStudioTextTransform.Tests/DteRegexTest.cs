using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AIT.Tools.VisualStudioTextTransform.Tests
{
    [TestClass]
    public class DteRegexTest
    {
        [TestMethod]
        public void TestVs2015()
        {
            var regex = DteHelper.CreateDteObjectNameRegex(18616);
            Assert.IsTrue(regex.IsMatch("!VisualStudio.DTE.14.0:18616"));
        }

    }
}
