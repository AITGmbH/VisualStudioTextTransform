using System;
using System.IO;
using System.Linq;
using AIT.VisualStudio.Controlling;
using EnvDTE80;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AIT.Tools.VisualStudioTextTransform.Tests
{
    /// <summary>
    /// Test class
    /// </summary>
    [TestClass]
    //[DeploymentItem(@"..\..\src")]
    public class TestTextTransform
    {
        private static StaThread _thread;
        private static DTE2 _dte;
        private static int _processId;
        private static TestEnv _testEnv;

        /// <summary>
        /// Initialize the class.
        /// </summary>
        /// <param name="context"></param>
        [ClassInitialize]
        public static void ClassInit(TestContext context)
        {
            using (new MessageFilter())
            {
                // make sure we create the COM object in a thread accessible by all others
                _thread = new StaThread();
                var result = (Tuple<int, DTE2>)_thread.Invoke(new Func<Tuple<int, DTE2>>(
                    () =>
                    {
                        using (new MessageFilter())
                        {
                            return DteHelper.CreateDteInstance();
                        }
                    }));
                //var result = DteHelper.CreateDteInstance();
                _dte = result.Item2;
                _processId = result.Item1;

                _testEnv = new TestEnv();
                _dte.Solution.Open(_testEnv.SolutionFile);
            }
        }

        /// <summary>
        /// Cleanup the class.
        /// </summary>
        [ClassCleanup]
        public static void ClassCleanup()
        {
            DteHelper.CleanupDteInstance(_processId, _dte);

            if (_thread != null)
            {
                _thread.Dispose();
                _thread = null;
            }
        }

        private static void TestExecutionByName(string testTemplateName, string expected)
        {
            // We might be running on any thread that mstest pleases, therefore we need to ensure to have a messagefilter in place.
            using (new MessageFilter())
            {
                var itemPath = GetItemPath(testTemplateName);
                Assert.IsTrue(File.Exists(itemPath));
                var defaultResolver = DefaultVariableResolver.CreateFromDte(_dte, itemPath);
                var resolver = new CombiningVariableResolver(DictionaryVariableResolver.FromOptionsValue("KnownProperty:" + defaultResolver.ResolveVariable("TargetDir").First()), defaultResolver);
                var result = TemplateProcessor.ProcessTemplateInMemory(_dte, itemPath, resolver);
                Assert.IsFalse(result.Item2.Errors.HasErrors);
                Assert.IsFalse(result.Item2.Errors.Count > 0);
                Assert.AreEqual(expected, result.Item1);
            }
        }

        private static string GetItemPath(string testTemplateName)
        {
            var itemPath = Path.Combine(_testEnv.TestProjectDir, "TestTemplates", testTemplateName);
            return itemPath;
        }

        /// <summary>
        /// Test a simple template.
        /// </summary>
        [TestMethod]
        public void TestSimpleExample()
        {
            TestExecutionByName("SimpleExample.tt", "Some simple string format with this");
        }

        /// <summary>
        /// Test template using visual studio interfaces.
        /// </summary>
        [TestMethod]
        public void TestVisualStudioExample()
        {
            TestExecutionByName("VisualStudioExample.tt", "VisualStudioTextTransform.Tests");
        }

        /// <summary>
        /// Test template with external dependencies.
        /// </summary>
        [TestMethod]
        public void TestExternalDependency()
        {
            TestExecutionByName("ExternalDependency.tt", HelperClass.GetResult());
        }

        /// <summary>
        /// Test template with project directory.
        /// </summary>
        [TestMethod]
        public void TestProjectDir()
        {
            // ReSharper disable once LocalizableElement
            Console.WriteLine("Tested as part of ExternalDependency.tt");
        }

        /// <summary>
        /// Test template with target directory.
        /// </summary>
        [TestMethod]
        public void TestTargetDir()
        {
            TestExecutionByName("TargetDir.tt", HelperClass.GetResult());
        }

        /// <summary>
        /// Test template with solution directory.
        /// </summary>
        [TestMethod]
        public void TestSolutionDir()
        {
            TestExecutionByName("SolutionDir.tt", HelperClass.GetResult());
        }

        /// <summary>
        /// Test template not part of the solution.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(TemplateNotPartOfSolutionException))]
        public void TestNotPartOfSolution()
        {
            TestExecutionByName("NotPartOfSolution.tt", HelperClass.GetResult());
        }

        /// <summary>
        /// Test template not part of the solution.
        /// </summary>
        [TestMethod]
        public void TestKnownProperty()
        {
            TestExecutionByName("KnownProperty.tt", HelperClass.GetResult());
        }

        /// <summary>
        /// Test template using Host.Resolve.
        /// </summary>
        [TestMethod]
        public void TestGetTemplatePathViaHostResolve()
        {
            const string template = "GetTemplatePathViaHostResolve.tt";
            var itemPath = GetItemPath(template);

            TestExecutionByName(template, Path.GetDirectoryName(itemPath));
        }

        /// <summary>
        /// Test template in a very long path.
        /// </summary>
        [TestMethod]
        public void TestSomeVeryLargePath()
        {
            TestExecutionByName(Path.Combine("SOME_SOME_SOME_SOME_SOME_SOME_SOME_SOME_SOME_SOME_SOME_SOME_SOME_SOME_SOME_SOME_SOME_SOME_SOME_SOME_VERY_LONG_PATH_NAME", "VeryLongPath.tt"),
                "Some example data");
        }
    }
}
