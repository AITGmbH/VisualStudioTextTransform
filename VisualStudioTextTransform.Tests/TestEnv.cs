using System;
using System.IO;

namespace AIT.Tools.VisualStudioTextTransform.Tests
{
    public class TestEnv
    {
        private readonly string _debugDir;
        private readonly string _binDir;
        private readonly string _testProjectDir;
        private readonly string _solutionDir;
        private readonly string _solutionFile;

        public TestEnv()
        {

            _debugDir = Environment.CurrentDirectory;
            _binDir = Path.GetDirectoryName(_debugDir);
            _testProjectDir = Path.GetDirectoryName(_binDir);
            _solutionDir = Path.GetDirectoryName(_testProjectDir);
            _solutionFile = Path.GetFullPath(Path.Combine(_solutionDir, "VisualStudioTextTransform.sln"));
            
        }

        public string DebugDir
        {
            get { return _debugDir; }
        }

        public string BinDir
        {
            get { return _binDir; }
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