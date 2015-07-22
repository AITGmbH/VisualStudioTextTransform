using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using AIT.Tools.VisualStudioTextTransform.Properties;
using EnvDTE80;
using Engine = Microsoft.VisualStudio.TextTemplating.Engine;

namespace AIT.Tools.VisualStudioTextTransform
{
    public static class Program
    {
        private static readonly TraceSource Source = new TraceSource("AIT.Tools.VisualStudioTextTransform");

        // From http://www.viva64.com/en/b/0169/
        public static DTE2 GetById(int ID)
        {
            //rot entry for visual studio running under current process.
            string rotEntry = String.Format(CultureInfo.InvariantCulture, "!VisualStudio.DTE.12.0:{0}", ID);
            IRunningObjectTable rot;
            NativeMethods.GetRunningObjectTable(0, out rot);
            IEnumMoniker enumMoniker;
            rot.EnumRunning(out enumMoniker);
            enumMoniker.Reset();
            IntPtr fetched = IntPtr.Zero;
            IMoniker[] moniker = new IMoniker[1];
            while (enumMoniker.Next(1, moniker, fetched) == 0)
            {
                IBindCtx bindCtx;
                NativeMethods.CreateBindCtx(0, out bindCtx);
                string displayName;
                moniker[0].GetDisplayName(bindCtx, null, out displayName);
                if (displayName == rotEntry)
                {
                    object comObject;
                    var result = rot.GetObject(moniker[0], out comObject);
                    Marshal.ThrowExceptionForHR(result);
                    return (DTE2)comObject;
                }
            }
            return null;
        }

        public static Tuple<int, DTE2> CreateDteInstance()
        {
            // We Create our own instance for customized logging + killing afterwards
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var pfx64 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var vs2013Relative = Path.Combine("Microsoft Visual Studio 12.0", "Common7", "IDE", "devenv.exe");
            var testPaths = new[]
            {
                Path.Combine(pf, vs2013Relative),
                Path.Combine(pfx64, vs2013Relative)
            };
            var devenvExe = testPaths.FirstOrDefault(File.Exists);
            if (String.IsNullOrEmpty(devenvExe))
            {
                Source.TraceEvent(TraceEventType.Error, 0, "Could not find devenv.exe, falling back to COM.");
                var dteType = Type.GetTypeFromProgID("VisualStudio.DTE.12.0", true);
                return Tuple.Create(-1, (DTE2)Activator.CreateInstance(dteType, true));
            }
            using (var start = 
                Process.Start(
                    devenvExe, 
                    string.Format(CultureInfo.InvariantCulture, "-Embedding /log \"{0}\"", 
                        Path.GetFullPath(Settings.Default.VisualStudioLogfile))))
            {
                Thread.Sleep(10000);
                var dte = GetById(start.Id);
                if (dte == null)
                {
                    throw new InvalidOperationException("Could not get DTE instance from process!");
                }
                return Tuple.Create(start.Id, dte);
            }
        }

        public static Tuple<string, VisualStudioTextTemplateHost> ProcessTemplateInMemory(DTE2 dte, string templateFileName)
        {
            if (dte == null)
            {
                throw new ArgumentNullException("dte");
            }
            if (string.IsNullOrEmpty(templateFileName) || !File.Exists(templateFileName))
            {
                throw new ArgumentException(Resources.Program_ProcessTemplateInMemory_String_is_null_or_empty_or_file_doesn_t_exist_, templateFileName);
            }

            //// This would be WAY morer elegant, but it spawns a confirmation box...
            ////printfn "Transforming templates..."
            ////dte.ExecuteCommand("TextTransformation.TransformAllTemplates")

            Source.TraceEvent(TraceEventType.Information, 0, Resources.Program_ProcessTemplate_Processing___0_____, templateFileName);

            var templateDir = Path.GetDirectoryName(templateFileName);
            //  Setup Environment
            var oldDir = Environment.CurrentDirectory;
            try
            {
                Environment.CurrentDirectory = templateDir;

                // Setup NamespaceHint in CallContext
                var templateFileItem = dte.Solution.FindProjectItem(templateFileName);
                var project = templateFileItem.ContainingProject;
                var projectDir = Path.GetDirectoryName(project.FullName);
                string defaultNamespace = project.Properties.Item("DefaultNamespace").Value.ToString();
                var templateFileNameUpper = templateFileName.ToUpperInvariant();
                var projectDirUpper = projectDir.ToUpperInvariant();
                Debug.Assert(templateFileNameUpper.StartsWith(projectDirUpper, StringComparison.Ordinal));
                var templateFileDir = Path.GetDirectoryName(templateFileName);
                var finalNamespace = defaultNamespace;
                if (templateFileDir.Length != projectDir.Length)
                {
                    var relativeNamespace =
                        templateFileDir.Substring(projectDir.Length + 1)
                        // BUG? Handle all namespace relevant characters
                            .Replace("\\", ".").Replace("/", ".");
                    finalNamespace =
                        string.Format(CultureInfo.InvariantCulture, "{0}.{1}", defaultNamespace, relativeNamespace);
                }

                using (new LogicalCallContextChange("NamespaceHint", finalNamespace))
                {
                    var host = new VisualStudioTextTemplateHost(templateFileName, dte);
                    var engine = new Engine();
                    var input = File.ReadAllText(templateFileName);
                    var output = engine.ProcessTemplate(input, host);
                    return Tuple.Create(output, host);
                }
            }
            finally
            {
                Environment.CurrentDirectory = oldDir;
            }
        }

