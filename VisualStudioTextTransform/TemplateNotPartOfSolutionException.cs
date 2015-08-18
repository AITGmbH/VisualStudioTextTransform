using System;

namespace AIT.Tools.VisualStudioTextTransform
{
    public class TemplateNotPartOfSolutionException : Exception
    {
        public TemplateNotPartOfSolutionException()
            : base()
        {
        }
        public TemplateNotPartOfSolutionException(string message)
            : base(message)
        {
        }
        public TemplateNotPartOfSolutionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}