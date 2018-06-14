using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace AIT.Tools.VisualStudioTextTransform
{
    internal static class NativeMethods
    {

        [ComImport]
        [Guid("00000016-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IMessageFilter
        {
            [SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly",
                Justification = "Names given by a foreign API"),
             SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly",
                Justification = "Names given by a foreign API"), PreserveSig]
            int HandleInComingCall(int dwCallType, IntPtr threadIdCaller, int dwTickCount, IntPtr lpInterfaceInfo);


            [SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly",
                Justification = "Names given by a foreign API"),
             SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly",
                Justification = "Names given by a foreign API"), PreserveSig]
            // ReSharper disable once InconsistentNaming
            int RetryRejectedCall(IntPtr threadIDCallee, int dwTickCount, int dwRejectType);

            [SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly",
                Justification = "Names given by a foreign API"),
             SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly",
                Justification = "Names given by a foreign API"), PreserveSig]
            // ReSharper disable once InconsistentNaming
            int MessagePending(IntPtr threadIDCallee, int dwTickCount, int dwPendingType);
        }

        [DllImport("ole32.dll")]
        [SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly",
            Justification = "Names given by a foreign API"),
         SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly",
             Justification = "Names given by a foreign API"), PreserveSig]
        public static extern int CoRegisterMessageFilter(IMessageFilter lpMessageFilter, out IMessageFilter lplpMessageFilter);

        [DllImport("ole32.dll")]
        internal static extern int CreateBindCtx(uint reserved, out IBindCtx ppbc);

        [DllImport("ole32.dll", PreserveSig = false)]
        internal static extern void GetRunningObjectTable(int reserved,
            out IRunningObjectTable prot);
    }
}