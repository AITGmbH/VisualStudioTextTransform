using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text.RegularExpressions;
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

        public static Regex CreateDteObjectNameRegex(int processId) => new Regex(@"!VisualStudio.DTE\.\d+\.\d+\:" + processId, RegexOptions.IgnoreCase);

        /// <summary>
        /// Gets the DTE object from any devenv process.
        /// </summary>
        /// <remarks>
        /// See http://www.viva64.com/en/b/0169/ and https://www.helixoft.com/blog/creating-envdte-dte-for-vs-2017-from-outside-of-the-devenv-exe.html
        ///</remarks>
        /// <param name="processId"></param>
        /// <returns>
        /// Retrieved DTE object or <see langword="null"> if not found.
        /// </see></returns>
        private static DTE2 GetFromProcess(int processId)
        {
            DTE2 result = null;
            IBindCtx bindCtx = null;
            IRunningObjectTable rot = null;
            IEnumMoniker enumMonikers = null;

            try
            {
                Marshal.ThrowExceptionForHR(NativeMethods.CreateBindCtx(reserved: 0, ppbc: out bindCtx));
                bindCtx.GetRunningObjectTable(out rot);
                rot.EnumRunning(out enumMonikers);

                IMoniker[] moniker = new IMoniker[1];
                IntPtr numberFetched = IntPtr.Zero;
                Regex monikerRegex = CreateDteObjectNameRegex(processId);

                object runningObject = null;
                while (enumMonikers.Next(1, moniker, numberFetched) == 0)
                {
                    IMoniker runningObjectMoniker = moniker[0];

                    string name = null;

                    try
                    {
                        if (runningObjectMoniker != null)
                        {
                            runningObjectMoniker.GetDisplayName(bindCtx, null, out name);
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Do nothing, there is something in the ROT that we do not have access to.
                    }

                    if (!string.IsNullOrEmpty(name) && monikerRegex.IsMatch(name))
                    {
                        Source.TraceEvent(TraceEventType.Verbose, 1, "Found matching COM object '{0}'.", name);
                        Marshal.ThrowExceptionForHR(rot.GetObject(runningObjectMoniker, out runningObject));
                        try
                        {
                            result = (DTE2)runningObject;
                            break;
                        }
                        catch (InvalidCastException)
                        {
                            Source.TraceEvent(TraceEventType.Warning, 1, "Found COM object with name '{0}', but was unable to cast to DTE2", name);
                        }
                    }
                    else
                    {
                        Source.TraceEvent(TraceEventType.Verbose, 1, "COM object '{0}' does not match regex.", name);
                    }
                }

            }
            finally
            {
                if (enumMonikers != null)
                {
                    Marshal.ReleaseComObject(enumMonikers);
                }

                if (rot != null)
                {
                    Marshal.ReleaseComObject(rot);
                }

                //if (bindCtx != null)
                //{
                //    Marshal.ReleaseComObject(bindCtx);
                //}
            }

            return result;
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
            var vs2015Relative = Path.Combine("Microsoft Visual Studio 14.0", "Common7", "IDE", "devenv.exe");
            var testPaths = new[]
            {
                Path.Combine(pf, vs2015Relative),
                Path.Combine(pfx64, vs2015Relative)
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
                        if (start.HasExited)
                        {
                            Source.TraceEvent(TraceEventType.Error, 1, "devenv Process exited (id: '{0}')", start.Id);
                            break;
                        }

                        Source.TraceInformation("Trying to get DTE instance from process...");
                        dte = GetFromProcess(start.Id);
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
            try
            {
                var dteType = Type.GetTypeFromProgID("VisualStudio.DTE.14.0", true);
                return Tuple.Create(-1, (DTE2)Activator.CreateInstance(dteType, true));
            }
            catch (Exception e)
            {
                throw new TargetInvocationException("Creating DTE-Instance failed, make sure Visual Studio 2015 is installed! See inner exception for details.", e);
            }
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
