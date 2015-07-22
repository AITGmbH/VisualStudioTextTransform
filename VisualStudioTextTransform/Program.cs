﻿using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using AIT.Tools.VisualStudioTextTransform.Properties;
using Microsoft.VisualStudio.TextTemplating;

namespace AIT.Tools.VisualStudioTextTransform
{
    public static class Program
    {
        private static readonly TraceSource Source = new TraceSource("AIT.Tools.TransformVisualStudioTextTemplates");

        public static Tuple<string, VisualStudioTextTemplateHost> ProcessTemplateInMemory(EnvDTE80.DTE2 dte, string templateFileName)
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

        public static CompilerErrorCollection ProcessTemplate(EnvDTE80.DTE2 dte, string templateFileName)
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
        public static int Main(string[] argv)
        {
            try
            {
                return ExecuteMain(argv);
            }
            catch (Exception e)
            {
                Source.TraceEvent(TraceEventType.Critical, 1, Resources.Program_Main_Application_crashed_with___0_, e);
                return 1;
            }
        }

        private static int ExecuteMain(string[] argv)
        {
            if (argv.Length == 0)
            {
                throw new ArgumentException(Resources.Program_Main_you_must_provide_a_solution_file);
            }
            var solutionFileName = argv[0];
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
                var dteType = Type.GetTypeFromProgID("VisualStudio.DTE.12.0", true);
                var dte = (EnvDTE80.DTE2) Activator.CreateInstance(dteType, true);
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
                    dte.Quit();
                }
            }
        }
    }
}