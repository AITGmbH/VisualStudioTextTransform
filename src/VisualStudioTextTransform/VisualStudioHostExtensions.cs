using System;
using AIT.VisualStudio.Controlling;

namespace AIT.Tools.VisualStudioTextTransform
{
    /// <summary>
    /// A simple extension for the <see cref="IVisualStudioHelperService"/> class.
    /// </summary>
    public static class VisualStudioHostExtensions
    {
        /// <summary>
        /// Get the a instance to call NuGetFeature methods.
        /// </summary>
        /// <param name="service">the service to get the NuGetFeature from.</param>
        /// <returns></returns>
        public static TransformTemplatesFeatureInvoker GetTextTransformFeature(this IVisualStudioHelperService service)
        {
            if (service == null)
            {
                throw new ArgumentNullException("service");
            }

            var type = typeof(TransformTemplatesFeature);
            var assembly = type.Assembly;
            var handle = service.RegisterFeature(assembly.Location, type.FullName);
            return new TransformTemplatesFeatureInvoker(service, handle);
        }
    }
}