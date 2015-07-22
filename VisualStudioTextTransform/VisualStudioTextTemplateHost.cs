using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using AIT.Tools.VisualStudioTextTransform.Properties;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.TextTemplating;

namespace AIT.Tools.VisualStudioTextTransform
{
    /// <summary>
    /// See https://msdn.microsoft.com/en-us/library/bb126579.aspx for more details
    /// </summary>
    public class VisualStudioTextTemplateHost : ITextTemplatingEngineHost, IServiceProvider
    {
        private const string DefaultFileExtension = ".txt";
        private const string FileProtocol = "file:///";
        private static readonly TraceSource _source = new TraceSource("AIT.Tools.VisualStudioTextTransform");

        private readonly string _templateFile;
        private readonly string _templateDir;
        private readonly DTE2 _dte;
        private CompilerErrorCollection _errors;
        private string _fileExtension = DefaultFileExtension;
        private Encoding _outputEncoding = Encoding.UTF8;
        
        /// <summary>
        /// /
        /// </summary>
        /// <param name="templateFile"></param>
        /// <param name="dte"></param>
        public VisualStudioTextTemplateHost(string templateFile, DTE2 dte)
        {
            if (templateFile == null)
            {
                throw new ArgumentNullException("templateFile");
            }

            if (dte == null)
            {
                throw new ArgumentNullException("dte");
            }
            _templateFile = templateFile;
            _dte = dte;
            _templateDir = Path.GetFullPath(Path.GetDirectoryName(templateFile));
        }

        private string ReplaceProjectVarsPrivate(string path)
        {
            if (path.StartsWith(FileProtocol, StringComparison.Ordinal))
            {
                path = path.Substring(FileProtocol.Length);
            }
            var templateFileItem = _dte.Solution.FindProjectItem(_templateFile);
            var project = templateFileItem.ContainingProject;
            var projectDir = Path.GetDirectoryName(project.FullName);
            string outDir = project.ConfigurationManager.ActiveConfiguration
                .Properties.Item("OutputPath").Value.ToString();
            return path
                .Replace("$(ProjectDir)", projectDir + Path.DirectorySeparatorChar)
                .Replace("$(SolutionDir)", Path.GetDirectoryName(_dte.Solution.FullName) + Path.DirectorySeparatorChar)
                .Replace("$(TargetDir)", Path.Combine(projectDir, outDir + Path.DirectorySeparatorChar));
        }

        private string ResolvePathPrivate(string path)
        {
            path = ReplaceProjectVarsPrivate(path);
            if (string.IsNullOrEmpty(path))
            {
                return _templateDir;
            }

            var combinedPath = Path.Combine(_templateDir, path);
            string relativePath = null;
            try
            {
                relativePath = Path.GetDirectoryName(combinedPath);
            }
            catch (PathTooLongException)
            {
                _source.TraceEvent(TraceEventType.Error, 0, "Path \"{0}\" is to long and has been ignored.", combinedPath);
            }
            var paths = new[]
            {
                // First check if we have a full path here
                path,
                // check relative to template file
                relativePath
                // TODO: Add more (GAC?, configured by CLI?)
            };
            _source.TraceEvent(TraceEventType.Verbose, 0, Resources.VisualStudioTextTemplateHost_ResolvePathPrivate_resolving__0_, path);

            var result = paths.FirstOrDefault(File.Exists);
            if (result != null)
            {
                result = Path.GetFullPath(result);
                _source.TraceEvent(TraceEventType.Verbose, 0, Resources.VisualStudioTextTemplateHost_ResolvePathPrivate_found__0_, result);
                return result;
            }
            return path;
        }


        /// <summary>
        /// Path and file name of the template currently processing
        /// </summary>
        public string TemplateFile
        {
            get { return _templateFile; }
        }
        
        /// <summary>
        /// Default Fallback if not specified by the file
        /// </summary>
        public string FileExtension
        {
            get { return _fileExtension; }
        }

        /// <summary>
        /// Encoding of the Output file
        /// </summary>
        public Encoding FileEncoding
        {
            get { return _outputEncoding; }
        }

        /// <summary>
        /// /
        /// </summary>
        public CompilerErrorCollection Errors
        {
            get { return _errors; }
        }

        /// <summary>
        /// /
        /// </summary>
        public IList<string> StandardAssemblyReferences
        {
            get { return new[] {typeof (Uri).Assembly.Location}; }
        }

        /// <summary>
        /// /
        /// </summary>
        public IList<string> StandardImports
        {
            get { return new[] {"System"}; }
        }

        /// <summary>
        /// The engine calls this method based on the optional include directive
        /// if the user has specified it in the text template.
        /// </summary>
        public bool LoadIncludeText(string requestFileName, out string content, out string location)
        {
            content = string.Empty;
            location = string.Empty;
            var resolved = ResolvePathPrivate(requestFileName);
            if (File.Exists(resolved))
            {
                location = Path.GetFullPath(resolved);
                content = File.ReadAllText(resolved);
                return true;
            }

            // TODO: Find file (use dte)
            return false;
        }

