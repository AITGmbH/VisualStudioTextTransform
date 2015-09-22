using System;
using System.Collections.Generic;
using System.Globalization;
using AIT.VisualStudio.Controlling;

namespace AIT.Tools.VisualStudioTextTransform
{
    public class TransformTemplatesFeature : IServiceFeature
    {
        private readonly VisualStudioInternalTransformHelper _helper;

        /// <summary>
        /// Initializes a new instance of the <see cref="TransformTemplatesFeature" /> class.
        /// </summary>
        public TransformTemplatesFeature()
        {
            this._helper = new VisualStudioInternalTransformHelper();
        }

        /// <summary>
        /// Execute the given feature.
        /// </summary>
        /// <param name="dataDictionary">the parameter for the feature.</param>
        /// <returns/>
        public FeatureDataDictionary ExecuteFeature(FeatureDataDictionary dataDictionary)
        {
            if (dataDictionary == null)
            {
                throw new ArgumentNullException("dataDictionary");
            }

            switch (dataDictionary.Data.ToUpperInvariant())
            {
                case "TRANSFORM_SOLUTION":
                    {
                        var targetDirectory = dataDictionary["targetDirectory"].Data;
                        var solutionFile = dataDictionary["solutionFile"].Data;
                        var result = this._helper.TransformTemplates(solutionFile, new Options {TargetDir = targetDirectory });

                        return new FeatureDataDictionary(result ? "true" : "false", null);
                    }

                default:
                    {
                        throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unknown function for NuGet feature: '{0}'", dataDictionary.Data));
                    }
            }
        }
    }
}