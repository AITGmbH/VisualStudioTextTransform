using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using AIT.Tools.VisualStudioTextTransform.Properties;
using EnvDTE80;
using Engine = Microsoft.VisualStudio.TextTemplating.Engine;

namespace AIT.Tools.VisualStudioTextTransform
{
    /// <summary>
    /// /
    /// </summary>
    public static class TemplateProcessor
    {
        private static readonly TraceSource Source = new TraceSource("AIT.Tools.VisualStudioTextTransform");

        /// <summary>
        /// /
        /// </summary>
        /// <param name="dte"></param>
        /// <param name="templateFileName"></param>
        /// <param name="resolver"></param>
        /// <returns></returns>
        public static Tuple<string, VisualStudioTextTemplateHost> ProcessTemplateInMemory(DTE2 dte, string templateFileName, IVariableResolver resolver)
        {
            if (dte == null)
            {
                throw new ArgumentNullException("dte");
            }
            if (string.IsNullOrEmpty(templateFileName) || !File.Exists(templateFileName))
            {
                throw new ArgumentException(Resources.Program_ProcessTemplateInMemory_String_is_null_or_empty_or_file_doesn_t_exist_, templateFileName);
            }

            //// This would be WAY more elegant, but it spawns a confirmation box...
            ////printfn "Transforming templates..."
            ////dte.ExecuteCommand("TextTransformation.TransformAllTemplates")

            Source.TraceEvent(TraceEventType.Information, 0, Resources.Program_ProcessTemplate_Processing___0_____, templateFileName);

            var templateDir = Path.GetDirectoryName(templateFileName);
            Debug.Assert(templateDir != null, "templateDir != null, don't expect templateFileName to be a root directory.");
            //  Setup Environment
            var oldDir = Environment.CurrentDirectory;
            try
            {
                Environment.CurrentDirectory = templateDir;

                // Setup NamespaceHint in CallContext
                var templateFileItem = dte.Solution.FindProjectItem(templateFileName);
                var project = templateFileItem.ContainingProject;
                var projectDir = Path.GetDirectoryName(project.FullName);
                Debug.Assert(projectDir != null, "projectDir != null, don't expect project.FullName to be a root directory.");
                string defaultNamespace = project.Properties.Item("DefaultNamespace").Value.ToString();
                var templateFileNameUpper = templateFileName.ToUpperInvariant();
                var projectDirUpper = projectDir.ToUpperInvariant();
                Debug.Assert(templateFileNameUpper.StartsWith(projectDirUpper, StringComparison.Ordinal), "Template file-name is not within the project directory.");

                var finalNamespace = defaultNamespace;
                if (templateDir.Length != projectDir.Length)
                {
                    var relativeNamespace =
                        templateDir.Substring(projectDir.Length + 1)
                            // BUG? Handle all namespace relevant characters
                            .Replace("\\", ".").Replace("/", ".");
                    finalNamespace =
                        string.Format(CultureInfo.InvariantCulture, "{0}.{1}", defaultNamespace, relativeNamespace);
                }

                using (new LogicalCallContextChange("NamespaceHint", finalNamespace))
                {
                    
                    var host = new VisualStudioTextTemplateHost(templateFileName, dte, resolver);
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

        /// <summary>
        /// /
        /// </summary>
        /// <param name="dte"></param>
        /// <param name="templateFileName"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static CompilerErrorCollection ProcessTemplate(DTE2 dte, string templateFileName, Options options)
        {
            if (dte == null)
            {
                throw new ArgumentNullException("dte");
            }
            if (templateFileName == null)
            {
                throw new ArgumentNullException("templateFileName");
            }
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            var templateDir = Path.GetDirectoryName(templateFileName);
            Debug.Assert(templateDir != null, "templateDir != null, don't expected templateFileName to be a root directory!");
            var defaultResolver = DefaultVariableResolver.CreateFromDte(dte, templateFileName);
            IVariableResolver resolver = defaultResolver;
            Source.TraceEvent(TraceEventType.Information, 1, "Default TargetDir {0} will be used", defaultResolver.TargetDir);
            Source.TraceEvent(TraceEventType.Information, 1, "Default SolutionDir {0} will be used", defaultResolver.SolutionDir);
            Source.TraceEvent(TraceEventType.Information, 1, "Default ProjectDir {0} will be used", defaultResolver.ProjectDir);

            if (!string.IsNullOrEmpty(options.TargetDir))
            {
                if (Directory.Exists(options.TargetDir))
                {
                    Source.TraceEvent(TraceEventType.Information, 1, "TargetDir {0} will be added ", options.TargetDir);
                    resolver = new CombiningVariableResolver(new DefaultVariableResolver(null, null, options.TargetDir), resolver);
                }
                else
                {
                    Source.TraceEvent(TraceEventType.Warning, 1, "TargetDir {0} doesn't exist and will be ignored!", options.TargetDir);
                }
            }

            var result = ProcessTemplateInMemory(dte, templateFileName, resolver);
            var host = result.Item2;
            var output = result.Item1;

            var outFileName = Path.GetFileNameWithoutExtension(templateFileName);
            var outFilePath = Path.Combine(templateDir, outFileName + host.FileExtension);
            // Because with TFS the files could be read-only!
            if (File.Exists(outFilePath))
            {
                var attr = File.GetAttributes(outFilePath);
                File.SetAttributes(outFilePath, attr & ~FileAttributes.ReadOnly);
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

        /// <summary>
        /// /
        /// </summary>
        /// <param name="solutionFileName"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static bool ProcessSolution(string solutionFileName, Options options)
        {
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
                var result = DteHelper.CreateDteInstance();
                var dte = result.Item2;
                var processId = result.Item1;
                try
                {
                    Source.TraceEvent(TraceEventType.Information, 0, Resources.Program_Main_Opening__0_, solutionFileName);
                    dte.Solution.Open(solutionFileName);

                    Source.TraceEvent(TraceEventType.Verbose, 0, Resources.Program_Main_Finding_and_processing___tt_templates___);
                    var firstError =
                        FindTemplates(Path.GetDirectoryName(solutionFileName))
                            .Select(t => Tuple.Create(t, ProcessTemplate(dte, t, options)))
                            .FirstOrDefault(tuple => tuple.Item2.Count > 0);

                    if (firstError != null)
                    {
                        Source.TraceEvent(TraceEventType.Warning, 0, Resources.Program_Main_FAILED_to_process___0__,
                            firstError.Item1);
                        foreach (var error in firstError.Item2)
                        {
                            Source.TraceEvent(TraceEventType.Error, 0, Resources.Program_Main_, error);
                        }
                        return false;
                    }

                    Source.TraceEvent(TraceEventType.Information, 0, Resources.Program_Main_Everything_worked_);
                    return true;
                }
                finally
                {
                    Process process = null;
                    if (processId > 0)
                    {
                        process = Process.GetProcessById(processId);
                    }
                    dte.Quit();

                    // Makes no sense to wait when the process already exited, or when we have no processId to kill.
                    int i = 0;
                    while (i < 10 && process != null && !process.HasExited)
                    {
                        Thread.Sleep(1000);
                        i++;
                    }
                    if (process != null && !process.HasExited)
                    {
                        process.Kill();
                    }
                }
            }
        }
    }
    
}