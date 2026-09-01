using Xunit;

// Review R5(b): opts this whole assembly into Xunit.SkippableFact's custom test runner so
// [SkippableFact]/[SkippableTheory] + Skip.If(...) can report an explicit "Skipped" result
// (instead of a silently-green "Passed" from an early `if (!CanRun) return;`) when
// SQL Server/LocalDB is unavailable in the sandbox. Xunit only allows a single
// [assembly: TestFramework] declaration per assembly — this is that one declaration.
[assembly: TestFramework("Xunit.Sdk.SkippableFactTestFramework", "Xunit.SkippableFact")]
