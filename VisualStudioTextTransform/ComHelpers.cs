using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AIT.Tools.VisualStudioTextTransform
{
    /// <summary>
    /// Fix COM retry errors: http://www.viva64.com/en/b/0169/
    /// </summary>
    public class MessageFilter : MarshalByRefObject, NativeMethods.IMessageFilter, IDisposable
    {
        // ReSharper disable InconsistentNaming
        private const int SERVERCALL_ISHANDLED = 0;
        private const int PENDINGMSG_WAITNOPROCESS = 2;
        private const int SERVERCALL_RETRYLATER = 2;
        // ReSharper restore InconsistentNaming

        private readonly NativeMethods.IMessageFilter _oldFilter;

        /// <summary>
        /// /
        /// </summary>
        public MessageFilter()
        {
            var hr = NativeMethods.CoRegisterMessageFilter(this, out _oldFilter);
            if (hr < 0)
            {
                throw Marshal.GetExceptionForHR(hr);
            }
            Debug.Assert(hr >= 0, "CoRegisterMessageFilter failed.");
        }

        /// <summary>
        /// /
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// /
        /// </summary>
        /// <param name="isDisposing"></param>
        protected virtual void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                NativeMethods.IMessageFilter f;
                var hr = NativeMethods.CoRegisterMessageFilter(_oldFilter, out f);
                Debug.Assert(hr >= 0, "CoRegisterMessageFilter failed.");
            }
        }

        /// <summary>
        /// /
        /// </summary>
        /// <param name="dwCallType"></param>
        /// <param name="threadIdCaller"></param>
        /// <param name="dwTickCount"></param>
        /// <param name="lpInterfaceInfo"></param>
        /// <returns></returns>
        public int HandleInComingCall(int dwCallType, IntPtr threadIdCaller, int dwTickCount, IntPtr lpInterfaceInfo)
        {
            // Return the ole default (don't let the call through).
            return SERVERCALL_ISHANDLED;
        }

        /// <summary>
        /// /
        /// </summary>
        /// <param name="threadIDCallee"></param>
        /// <param name="dwTickCount"></param>
        /// <param name="dwRejectType"></param>
        /// <returns></returns>
        // ReSharper disable once InconsistentNaming
        public int RetryRejectedCall(IntPtr threadIDCallee, int dwTickCount, int dwRejectType)
        {
            return dwRejectType == SERVERCALL_RETRYLATER ? 150 : -1;
        }

        /// <summary>
        /// /
        /// </summary>
        /// <param name="threadIDCallee"></param>
        /// <param name="dwTickCount"></param>
        /// <param name="dwPendingType"></param>
        /// <returns></returns>
        // ReSharper disable once InconsistentNaming
        public int MessagePending(IntPtr threadIDCallee, int dwTickCount, int dwPendingType)
        {
            return PENDINGMSG_WAITNOPROCESS; // default processing
        }
    }

}