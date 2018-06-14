using System;
using System.IO;

namespace AIT.Tools.VisualStudioTextTransform.Tests
{
    public class TestEnv
    {
        private readonly string _testProjectDir;
        private readonly string _solutionDir;
        private readonly string _solutionFile;
        private readonly string _globalProjectDir;

        public TestEnv()
        {
            // This is a bit ugly, but the problem is we use the current solution for testing
            // But we cannot use the MSTest Deploy mechanism because of path length problems.
            var globalProjectDir = Environment.GetEnvironmentVariable("PROJECT_DIRECTORY");
            if (string.IsNullOrEmpty(globalProjectDir))
            {
                var currentDir = Environment.CurrentDirectory;
                string configurationTestDir;
                if (Path.GetFileName(currentDir) == "Out")
                {
                    configurationTestDir = Path.GetDirectoryName(currentDir);
                }
                else
                {
                    configurationTestDir = currentDir;
                }
                var testDir = Path.GetDirectoryName(configurationTestDir);
                var buildDir = Path.GetDirectoryName(testDir);
                globalProjectDir = Path.GetDirectoryName(buildDir);
            }
            _globalProjectDir = globalProjectDir;
            _solutionDir = Path.Combine(_globalProjectDir, "src");
            _testProjectDir = Path.Combine(_solutionDir, "VisualStudioTextTransform.Tests");
            _solutionFile = Path.GetFullPath(Path.Combine(_solutionDir, "VisualStudioTextTransform.sln"));
            
        }

        public string TestProjectDir
        {
            get { return _testProjectDir; }
        }

        public string SolutionDir
        {
            get { return _solutionDir; }
        }

        public string SolutionFile
        {
            get { return _solutionFile; }
        }
    }
}