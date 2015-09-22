using System;
using System.Diagnostics;
using AIT.Tools.VisualStudioTextTransform.Properties;
using AIT.VisualStudio.Controlling;
using CommandLine;

namespace AIT.Tools.VisualStudioTextTransform
{
    /// <summary>
    /// the entry point of the application.
    /// </summary>
    public static class Program
    {
        private static readonly TraceSource Source = LoggingHelper.CreateSource("AIT.Tools.VisualStudioTextTransform");

        /// <summary>
        /// the Entry point of the application.
        /// </summary>
        /// <param name="arguments">the arguments of the program</param>
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
                Source.TraceEvent(TraceEventType.Critical, 1, Resources.Program_Main_Application_crashed_with___0_, e);
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
            var opts = new string[arguments.Length - 1];
            Array.Copy(arguments, 1, opts, 0, arguments.Length - 1);
            var options = new Options();
            Parser.Default.ParseArguments(opts, options);

            return 
                TemplateProcessor.ProcessSolution(solutionFileName, options) ? 0 : 1;
        }

    }
}