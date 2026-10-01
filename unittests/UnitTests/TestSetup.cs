using Maps.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace UnitTests
{
    [TestClass]
    public static class TestSetup
    {
        /// <summary>Repository root (the directory containing Maps.sln).</summary>
        public static string RepoRoot { get; private set; }

        [AssemblyInitialize]
        public static void Initialize(TestContext context)
        {
            // Tests run outside ASP.NET, so "~/res/..." paths resolve against the repo.
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Maps.sln")))
                dir = dir.Parent;
            if (dir == null)
                throw new InvalidOperationException("Could not find repository root (Maps.sln)");

            RepoRoot = dir.FullName;
            Util.ContentRoot = RepoRoot;
        }
    }
}
