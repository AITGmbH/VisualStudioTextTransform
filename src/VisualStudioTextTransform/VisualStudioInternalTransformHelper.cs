using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AIT.VisualStudio.Controlling;
using EnvDTE80;

namespace AIT.Tools.VisualStudioTextTransform
{
    internal class VisualStudioInternalTransformHelper
    {
        private static readonly TraceSource Source = LoggingHelper.CreateSource("AIT.Tools.VisualStudioTextTransform");
        private readonly VisualStudioInternalHelper _helper;
        private readonly DTE2 _dte;

        public VisualStudioInternalTransformHelper()
        {
            this._helper = new VisualStudioInternalHelper();

            this._dte = this._helper.DTE;
        }


        public bool TransformTemplates(string solutionFileName, Options options)
        {
            return TemplateProcessor.ProcessSolution(_dte, solutionFileName, options);
        }
    }
}
