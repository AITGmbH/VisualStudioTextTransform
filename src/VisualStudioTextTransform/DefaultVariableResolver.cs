using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using EnvDTE80;

namespace AIT.Tools.VisualStudioTextTransform
{
    /// <summary>
    /// A default implementation of IVariableResolver returning simple paths.
    /// </summary>
    public class DefaultVariableResolver : IVariableResolver
    {
        private readonly string _projectDir;
        private readonly string _solutionDir;
        private readonly string _targetDir;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultVariableResolver"/> class.
        /// </summary>
        /// <param name="projectDir">the project directory</param>
        /// <param name="solutionDir">the solution directory</param>
        /// <param name="targetDir">the target directory.</param>
        public DefaultVariableResolver(string projectDir, string solutionDir, string targetDir)
        {
            _projectDir = projectDir;
            _solutionDir = solutionDir;
            _targetDir = targetDir;
        }

        /// <summary>
        /// returns the target directory.
        /// </summary>
        public string TargetDir
        {
            get
            {
                return _targetDir;
            }
        }

        /// <summary>
        /// returns the solution directory.
        /// </summary>
        public string SolutionDir
        {
            get
            {
                return _solutionDir;
            }
        }

        /// <summary>
        /// returns the project directory.
        /// </summary>
        public string ProjectDir
        {
            get
            {
                return _projectDir;
            }
        }

        /// <summary>
        /// Create a default instance by fetching the paths from the solution.
        /// </summary>
        /// <param name="dte">the <see cref="DTE2"/> instance.</param>
        /// <param name="templateFile">the template file.</param>
        /// <returns></returns>
        public static DefaultVariableResolver CreateFromDte(DTE2 dte, string templateFile)
        {
            if (dte == null)
            {
                throw new ArgumentNullException("dte");
            }
            if (templateFile == null)
            {
                throw new ArgumentNullException("templateFile");
            }

            var templateFileItem = dte.Solution.FindProjectItem(templateFile);
            if (templateFileItem == null)
            { // This item is not part of the solution
                throw new TemplateNotPartOfSolutionException("The template-file " + templateFile + " was not found in the given solution!");
            }
            var project = templateFileItem.ContainingProject;
            var projectDir = Path.GetDirectoryName(project.FullName);
            string outDir = project.ConfigurationManager.ActiveConfiguration
                .Properties.Item("OutputPath").Value.ToString();
            Debug.Assert(projectDir != null, "projectDir != null, did not expect project.FullName to be a root directory!");
            var targetDir = Path.GetFullPath(Path.Combine(projectDir, outDir));
            return new DefaultVariableResolver(projectDir, Path.GetDirectoryName(dte.Solution.FullName), targetDir);
        }

        /// <summary>
        /// Return a new instance with a changed Target-Directory.
        /// </summary>
        /// <param name="targetDir">the target directory.</param>
        /// <returns></returns>
        public DefaultVariableResolver WithTargetDir(string targetDir)
        {
            return new DefaultVariableResolver(_projectDir, _solutionDir, targetDir);
        }

        /// <summary>
        /// The simple resolution strategy of this instance.
        /// </summary>
        /// <param name="variable">the variable to resolve.</param>
        /// <returns></returns>
        public string SimpleResolveVariable(string variable)
        {
            switch (variable)
            {
                case "ProjectDir":
                {
                    return _projectDir;
                }
                case "SolutionDir":
                {
                    return _solutionDir;
                }
                case "TargetDir":
                {
                    return _targetDir;
                }
            }
            throw new ArgumentOutOfRangeException(
                string.Format(CultureInfo.CurrentUICulture, "Unknown variable {0}", variable));
        }

        /// <summary>
        /// Resolves a given variable.
        /// </summary>
        /// <param name="variable">the variable to resolve.</param>
        /// <returns></returns>
        public IEnumerable<string> ResolveVariable(string variable)
        {
            if (!string.IsNullOrEmpty(variable))
            {
                var resolved = SimpleResolveVariable(variable);
                if (resolved != null)
                {
                    yield return resolved + Path.DirectorySeparatorChar;
                }
            }
        }
    }
}