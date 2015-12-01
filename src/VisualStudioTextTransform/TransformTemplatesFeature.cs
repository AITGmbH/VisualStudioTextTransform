using System;
using System.Globalization;
using AIT.VisualStudio.Controlling;

namespace AIT.Tools.VisualStudioTextTransform
{
    /// <summary>
    /// Service for AIT.VisualStudio.Controlling, to transform the templates within the visual studio process.
    /// </summary>
    public class TransformTemplatesFeature : IServiceFeature
    {
        private readonly VisualStudioInternalTransformHelper _helper;

        /// <summary>
        /// Initializes a new instance of the <see cref="TransformTemplatesFeature" /> class.
        /// </summary>
        public TransformTemplatesFeature()
        {
            _helper = new VisualStudioInternalTransformHelper();
        }

        /// <summary>
        /// Execute the given feature.
        /// </summary>
        /// <param name="dataDictionary">the parameter for the feature.</param>
        /// <returns/>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "NuGet")]
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
                        var properties = dataDictionary["properties"].Data;
                        var result = _helper.TransformTemplates(solutionFile, new Options { TargetDir = targetDirectory, Properties = properties });

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