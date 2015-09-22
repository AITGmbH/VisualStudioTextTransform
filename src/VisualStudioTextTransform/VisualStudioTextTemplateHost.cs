using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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
    /// <see href="https://msdn.microsoft.com/en-us/library/bb126579.aspx">See here</see> for more details 
    /// </summary>
    public class VisualStudioTextTemplateHost : ITextTemplatingEngineHost, IServiceProvider
    {
        private const string DefaultFileExtension = ".txt";
        private const string FileProtocol = "file:///";
        private static readonly TraceSource Source = new TraceSource("AIT.Tools.VisualStudioTextTransform");

        private readonly string _templateFile;
        private readonly string _templateDir;
        private readonly DTE2 _dte;
        private readonly IVariableResolver _resolver;
        private CompilerErrorCollection _errors;
        private string _fileExtension = DefaultFileExtension;
        private Encoding _outputEncoding = Encoding.UTF8;

        /// <summary>
        /// Initializes a new instance of the <see cref="VisualStudioTextTemplateHost"/> class.
        /// </summary>
        /// <param name="templateFile">the path to the template file.</param>
        /// <param name="dte">the <see cref="DTE2"/> instance.</param>
        /// <param name="resolver">the resolver to use.</param>
        public VisualStudioTextTemplateHost(string templateFile, DTE2 dte, IVariableResolver resolver)
        {
            if (string.IsNullOrEmpty(templateFile))
            {
                throw new ArgumentNullException("templateFile");
            }

            if (dte == null)
            {
                throw new ArgumentNullException("dte");
            }
            if (resolver == null)
            {
                throw new ArgumentNullException("resolver");
            }
            _templateFile = templateFile;
            
            _dte = dte;
            _resolver = resolver;
            var directoryName = Path.GetDirectoryName(templateFile);
            Debug.Assert(directoryName != null, "directoryName != null, don't expect templateFile to be a root directory!");
            _templateDir = Path.GetFullPath(directoryName);
        }

        private IEnumerable<string> ReplaceProjectVar(string path, string variable)
        {
            foreach (var resolvedVariable in _resolver.ResolveVariable(variable))
            {
                yield return path.Replace(string.Format(CultureInfo.InvariantCulture, "$({0})", variable), resolvedVariable);
            }
        } 

        private IEnumerable<string> ReplaceProjectVarsPrivate(string path)
        {
            if (path.StartsWith(FileProtocol, StringComparison.Ordinal))
            {
                path = path.Substring(FileProtocol.Length);
            }

            return
                ReplaceProjectVar(path, "ProjectDir")
                .SelectMany(p => ReplaceProjectVar(p, "SolutionDir"))
                .SelectMany(p => ReplaceProjectVar(p, "TargetDir"));
        }


        private IEnumerable<string> PossibleFullPaths(string path)
        {
            // check relative to template file
            yield return Path.Combine(_templateDir, path);
            
            // First check if we have a full path here
            yield return path;
            // TODO: Add more (GAC?, configured by CLI?)
        }


        private string ResolveFilePathPrivate(string path)
        {
            var paths = ResolveAllPathsPrivate(path);

            var result = paths.FirstOrDefault(File.Exists);
            if (result != null)
            {
                result = Path.GetFullPath(result);
                Source.TraceEvent(TraceEventType.Verbose, 0, Resources.VisualStudioTextTemplateHost_ResolvePathPrivate_found__0_, result);
                return result;
            }
            return path;
        }

        private IEnumerable<string> ResolveAllPathsPrivate(string path)
        {
            Source.TraceEvent(TraceEventType.Verbose, 0, Resources.VisualStudioTextTemplateHost_ResolvePathPrivate_resolving__0_, path);
            var possiblePaths = ReplaceProjectVarsPrivate(path);

            // Distinct because Path.Combine ignores the first parameter when the second one is a full path -> duplicates.
            var paths = possiblePaths.SelectMany(PossibleFullPaths).Distinct().ToList();

            foreach (var possiblePath in paths)
            {
                Source.TraceEvent(TraceEventType.Verbose, 0, "Considering {0}", possiblePath);
            }
            return paths;
        }

        /// <summary>
        /// Path and file name of the template currently processing
        /// </summary>
        public string TemplateFile
        {
            get { return _templateFile; }
        }
        
        /// <summary>
        /// Default fall-back if not specified by the file
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
        /// The compiler errors.
        /// </summary>
        public CompilerErrorCollection Errors
        {
            get { return _errors; }
        }

        /// <summary>
        /// The standard assembly references.
        /// </summary>
        public IList<string> StandardAssemblyReferences
        {
            get { return new[] {typeof (Uri).Assembly.Location}; }
        }

        /// <summary>
        /// the standard imports (using directives)
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
            var resolved = ResolveFilePathPrivate(requestFileName);
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
            var relative = ResolveFilePathPrivate(assemblyReference);
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
                // - PInvoke -> bad and not cross plat
                // - Specifying the GAC directories directly -> bad
#pragma warning disable 618
                var ass = Assembly.LoadWithPartialName(assemblyReference);
#pragma warning restore 618

                if (ass != null && !string.IsNullOrEmpty(ass.Location))
                {
                    Source.TraceEvent(TraceEventType.Verbose, 0, "Could resolve the given string to an assembly: {0}", ass.Location);
                    return ass.Location;
                }

                throw new ArgumentException(Resources.VisualStudioTextTemplateHost_ResolveAssemblyReference_we_could_load_the_given_assembly_but_cannot_resolve_it_to_a_path_);
            }
            catch (Exception e)
            {
                Source.TraceEvent(TraceEventType.Verbose, 0, Resources.VisualStudioTextTemplateHost_ResolveAssemblyReference_Error__Could_not_load_Assembly___0_, e);
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

            var resolveAllPathsPrivate = ResolveAllPathsPrivate(path).ToList();
            var resolvedPath = resolveAllPathsPrivate.FirstOrDefault(p => Directory.Exists(p) || File.Exists(p));
            if (resolvedPath != null)
            {
                Source.TraceEvent(TraceEventType.Verbose, 1, "Using existing path: {0}", resolvedPath);
                return resolvedPath;
            }

            throw new ArgumentException("Could not resolve path: " + path);
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
        /// Set the current file extension
        /// </summary>
        /// <param name="extension">the extension to use</param>
        public void SetFileExtension(string extension)
        {
            _fileExtension = extension;
        }

        /// <summary>
        /// Set the current output encoding.
        /// </summary>
        /// <param name="encoding">the encoding to use.</param>
        /// <param name="fromOutputDirective">true if called because of an output directive.</param>
        public void SetOutputEncoding(Encoding encoding, bool fromOutputDirective)
        {
            _outputEncoding = encoding;
        }

        /// <summary>
        /// Log the given errors.
        /// </summary>
        /// <param name="errors">the compiler errors.</param>
        public void LogErrors(CompilerErrorCollection errors)
        {
            _errors = errors;
        }

        /// <summary>
        /// Provide an AppDomain for the template code.
        /// </summary>
        /// <param name="content">the content of the template.</param>
        /// <returns></returns>
        public AppDomain ProvideTemplatingAppDomain(string content)
        {
            // return the current domain as we expect to be a short lived application
            // please read the notes (https://msdn.microsoft.com/en-us/library/bb126579.aspx)
            // if you use this code snippet on a long lived application.
            return AppDomain.CurrentDomain;
        }

        /// <summary>
        /// Returns the requested service type.
        /// </summary>
        /// <param name="serviceType">the type of the service we should return.</param>
        /// <returns></returns>
        public object GetService(Type serviceType)
        {
            Source.TraceEvent(TraceEventType.Verbose, 0, Resources.VisualStudioTextTemplateHost_GetService_Service_request_of_type___0_, serviceType);
            if (serviceType == typeof (DTE) || serviceType == typeof (DTE2))
            {
                Source.TraceEvent(TraceEventType.Verbose, 0, Resources.VisualStudioTextTemplateHost_GetService_Returning_DTE_instance_);
                return _dte;
            }

            return null;
        }
    }
}