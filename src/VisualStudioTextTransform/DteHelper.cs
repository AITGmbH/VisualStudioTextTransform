using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using AIT.Tools.VisualStudioTextTransform.Properties;
using EnvDTE80;

namespace AIT.Tools.VisualStudioTextTransform
{
    /// <summary>
    /// Helper class to fetch DTE instances.
    /// </summary>
    public static class DteHelper
    {

        /// <summary>
        /// /
        /// </summary>
        /// <returns></returns>
        private static readonly TraceSource Source = new TraceSource("AIT.Tools.VisualStudioTextTransform");

        // From http://www.viva64.com/en/b/0169/
        /// <summary>
        /// Tries to get the DTE2 instance by process id.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static DTE2 TryGetById(int id)
        {
            //rot entry for visual studio running under current process.
            var rotEntry = string.Format(CultureInfo.InvariantCulture, "!VisualStudio.DTE.12.0:{0}", id);
            IRunningObjectTable rot;
            NativeMethods.GetRunningObjectTable(0, out rot);
            IEnumMoniker enumMoniker;
            rot.EnumRunning(out enumMoniker);
            enumMoniker.Reset();
            IntPtr fetched = IntPtr.Zero;
            IMoniker[] moniker = new IMoniker[1];
            while (enumMoniker.Next(1, moniker, fetched) == 0)
            {
                IBindCtx bindCtx;
                NativeMethods.CreateBindCtx(0, out bindCtx);
                string displayName;
                moniker[0].GetDisplayName(bindCtx, null, out displayName);
                if (displayName == rotEntry)
                {
                    object comObject;
                    var result = rot.GetObject(moniker[0], out comObject);
                    Marshal.ThrowExceptionForHR(result);
                    return (DTE2)comObject;
                }
                else
                {
                    Source.TraceEvent(TraceEventType.Information, 0, "Found event with name: {0}", displayName);
                }
            }
            return null;
        }
        
        /// <summary>
        /// Tries to create a DTE instance.
        /// First it tries to start a new Visual Studio instance and to fetch the DTE instance.
        /// If that fails we fall back to creating the Instance via Activator.CreateInstance.
        /// If possible we return the process-Id of the Visual Studio instance (or -1 otherwise).
        /// 
        /// If something goes wrong no process is opened (i.e. this method cleans up the opened process) otherwise
        /// the caller needs to take care about closing the Visual Studio instance.
        /// </summary>
        /// <returns></returns>
        public static Tuple<int, DTE2> CreateDteInstance()
        {
            if (!Settings.Default.SelfHostVisualStudio)
            {
                Source.TraceEvent(TraceEventType.Warning, 0, "Self-hosting is disabled");
                return CreateDteInstanceWithActivator();
            }

            // We Create our own instance for customized logging + killing afterwards
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var pfx64 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var vs2013Relative = Path.Combine("Microsoft Visual Studio 12.0", "Common7", "IDE", "devenv.exe");
            var testPaths = new[]
            {
                Path.Combine(pf, vs2013Relative),
                Path.Combine(pfx64, vs2013Relative)
            };
            var devenvExe = testPaths.FirstOrDefault(File.Exists);
            if (string.IsNullOrEmpty(devenvExe))
            {
                Source.TraceEvent(TraceEventType.Error, 0, "Could not find devenv.exe, falling back to COM.");
                return CreateDteInstanceWithActivator();
            }

            using (var start =
                Process.Start(
                    devenvExe,
                    string.Format(CultureInfo.InvariantCulture, "-Embedding /log \"{0}\"",
                        Path.GetFullPath(Settings.Default.VisualStudioLogfile))))
            {
                if (start == null)
                {
                    Source.TraceEvent(TraceEventType.Error, 0, "Could not start devenv.exe, falling back to COM.");
                    return CreateDteInstanceWithActivator();
                }
                try
                {
                    DTE2 dte = null;
                    var timeout = TimeSpan.FromMinutes(5);
                    var currentSpan = TimeSpan.Zero;
                    var waitTime = TimeSpan.FromSeconds(10);
                    while (dte == null && currentSpan < timeout && !start.HasExited)
                    {
                        Thread.Sleep(waitTime);
                        Source.TraceInformation("Trying to get DTE instance from process...");
                        dte = TryGetById(start.Id);
                        currentSpan += waitTime;
                    }

                    if (dte == null)
                    {
                        if (!start.HasExited)
                        {
                            start.Kill();
                        }
                        Source.TraceEvent(TraceEventType.Error, 1, "Could not get DTE instance from process!");
                        return CreateDteInstanceWithActivator();
                    }

                    return Tuple.Create(start.Id, dte);
                }
                catch (Exception e)
                {
                    Source.TraceEvent(TraceEventType.Verbose, 0, "Killing devenv.exe because of error while fetching dte instance from process: {0}",
                        e);
                    if (!start.HasExited)
                    {
                        start.Kill();
                    }

                    throw;
                }
            }
        }

        private static Tuple<int, DTE2> CreateDteInstanceWithActivator()
        {
            Source.TraceEvent(TraceEventType.Verbose, 1, "Creating Visual Studio (devenv.exe) instance via COM.");
            var dteType = Type.GetTypeFromProgID("VisualStudio.DTE.12.0", true);
            return Tuple.Create(-1, (DTE2)Activator.CreateInstance(dteType, true));
        }

        /// <summary>
        /// Cleanup the given DTE instance and kill the process after some timeout.
        /// </summary>
        /// <param name="processId"></param>
        /// <param name="dte"></param>
        public static void CleanupDteInstance(int processId, DTE2 dte)
        {
            if (dte == null)
            {
                throw new ArgumentNullException("dte");
            }

            Process process = null;
            if (processId > 0)
            {
                process = Process.GetProcessById(processId);
            }

            dte.Quit();

            // Makes no sense to wait when the process already exited, or when we have no processId to kill.
            int i = 0;
            while (i < 10 && process != null && !process.HasExited)
            {
                Thread.Sleep(1000);
                i++;
            }

            if (process != null && !process.HasExited)
            {
                process.Kill();
            }
        }
    }
}
