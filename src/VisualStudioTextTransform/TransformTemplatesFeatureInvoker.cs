using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AIT.VisualStudio.Controlling;

namespace AIT.Tools.VisualStudioTextTransform
{
    public class TransformTemplatesFeatureInvoker
    {
        private readonly string _handle;
        private readonly IVisualStudioHelperService _service;

        internal TransformTemplatesFeatureInvoker(IVisualStudioHelperService service, string handle)
        {
            this._service = service;
            this._handle = handle;
        }

        /// <summary>
        /// Install the given packages to the given project
        /// </summary>
        public bool TransformTemplates(string solutionFile, string targetDirectory)
        {
            var dict = new Dictionary<string, FeatureDataDictionary>();
            dict["solutionFile"] = new FeatureDataDictionary(solutionFile, null);
            dict["targetDirectory"] = new FeatureDataDictionary(targetDirectory, null);

            var result = this._service.ExecuteFeature(this._handle, new FeatureDataDictionary("TRANSFORM_SOLUTION", dict));
            return bool.Parse(result.Data);
        }
    }
}
