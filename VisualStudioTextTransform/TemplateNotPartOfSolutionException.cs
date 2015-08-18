using System;
using System.Runtime.Serialization;

namespace AIT.Tools.VisualStudioTextTransform
{
    /// <summary>
    /// Is thrown when the given template is not part of the given solution.
    /// </summary>
    [Serializable]
    public class TemplateNotPartOfSolutionException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the TemplateNotPartOfSolutionException class.
        /// </summary>
        public TemplateNotPartOfSolutionException()
            : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the TemplateNotPartOfSolutionException class.
        /// </summary>
        /// <param name="message"></param>
        public TemplateNotPartOfSolutionException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the TemplateNotPartOfSolutionException class.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        public TemplateNotPartOfSolutionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the TemplateNotPartOfSolutionException class.
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        protected TemplateNotPartOfSolutionException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            
        }
    }
}