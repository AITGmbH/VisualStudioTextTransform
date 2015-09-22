using AIT.VisualStudio.Controlling;
using EnvDTE80;

namespace AIT.Tools.VisualStudioTextTransform
{
    internal class VisualStudioInternalTransformHelper
    {
        private readonly DTE2 _dte;

        public VisualStudioInternalTransformHelper()
        {
            var helper = new VisualStudioInternalHelper();

            _dte = helper.DTE;
        }

        public bool TransformTemplates(string solutionFileName, Options options)
        {
            return TemplateProcessor.ProcessSolution(_dte, solutionFileName, options);
        }
    }
}