        public static CompilerErrorCollection ProcessTemplate(DTE2 dte, string templateFileName)
        {
            var templateDir = Path.GetDirectoryName(templateFileName);
            var result = ProcessTemplateInMemory(dte, templateFileName);
            var host = result.Item2;
            var output = result.Item1;

            var outFileName = Path.GetFileNameWithoutExtension(templateFileName);
            var outFilePath = Path.Combine(templateDir, outFileName + host.FileExtension);
            // Because with TFS the files could be read-only!
            if (File.Exists(outFilePath))
            {
                File.Delete(outFilePath);
            }
            File.WriteAllText(outFilePath, output, host.FileEncoding);
            return host.Errors;
        }

        private static IEnumerable<string> FindTemplates(string p)
        {
            foreach (var template in Directory.EnumerateDirectories(p).SelectMany(FindTemplates))
            {
                yield return template;
            }
            foreach (var template in Directory.EnumerateFiles(p, "*.tt"))
            {
                yield return Path.GetFullPath(template);
            }
        }

        [STAThread]
        public static int Main(string[] arguments)
        {
            try
            {
                return ExecuteMain(arguments);
            }
            catch (Exception e)
            {
                Source.TraceEvent(TraceEventType.Critical, 1, Resources.Program_Main_Application_crashed_with___0_, e);
                return 1;
            }
        }

        private static int ExecuteMain(string[] arguments)
        {
            if (arguments.Length == 0)
            {
                throw new ArgumentException(Resources.Program_Main_you_must_provide_a_solution_file);
            }
            var solutionFileName = arguments[0];
            if (string.IsNullOrEmpty(solutionFileName) || !File.Exists(solutionFileName))
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.CurrentUICulture,
                        Resources.Program_Main_the_file_path___0___is_either_invalid_or_doesn_t_exist_, solutionFileName));
            }

            solutionFileName = Path.GetFullPath(solutionFileName);
            Source.TraceEvent(TraceEventType.Information, 0, Resources.Program_Main_Creating_VS_instance___);
            using (new MessageFilter())
            {
                var result = CreateDteInstance();
                var dte = result.Item2;
                var processId = result.Item1;
                try
                {
                    Source.TraceEvent(TraceEventType.Information, 0, Resources.Program_Main_Opening__0_, solutionFileName);
                    dte.Solution.Open(solutionFileName);

                    Source.TraceEvent(TraceEventType.Verbose, 0, Resources.Program_Main_Finding_and_processing___tt_templates___);
                    var firstError =
                        FindTemplates(Path.GetDirectoryName(solutionFileName))
                            .Select(t => Tuple.Create(t, ProcessTemplate(dte, t)))
                            .FirstOrDefault(tuple => tuple.Item2.Count > 0);

                    if (firstError != null)
                    {
                        Source.TraceEvent(TraceEventType.Warning, 0, Resources.Program_Main_FAILED_to_process___0__,
                            firstError.Item1);
                        foreach (var error in firstError.Item2)
                        {
                            Source.TraceEvent(TraceEventType.Error, 0, Resources.Program_Main_, error);
                        }
                        return 1;
                    }

                    Source.TraceEvent(TraceEventType.Information, 0, Resources.Program_Main_Everything_worked_);
                    return 0;
                }
                finally
                {
                    var process = Process.GetProcessById(processId);
                    dte.Quit();
                    Thread.Sleep(10000);
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                }
            }
        }
    }
}