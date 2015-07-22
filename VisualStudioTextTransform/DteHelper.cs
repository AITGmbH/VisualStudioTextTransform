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
    internal static class DteHelper
    {

        /// <summary>
        /// /
        /// </summary>
        /// <returns></returns>
        private static readonly TraceSource _source = new TraceSource("AIT.Tools.VisualStudioTextTransform");

        // From http://www.viva64.com/en/b/0169/
        public static DTE2 GetById(int id)
        {
            //rot entry for visual studio running under current process.
            string rotEntry = string.Format(CultureInfo.InvariantCulture, "!VisualStudio.DTE.12.0:{0}", id);
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
                    _source.TraceEvent(TraceEventType.Information, 0, "Found event with name: {0}", displayName);
                }
            }
            return null;
        }

        public static Tuple<int, DTE2> CreateDteInstance()
        {
            if (!Settings.Default.SelfHostVisualStudio)
            {
                _source.TraceEvent(TraceEventType.Warning, 0, "Selfhosting is disabled");
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
                _source.TraceEvent(TraceEventType.Error, 0, "Could not find devenv.exe, falling back to COM.");
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
                    _source.TraceEvent(TraceEventType.Error, 0, "Could not start devenv.exe, falling back to COM.");
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
                        _source.TraceInformation("Trying to get DTE instance from process...");
                        dte = GetById(start.Id);
                        currentSpan += waitTime;
                    }
                    if (dte == null)
                    {
                        if (!start.HasExited)
                        {
                            start.Kill();
                        }
                        _source.TraceEvent(TraceEventType.Error, 1, "Could not get DTE instance from process!");
                        return CreateDteInstanceWithActivator();
                    }
                    return Tuple.Create(start.Id, dte);
                }
                catch (Exception e)
                {
                    _source.TraceEvent(TraceEventType.Verbose, 0, "Killing devenv.exe because of error while fetching dte instance from process: {0}",
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
            _source.TraceEvent(TraceEventType.Verbose, 1, "Creating devenv instance via COM.");
            var dteType = Type.GetTypeFromProgID("VisualStudio.DTE.12.0", true);
            return Tuple.Create(-1, (DTE2)Activator.CreateInstance(dteType, true));
        }

    }
}
