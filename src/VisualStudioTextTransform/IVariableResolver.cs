using System.Collections.Generic;

namespace AIT.Tools.VisualStudioTextTransform
{
    /// <summary>
    /// Interface to resolve <code>msbuild</code> variables.
    /// </summary>
    public interface IVariableResolver
    {
        /// <summary>
        /// Try to resolve the given variable (return a list of possible paths)
        /// </summary>
        /// <param name="variable">the variable to resolve.</param>
        /// <returns></returns>
        IEnumerable<string> ResolveVariable(string variable);
    }
}