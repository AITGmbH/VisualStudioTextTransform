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


    }
}