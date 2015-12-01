using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using AIT.Tools.VisualStudioTextTransform.Properties;
using AIT.VisualStudio.Controlling;
using EnvDTE80;
using Engine = Microsoft.VisualStudio.TextTemplating.Engine;

namespace AIT.Tools.VisualStudioTextTransform
{
    /// <summary>
    ///     Utility class to process t4 templates.
    /// </summary>
    public static class TemplateProcessor
    {
        private static readonly TraceSource Source = LoggingHelper.CreateSource("AIT.Tools.VisualStudioTextTransform");

        /// <summary>
        ///     Process the given template in memory
        /// </summary>
        /// <param name="dte">the <see cref="DTE2" /> instance.</param>
        /// <param name="templateFileName">the path of the template.</param>
        /// <param name="resolver">the resolver to use.</param>
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
                var defaultNamespace = project.Properties.Item("DefaultNamespace").Value.ToString();
                Debug.Assert(templateFileName.StartsWith(projectDir, StringComparison.OrdinalIgnoreCase), "Template file-name is not within the project directory.");

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
        ///     Process the given template by creating a resolver from information provided by the <see cref="DTE2" /> instance.
        /// </summary>
        /// <param name="dte">the <see cref="DTE2" /> instance.</param>
        /// <param name="templateFileName">the path to the template file.</param>
        /// <param name="options">the options to use.</param>
        /// <returns>null if we could not process the template and an error-collection of the compilation otherwise.</returns>
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

            IVariableResolver resolver = new CombiningVariableResolver(DictionaryVariableResolver.FromOptionsValue(options.Properties), defaultResolver);
            Source.TraceEvent(TraceEventType.Information, 1, "Default TargetDir {0} will be used", defaultResolver.TargetDir);
            Source.TraceEvent(TraceEventType.Information, 1, "Default SolutionDir {0} will be used", defaultResolver.SolutionDir);
            Source.TraceEvent(TraceEventType.Information, 1, "Default ProjectDir {0} will be used", defaultResolver.ProjectDir);

            if (!string.IsNullOrEmpty(options.TargetDir))
            {
                if (Directory.Exists(options.TargetDir))
                {
                    Source.TraceEvent(TraceEventType.Information, 1, "TargetDir {0} will be added ", options.TargetDir);
                    resolver = new CombiningVariableResolver(
                            new DefaultVariableResolver(null, null, options.TargetDir),
                            resolver);
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

            if (host.Errors.HasErrors)
            {
                foreach (var error in host.Errors)
                {
                    var msg = error.ToString();

                    var ind = msg.IndexOf("System.UnauthorizedAccessException");
                    if (ind >= 0)
                    {
                        Source.TraceEvent(TraceEventType.Warning, 0, "trying again because System.UnauthorizedAccessException was detected: {0}", msg);

                        // This could be a case where the template tried to write to the file beforehand.
                        // We already changed attributes above, but just to be sure:
                        var sub1 = msg.Substring(ind);
                        var fileStart = sub1.IndexOf('\'');
                        if (fileStart >= 0)
                        {
                            var fileStartingString = sub1.Substring(fileStart + 1);
                            var fileEnd = fileStartingString.IndexOf('\'');
                            if (fileEnd >= 0)
                            {
                                var filePath = fileStartingString.Substring(0, fileEnd);
                                Source.TraceEvent(TraceEventType.Information, 0, "make sure '{0}' is not readonly", filePath);
                                if (File.Exists(filePath))
                                {
                                    var attr = File.GetAttributes(filePath);
                                    File.SetAttributes(filePath, attr & ~FileAttributes.ReadOnly);
                                    File.Delete(filePath);
                                }
                            }
                        }

                        result = ProcessTemplateInMemory(dte, templateFileName, resolver);
                        host = result.Item2;
                        output = result.Item1;
                        break;
                    }
                }
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
        ///     Process all templates of the given solution-file.
        /// </summary>
        /// <param name="dte">the <see cref="DTE2" /> instance.</param>
        /// <param name="solutionFileName">the filename of the solution</param>
        /// <param name="options">the options to use for processing</param>
        /// <returns></returns>
        public static bool ProcessSolution(DTE2 dte, string solutionFileName, Options options)
        {
            Source.TraceEvent(TraceEventType.Information, 0, Resources.Program_Main_Opening__0_, solutionFileName);
            Source.TraceEvent(TraceEventType.Information, 0, "Version: {0}", Assembly.GetExecutingAssembly().GetName().Version);
            Source.TraceEvent(TraceEventType.Information, 0, "options.TargetDir: {0}", options.TargetDir);
            Source.TraceEvent(TraceEventType.Information, 0, "options.Properties: {0}", options.Properties);
            dte.Solution.Open(solutionFileName);

            Source.TraceEvent(TraceEventType.Verbose, 0, Resources.Program_Main_Finding_and_processing___tt_templates___);
            var firstError =
                FindTemplates(Path.GetDirectoryName(solutionFileName))
                    .Select(t =>
                            {
                                try
                                {
                                    return Tuple.Create(t, ProcessTemplate(dte, t, options));
                                }
                                catch (TemplateNotPartOfSolutionException)
                                {
                                    Source.TraceEvent(TraceEventType.Warning, 2, "The template found within the solution dir was not part of the given solution ({0}): {1}", solutionFileName, t);
                                    return null;
                                }
                            })
                    .Where(t => t != null)
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

        /// <summary>
        ///     Process all templates of the given solution-file.
        /// </summary>
        /// <param name="solutionFileName">the filename of the solution</param>
        /// <param name="options">the options to use for processing</param>
        /// <returns></returns>
        public static bool ProcessSolution(string solutionFileName, Options options)
        {
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

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
                    using (var controller = new VisualStudioController(dte))
                    {
                        var tt = controller.GetTextTransformFeature();
                        return tt.TransformTemplates(solutionFileName, options);
                    }
                    //return ProcessSolution(dte, solutionFileName, options);
                }
                finally
                {
                    DteHelper.CleanupDteInstance(processId, dte);
                }
            }
        }
    }
}