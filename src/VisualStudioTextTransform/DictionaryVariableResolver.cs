using System;
using System.Collections.Generic;
using AIT.Tools.VisualStudioTextTransform.Properties;

namespace AIT.Tools.VisualStudioTextTransform
{
    /// <summary>
    /// Simple resolver strategy by using a dictionary.
    /// </summary>
    public class DictionaryVariableResolver : IVariableResolver
    {
        private readonly IDictionary<string, string> _dictionary;

        /// <summary>
        /// Creates a new instance of the <see cref="DictionaryVariableResolver"/> class.
        /// </summary>
        /// <param name="dictionary"></param>
        private DictionaryVariableResolver(IDictionary<string, string> dictionary)
        {
            _dictionary = dictionary;
        }

        /// <summary>
        /// Try to resolve the given variable (return a list of possible paths)
        /// </summary>
        /// <param name="variable">the variable to resolve.</param>
        /// <returns></returns>
        public IEnumerable<string> ResolveVariable(string variable)
        {
            string value;
            if (_dictionary.TryGetValue(variable, out value))
            {
                return new[] { value };
            }

            return new string [0];
        }

        /// <summary>
        /// Create a new resolver from a command line properties string.
        /// </summary>
        /// <param name="propertyValues">the string given via command line, for example 'test:C:\Path1;path2:C:\OtherPath'</param>
        /// <returns>the resolver instance.</returns>
        public static DictionaryVariableResolver FromOptionsValue(string propertyValues)
        {
            var dict = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(propertyValues))
            {
                return new DictionaryVariableResolver(dict);
            }

            var properties = propertyValues.Split(';');
            foreach (var property in properties)
            {
                var s = property.IndexOf(':');
                if (s < 0)
                {
                    throw new ArgumentOutOfRangeException("propertyValues", propertyValues, Resources.DictionaryVariableResolver_FromOptionsString_Expected_propertiesString_to_contain_a_colon_for_every_entry);
                }

                var propertyName = property.Substring(0, s);
                var propertyValue = property.Substring(s + 1);
                dict[propertyName] = propertyValue;
            }

            return new DictionaryVariableResolver(dict);
        }
    }
}