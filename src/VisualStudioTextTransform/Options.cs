using System.Collections.Generic;
using CommandLine;

namespace AIT.Tools.VisualStudioTextTransform
{
    /// <summary>
    /// A options class for the Command-Line parser library.
    /// </summary>
    public class Options
    {
        /// <summary>
        /// An overwrite for the Target-Directory <code>msbuild</code> variable.
        /// </summary>
        [Option('t', "targetdir", DefaultValue = null, Required = false, HelpText = "Set a custom TargetDir reference.")]
        public string TargetDir
        {
            get;
            set;
        }

        /// <summary>
        /// Set properties to resolve paths from within the template.
        /// </summary>
        [Option('p', "properties", DefaultValue = null, Required = false, HelpText = "Set custom properties - split by ';' - to help resolve paths (property names cannot contain ':' or ';'): Example 'path1:C:\\path1;path2:C:\\path2'")]
        public string Properties
        {
            get;
            set;
        }
    }
}