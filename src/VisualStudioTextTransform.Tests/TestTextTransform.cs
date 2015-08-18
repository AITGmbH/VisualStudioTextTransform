using System;
using System.Diagnostics;
using System.IO;
using System.Net.Mime;
using System.Threading;
using System.Windows.Forms;
using EnvDTE80;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AIT.Tools.VisualStudioTextTransform.Tests
{

    class STAThread : IDisposable
    {
        public STAThread()
        {
            using (mre = new ManualResetEvent(false))
            {
                thread = new Thread(() =>
                {
                    Application.Idle += Initialize;
                    Application.Run();
                });
                thread.IsBackground = true;
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                mre.WaitOne();
            }
        }
        public void BeginInvoke(Delegate dlg, params Object[] args)
        {
            if (ctx == null) throw new ObjectDisposedException("STAThread");
            ctx.Post((_) => dlg.DynamicInvoke(args), null);
        }
        public object Invoke(Delegate dlg, params Object[] args)
        {
            if (ctx == null) throw new ObjectDisposedException("STAThread");
            object result = null;
            ctx.Send((_) => result = dlg.DynamicInvoke(args), null);
            return result;
        }


        protected void Initialize(object sender, EventArgs e)
        {
            ctx = SynchronizationContext.Current;
            mre.Set();
            Application.Idle -= Initialize;
        }
        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                if (ctx != null)
                {
                    ctx.Send((_) => Application.ExitThread(), null);
                    ctx = null;
                }
            }
        }

        private Thread thread;
        private SynchronizationContext ctx;
        private ManualResetEvent mre;
    }

    [TestClass]
    //[DeploymentItem(@"..\..\src")]
    public class TestTextTransform
    {
        private static readonly TraceSource Source = new TraceSource("AIT.Tools.VisualStudioTextTransform");

        private static STAThread _thread;
        private static EnvDTE80.DTE2 _dte;
        private static int _processId;
        private static TestEnv _testEnv;

        [ClassInitialize]
        public static void ClassInit(TestContext context)
        {
            using (new MessageFilter())
            {
                // make sure we create the COM object in a thread accessible by all others
                _thread = new STAThread();
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
                var result = TemplateProcessor.ProcessTemplateInMemory(_dte, itemPath, DefaultVariableResolver.CreateFromDte(_dte, itemPath));
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

        [TestMethod]
        [ExpectedException(typeof(TemplateNotPartOfSolutionException))]
        public void TestNotPartOfSolution()
        {
            TestExecutionByName("NotPartOfSolution.tt", HelperClass.GetResult());
        }

        [TestMethod]
        public void TestGetTemplatePathViaHostResolve()
        {
            const string template = "GetTemplatePathViaHostResolve.tt";
            var itemPath = GetItemPath(template);
            
            TestExecutionByName(template, Path.GetDirectoryName(itemPath));
        }

        [TestMethod]
        public void TestSomeVeryLargePath()
        {
            TestExecutionByName(Path.Combine("SOME_SOME_SOME_SOME_SOME_SOME_SOME_SOME_SOME_SOME_SOME_SOME_SOME_SOME_SOME_SOME_SOME_SOME_SOME_SOME_VERY_LONG_PATH_NAME", "VeryLongPath.tt"),
                "Some example data");
        }
    }
}
