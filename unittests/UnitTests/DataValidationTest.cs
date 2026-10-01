using Maps;
using Maps.Admin;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace UnitTests
{
    /// <summary>
    /// Validates all sector data and metadata in res/Sectors (data files parse, allegiance
    /// codes are defined, routes are sane, XML matches res/sectors.xsd).
    ///
    /// Existing problems are listed in test/data-validation-baseline.txt; the test fails only
    /// on errors that are not in the baseline, so data edits can't introduce new ones.
    /// After fixing data, regenerate the baseline by running this test with the
    /// environment variable TM_UPDATE_BASELINE=1.
    /// </summary>
    [TestClass]
    public class DataValidationTest
    {
        private static readonly Regex LINE_NUMBER = new Regex(@"\bline \d+[:,]?\s*");

        // Line numbers shift whenever a file is edited, so they are not part of the key.
        private static string Key(DataValidator.Finding f) =>
            $"{f.Category} | {f.Where} | {LINE_NUMBER.Replace(f.Message, "")}";

        private static Dictionary<string, int> Count(IEnumerable<string> keys) =>
            keys.GroupBy(k => k).ToDictionary(g => g.Key, g => g.Count());

        [TestMethod]
        public void ValidateAllData()
        {
            var validator = new DataValidator();
            SectorMap map = SectorMap.GetInstance();
            validator.ValidateSectors(map, ResourceManager.GetDedicatedInstance());
            validator.ValidateXml(SectorMap.MetafilePaths()
                .Concat(map.Sectors.Where(s => s.MetadataFile != null).Select(s => s.MetadataFile)));

            var errors = validator.Findings.Where(f => f.Severity == DataValidator.Severity.Error).ToList();
            var current = Count(errors.Select(Key));

            string baselinePath = Path.Combine(TestSetup.RepoRoot, "test", "data-validation-baseline.txt");

            if (Environment.GetEnvironmentVariable("TM_UPDATE_BASELINE") == "1")
            {
                var lines = new List<string> {
                    "# Known data validation errors; see unittests/UnitTests/DataValidationTest.cs.",
                    "# Format: category | sector (milieu) or file | message. Regenerate with TM_UPDATE_BASELINE=1.",
                };
                lines.AddRange(errors.Select(Key).OrderBy(k => k, StringComparer.Ordinal));
                File.WriteAllLines(baselinePath, lines, new UTF8Encoding(false));
                Console.WriteLine($"Wrote {errors.Count} entries to {baselinePath}");
                return;
            }

            var baseline = Count(File.ReadAllLines(baselinePath).Where(l => l.Length > 0 && !l.StartsWith("#")));

            var added = current
                .Where(kv => kv.Value > (baseline.TryGetValue(kv.Key, out int n) ? n : 0))
                .Select(kv => kv.Key).OrderBy(k => k, StringComparer.Ordinal).ToList();
            int fixedCount = baseline.Sum(kv => Math.Max(0, kv.Value - (current.TryGetValue(kv.Key, out int n) ? n : 0)));

            var summary = new StringBuilder();
            foreach (var group in validator.Findings.GroupBy(f => (f.Severity, f.Category)).OrderBy(g => g.Key.ToString()))
                summary.AppendLine($"  {group.Key.Severity} {group.Key.Category}: {group.Count()}");
            Console.WriteLine("Findings:\n" + summary);
            if (fixedCount > 0)
                Console.WriteLine($"{fixedCount} baseline error(s) no longer occur. Regenerate the baseline (TM_UPDATE_BASELINE=1) to lock in the fix.");

            if (added.Count > 0)
            {
                Assert.Fail($"{added.Count} new data error(s) not in test/data-validation-baseline.txt:\n" +
                    string.Join("\n", added.Take(50)) +
                    (added.Count > 50 ? $"\n... and {added.Count - 50} more" : ""));
            }
        }
    }
}
