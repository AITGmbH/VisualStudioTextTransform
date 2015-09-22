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
        {
        }

        /// <summary>
        /// Initializes a new instance of the TemplateNotPartOfSolutionException class.
        /// </summary>
        /// <param name="message">the message of the exception</param>
        public TemplateNotPartOfSolutionException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the TemplateNotPartOfSolutionException class.
        /// </summary>
        /// <param name="message">the message of the exception</param>
        /// <param name="innerException">the underlying issue.</param>
        public TemplateNotPartOfSolutionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the TemplateNotPartOfSolutionException class.
        /// </summary>
        /// <param name="info">the serialization info.</param>
        /// <param name="context">the streaming context.</param>
        protected TemplateNotPartOfSolutionException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            
        }
    }
}