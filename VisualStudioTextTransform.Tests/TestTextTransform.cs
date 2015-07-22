using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AIT.Tools.VisualStudioTextTransform.Tests
{

    [TestClass]
    public class TestTextTransform
    {
        private static EnvDTE80.DTE2 _dte;
        private static string _debugDir;
        private static string _binDir;
        private static string _testProjectDir;
        private static string _solutionDir;
        private static MessageFilter _msgFilter;

        [ClassInitialize]
        public static void ClassInit(TestContext context)
        {
            _msgFilter = new MessageFilter();
            var dteType = Type.GetTypeFromProgID("VisualStudio.DTE.12.0", true);
            _dte = (EnvDTE80.DTE2)Activator.CreateInstance(dteType, true);

            _debugDir = Environment.CurrentDirectory;
            _binDir = Path.GetDirectoryName(_debugDir);
            _testProjectDir = Path.GetDirectoryName(_binDir);
            _solutionDir = Path.GetDirectoryName(_testProjectDir);
            var solutionFile = Path.GetFullPath(Path.Combine(_solutionDir, "VisualStudioTextTransform.sln"));
            _dte.Solution.Open(solutionFile);
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            if (_dte != null)
            {
                _dte.Quit();
                _dte = null;
            }

            if (_msgFilter != null)
            {
                _msgFilter.Dispose();
                _msgFilter = null;
            }
        }

        private static void TestExecutionByName(string testTemplateName, string expected)
        {
            var itemPath = Path.Combine(_testProjectDir, "TestTemplates", testTemplateName);
            Assert.IsTrue(File.Exists(itemPath));
            var result = Program.ProcessTemplateInMemory(_dte, itemPath);
            Assert.IsFalse(result.Item2.Errors.HasErrors);
            Assert.IsFalse(result.Item2.Errors.Count > 0);
            Assert.AreEqual(expected, result.Item1);
        }

        [TestMethod]
        public void TestSimpleExample()
        {
            TestExecutionByName("SimpleExample.tt", "Some simple string format with this");
        }

        [TestMethod]
        public void TestVisualStudioExample()
        {
            TestExecutionByName("VisualStudioExample.tt", "VisualStudioTextTransform.Tests");
        }

        [TestMethod]
        public void TestExternalDependency()
        {
            TestExecutionByName("ExternalDependency.tt", HelperClass.GetResult());
        }

        [TestMethod]
        public void TestProjectDir()
        {
            Console.WriteLine("Tested as part of ExternalDependency.tt");
        }

        [TestMethod]
        public void TestTargetDir()
        {
            TestExecutionByName("TargetDir.tt", HelperClass.GetResult());
        }
        [TestMethod]
        public void TestSolutionDir()
        {
            TestExecutionByName("SolutionDir.tt", HelperClass.GetResult());
        }
    }
}