        /// <summary>
        /// Called by the Engine to enquire about 
        /// the processing options you require. 
        /// If you recognize that option, return an 
        /// appropriate value. 
        /// Otherwise, pass back NULL.
        /// </summary>
        public object GetHostOption(string optionName)
        {
            switch (optionName)
            {
                case "CacheAssemblies":
                {
                    return true;
                }
                default:
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// The engine calls this method to resolve assembly references used in
        /// the generated transformation class project and for the optional 
        /// assembly directive if the user has specified it in the text template.
        /// This method can be called 0, 1, or more times.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes",
            Justification = "We print the exception and don't really care if we can load the assembly or not."), 
         SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", 
            MessageId = "System.Reflection.Assembly.LoadWithPartialName",
            Justification = "There is no satisfying workaround.")]
        public string ResolveAssemblyReference(string assemblyReference)
        {
            var relative = ResolvePathPrivate(assemblyReference);
            if (File.Exists(relative))
            {
                return relative;
            }

            // try to load the assembly
            try
            {
                // Well yes it's obsolete, but see http://stackoverflow.com/questions/11659594/load-latest-assembly-version-dynamically-from-gac
                // for the alternatives 
                // - use all versions -> not possible here
                // - PInvoke -> not Xplat
                // - Specifying the GAC directories directly -> bad
                var ass = Assembly.LoadWithPartialName(assemblyReference);

                if (ass != null && ass.Location != null)
                {
                    return ass.Location;
                }

                throw new ArgumentException(Resources.VisualStudioTextTemplateHost_ResolveAssemblyReference_we_could_load_the_given_assembly_but_cannot_resolve_it_to_a_path_);
            }
            catch (Exception e)
            {
                _source.TraceEvent(TraceEventType.Verbose, 0, Resources.VisualStudioTextTemplateHost_ResolveAssemblyReference_Error__Could_not_load_Assembly___0_, e);
                return assemblyReference;
            }
        }

        /// <summary>
        /// The engine calls this method based on the directives the user has 
        /// specified in the text template.
        /// This method can be called 0, 1, or more times.
        /// </summary>
        [SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", MessageId = "System.String.ToUpperInvariant",
            Justification = "Remove this SuppressMessage as soon as we support a processor directive.")]
        public Type ResolveDirectiveProcessor(string processorName)
        {
            if (processorName == null)
            {
                throw new ArgumentNullException("processorName");
            }

            switch (processorName.ToUpperInvariant())
            {
                default:
                {
                    throw new ArgumentException(Resources.VisualStudioTextTemplateHost_ResolveDirectiveProcessor_Processor_Directive_is_unknown_);
                }
            }
        }
        /// <summary>
        /// A directive processor can call this method if a file name does not 
        /// have a path.
        /// The host can attempt to provide path information by searching 
        /// specific paths for the file and returning the file and path if found.
        /// This method can be called 0, 1, or more times.
        /// </summary>
        public string ResolvePath(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException("path", Resources.VisualStudioTextTemplateHost_ResolvePath_the_file_name_cannot_be_null);
            }
            return ResolvePathPrivate(path);
        }

        /// <summary>
        /// If a call to a directive in a text template does not provide a value
        /// for a required parameter, the directive processor can try to get it
        /// from the host by calling this method.
        /// This method can be called 0, 1, or more times.
        /// </summary>
        public string ResolveParameterValue(string directiveId, string processorName, string parameterName)
        {
            if (directiveId == null)
            {
                throw new ArgumentNullException("directiveId", Resources.VisualStudioTextTemplateHost_ResolveParameterValue_the_directiveId_cannot_be_null);
            }
            if (processorName == null)
            {
                throw new ArgumentNullException("processorName", Resources.VisualStudioTextTemplateHost_ResolveParameterValue_the_processorName_cannot_be_null);
            }
            if (parameterName == null)
            {
                throw new ArgumentNullException("parameterName", Resources.VisualStudioTextTemplateHost_ResolveParameterValue_the_parameterName_cannot_be_null);
            }
            //Code to provide "hard-coded" parameter values goes here.
            //This code depends on the directive processors this host will interact with.
            //If we cannot do better, return the empty string.
            return string.Empty;
        }

        /// <summary>
        /// /
        /// </summary>
        /// <param name="extension"></param>
        public void SetFileExtension(string extension)
        {
            _fileExtension = extension;
        }

        /// <summary>
        /// /
        /// </summary>
        /// <param name="encoding"></param>
        /// <param name="fromOutputDirective"></param>
        public void SetOutputEncoding(Encoding encoding, bool fromOutputDirective)
        {
            _outputEncoding = encoding;
        }

        /// <summary>
        /// /
        /// </summary>
        /// <param name="errors"></param>
        public void LogErrors(CompilerErrorCollection errors)
        {
            _errors = errors;
        }

        /// <summary>
        /// /
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        public AppDomain ProvideTemplatingAppDomain(string content)
        {
            // return the current domain as we expect to be a short lived application
            // please read the notes (https://msdn.microsoft.com/en-us/library/bb126579.aspx)
            // if you use this code snippet on a long lived application.
            return AppDomain.CurrentDomain;
        }

        /// <summary>
        /// /
        /// </summary>
        /// <param name="serviceType"></param>
        /// <returns></returns>
        public object GetService(Type serviceType)
        {
            _source.TraceEvent(TraceEventType.Verbose, 0, Resources.VisualStudioTextTemplateHost_GetService_Service_request_of_type___0_, serviceType);
            if (serviceType == typeof (DTE) || serviceType == typeof (DTE2))
            {
                _source.TraceEvent(TraceEventType.Verbose, 0, Resources.VisualStudioTextTemplateHost_GetService_Returning_DTE_instance_);
                return _dte;
            }

            return null;
        }
    }
}