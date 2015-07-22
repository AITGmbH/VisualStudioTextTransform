using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace AIT.Tools.VisualStudioTextTransform
{
    // Fix COM retry errors: http://www.viva64.com/en/b/0169/
    [ComImport]
    [Guid("00000016-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMessageFilter
    {
        [SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly",
            Justification = "Names given by a foraign API"),
         SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly",
            Justification = "Names given by a foreign API"), PreserveSig]
        int HandleInComingCall(int dwCallType, IntPtr threadIdCaller, int dwTickCount, IntPtr lpInterfaceInfo);


        [SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly",
            Justification = "Names given by a foraign API"),
         SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly",
            Justification = "Names given by a foreign API"), PreserveSig]
        int RetryRejectedCall(IntPtr threadIDCallee, int dwTickCount, int dwRejectType);

        [SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly",
            Justification = "Names given by a foraign API"),
         SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly",
            Justification = "Names given by a foreign API"), PreserveSig]
        int MessagePending(IntPtr threadIDCallee , int dwTickCount , int dwPendingType);
    }

    internal static class NativeMethods
    {
        [DllImport("ole32.dll")]
        [SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly",
            Justification = "Names given by a foraign API"),
         SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly",
            Justification = "Names given by a foreign API"), PreserveSig]
        public static extern int CoRegisterMessageFilter(IMessageFilter lpMessageFilter, out IMessageFilter lplpMessageFilter);

    }

    public class MessageFilter : MarshalByRefObject, IMessageFilter, IDisposable
    {
        public const int SERVERCALL_ISHANDLED = 0;
        public const int PENDINGMSG_WAITNOPROCESS = 2;
        public const int SERVERCALL_RETRYLATER = 2;
        
        private IMessageFilter oldFilter;
        public MessageFilter()
        {
            var hr = NativeMethods.CoRegisterMessageFilter(this, out oldFilter);
            if (hr < 0)
            {
                throw Marshal.GetExceptionForHR(hr);
            }
            Debug.Assert(hr >= 0);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                IMessageFilter f;
                var hr = NativeMethods.CoRegisterMessageFilter(oldFilter, out f);
                Debug.Assert(hr >= 0);
            }
        }


        public int HandleInComingCall(int dwCallType, IntPtr threadIdCaller, int dwTickCount, IntPtr lpInterfaceInfo)
        {
            // Return the ole default (don't let the call through).
            return SERVERCALL_ISHANDLED;
        }

        public int RetryRejectedCall(IntPtr threadIDCallee, int dwTickCount, int dwRejectType)
        {
            return dwRejectType == SERVERCALL_RETRYLATER ? 150 : -1;
        }


        public int MessagePending(IntPtr threadIDCallee , int dwTickCount , int dwPendingType)
        {
            return PENDINGMSG_WAITNOPROCESS; // default processing
        }
    }

}