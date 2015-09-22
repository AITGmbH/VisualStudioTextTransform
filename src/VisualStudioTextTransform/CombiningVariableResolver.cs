using System.Collections.Generic;
using System.Linq;

namespace AIT.Tools.VisualStudioTextTransform
{
    /// <summary>
    /// Combines the given resolvers
    /// </summary>
    public class CombiningVariableResolver : IVariableResolver
    {
        private readonly IEnumerable<IVariableResolver> _resolvers;

        /// <summary>
        /// Combines various <see cref="IVariableResolver"/> instances.
        /// </summary>
        /// <param name="resolvers">the list of resolvers.</param>
        public CombiningVariableResolver(IEnumerable<IVariableResolver> resolvers)
        {
            _resolvers = resolvers.ToList(); // Save copy
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CombiningVariableResolver"/> class.
        /// </summary>
        /// <param name="resolver1">the first resolver</param>
        /// <param name="resolver2">the second resolver</param>
        public CombiningVariableResolver(IVariableResolver resolver1, IVariableResolver resolver2)
            : this(new[] { resolver1, resolver2 }) { }

        /// <summary>
        /// Try to resolve the given variable (return a list of possible paths)
        /// </summary>
        /// <param name="variable">the variable to resolve.</param>
        /// <returns></returns>
        public IEnumerable<string> ResolveVariable(string variable)
        {
            foreach (var resolver in _resolvers)
            {
                foreach (var resolvedVar in resolver.ResolveVariable(variable))
                {
                    yield return resolvedVar;
                }
            }
        }
    }
}