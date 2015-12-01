using System.Collections.Generic;
using AIT.VisualStudio.Controlling;

namespace AIT.Tools.VisualStudioTextTransform
{
    /// <summary>
    ///     Helper to extend the <see cref="IVisualStudioHelperService" /> interface.
    ///     See also the <see cref="VisualStudioHostExtensions" /> class.
    /// </summary>
    public class TransformTemplatesFeatureInvoker
    {
        private readonly string _handle;
        private readonly IVisualStudioHelperService _service;

        internal TransformTemplatesFeatureInvoker(IVisualStudioHelperService service, string handle)
        {
            _service = service;
            _handle = handle;
        }

        /// <summary>
        ///     Install the given packages to the given project
        /// </summary>
        public bool TransformTemplates(string solutionFile, Options options)
        {
            var dict = new Dictionary<string, FeatureDataDictionary>();
            dict["solutionFile"] = new FeatureDataDictionary(solutionFile, null);
            dict["targetDirectory"] = new FeatureDataDictionary(options.TargetDir, null);
            dict["properties"] = new FeatureDataDictionary(options.Properties, null);

            var result = _service.ExecuteFeature(_handle, new FeatureDataDictionary("TRANSFORM_SOLUTION", dict));
            return bool.Parse(result.Data);
        }
    }
}