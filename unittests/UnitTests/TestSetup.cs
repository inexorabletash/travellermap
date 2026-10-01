using Maps.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
            // Where tests run from depends on the test runner (base directory, shadow
            // copies, deployment folders), so try several starting points.
            var starts = new List<string>();
            void Add(Func<string> get)
            {
                try
                {
                    string path = get();
                    if (!string.IsNullOrEmpty(path))
                        starts.Add(path);
                }
                catch
                {
                    // Not available in this runner.
                }
            }
            Add(() => Environment.GetEnvironmentVariable("TM_REPO_ROOT"));
            Add(() => new Uri(typeof(TestSetup).Assembly.CodeBase).LocalPath);  // original location, before shadow copying
            Add(() => typeof(TestSetup).Assembly.Location);
            Add(() => AppDomain.CurrentDomain.BaseDirectory);
            Add(() => Environment.CurrentDirectory);
            Add(() => context.TestRunDirectory);
            Add(() => context.DeploymentDirectory);

            RepoRoot = starts.Select(FindRepoRoot).FirstOrDefault(root => root != null)
                ?? throw new InvalidOperationException(
                    "Could not find repository root (Maps.sln) from: " + string.Join("; ", starts) +
                    ". Set TM_REPO_ROOT to override.");
            Util.ContentRoot = RepoRoot;
        }

        private static string FindRepoRoot(string start)
        {
            var dir = File.Exists(start) ? new FileInfo(start).Directory : new DirectoryInfo(start);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Maps.sln")))
                dir = dir.Parent;
            return dir?.FullName;
        }
    }
}
