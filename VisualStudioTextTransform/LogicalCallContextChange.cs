using System;
using System.Runtime.Remoting.Messaging;

namespace AIT.Tools.VisualStudioTextTransform
{
    /// <summary>
    /// Class to change the LogicalCallContext temporarily.
    /// </summary>
    public class LogicalCallContextChange : IDisposable
    {
        private readonly string _name;
        private readonly object _prevHint;

        /// <summary>
        /// /
        /// </summary>
        /// <param name="name"></param>
        /// <param name="newValue"></param>
        public LogicalCallContextChange(string name, object newValue)
        {
            _name = name;
            _prevHint = CallContext.LogicalGetData(name);
            CallContext.LogicalSetData(name, newValue);
        }

        /// <summary>
        /// /
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                CallContext.LogicalSetData(_name, _prevHint);
            }
        }
    }
}