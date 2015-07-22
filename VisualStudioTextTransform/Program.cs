using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using AIT.Tools.VisualStudioTextTransform.Properties;
using EnvDTE80;
using Engine = Microsoft.VisualStudio.TextTemplating.Engine;

namespace AIT.Tools.VisualStudioTextTransform
{
    /// <summary>
    /// /
    /// </summary>
    public static class Program
    {
        private static readonly TraceSource _source = new TraceSource("AIT.Tools.VisualStudioTextTransform");

        /// <summary>
        /// /
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
        [STAThread]
        public static int Main(string[] arguments)
        {
            try
            {
                return ExecuteMain(arguments);
            }
            catch (Exception e)
            {
                _source.TraceEvent(TraceEventType.Critical, 1, Resources.Program_Main_Application_crashed_with___0_, e);
                return 1;
            }
        }

        private static int ExecuteMain(string[] arguments)
        {
            if (arguments.Length == 0)
            {
                throw new ArgumentException(Resources.Program_Main_you_must_provide_a_solution_file);
            }
            var solutionFileName = arguments[0];
            return 
                TemplateProcessor.ProcessSolution(solutionFileName) ? 0 : 1;
        }

    }
}