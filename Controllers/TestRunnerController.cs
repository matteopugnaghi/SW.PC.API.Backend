// Controllers/TestRunnerController.cs
// 🧪 xUnit Test Runner API — Development Only
// Executes backend unit tests and logs results to SystemLog (L3)

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Services;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SW.PC.API.Backend.Controllers
{
    [ApiController]
    [Route("api/tests")]
    [Authorize]
    public class TestRunnerController : ControllerBase
    {
        private readonly ILogger<TestRunnerController> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly ISystemLogService _systemLogService;

        public TestRunnerController(
            ILogger<TestRunnerController> logger,
            IWebHostEnvironment environment,
            ISystemLogService systemLogService)
        {
            _logger = logger;
            _environment = environment;
            _systemLogService = systemLogService;
        }

        /// <summary>
        /// POST /api/tests/run — Run xUnit tests (Development only)
        /// </summary>
        [HttpPost("run")]
        public async Task<IActionResult> RunTests()
        {
            // ⛔ Only allow in Development
            if (!_environment.IsDevelopment())
            {
                return StatusCode(403, new { error = "Test runner is only available in Development mode" });
            }

            var contentRoot = _environment.ContentRootPath;
            var testProjectPath = Path.Combine(contentRoot, "Tests", "SW.PC.API.Backend.Tests.csproj");

            if (!System.IO.File.Exists(testProjectPath))
            {
                return NotFound(new { error = "Test project not found", path = testProjectPath });
            }

            _logger.LogInformation("🧪 Running xUnit tests from {Path}", testProjectPath);

            // Log test start
            _systemLogService.AddEntry(new SystemLogEntry
            {
                Level = SystemLogLevel.Warning,
                Source = SystemLogSource.Backend,
                Category = "TestRunner",
                Message = "🧪 xUnit test execution started"
            });

            try
            {
                var result = await RunDotnetTestAsync(testProjectPath);

                // Log results to SystemLog (L3)
                var logLevel = result.Failed > 0 ? SystemLogLevel.Error : SystemLogLevel.Warning;
                _systemLogService.AddEntry(new SystemLogEntry
                {
                    Level = logLevel,
                    Source = SystemLogSource.Backend,
                    Category = "TestRunner",
                    Message = $"🧪 Tests completed: {result.Passed} passed, {result.Failed} failed, {result.Total} total ({result.DurationMs}ms)"
                });

                // Log individual failures
                foreach (var failure in result.Failures)
                {
                    _systemLogService.AddEntry(new SystemLogEntry
                    {
                        Level = SystemLogLevel.Error,
                        Source = SystemLogSource.Backend,
                        Category = "TestRunner.Failure",
                        Message = $"❌ FAIL: {failure.TestName}",
                        Exception = failure.ErrorMessage
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running tests");
                _systemLogService.AddEntry(new SystemLogEntry
                {
                    Level = SystemLogLevel.Critical,
                    Source = SystemLogSource.Backend,
                    Category = "TestRunner",
                    Message = $"🧪 Test execution failed: {ex.Message}",
                    Exception = ex.ToString()
                });

                return StatusCode(500, new { error = "Test execution failed", message = ex.Message });
            }
        }

        /// <summary>
        /// GET /api/tests/status — Check if test runner is available
        /// </summary>
        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            var contentRoot = _environment.ContentRootPath;
            var testProjectPath = Path.Combine(contentRoot, "Tests", "SW.PC.API.Backend.Tests.csproj");
            var exists = System.IO.File.Exists(testProjectPath);

            return Ok(new
            {
                available = _environment.IsDevelopment() && exists,
                isDevelopment = _environment.IsDevelopment(),
                testProjectExists = exists,
                testProjectPath = exists ? testProjectPath : null
            });
        }

        // ==================== Private Methods ====================

        private async Task<TestRunResult> RunDotnetTestAsync(string projectPath)
        {
            var sw = Stopwatch.StartNew();

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"test \"{projectPath}\" --verbosity normal",
                WorkingDirectory = _environment.ContentRootPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            var output = new System.Text.StringBuilder();
            var error = new System.Text.StringBuilder();

            process.OutputDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) error.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Timeout: 120 seconds max (includes build + test)
            var completed = await Task.Run(() => process.WaitForExit(120_000));
            sw.Stop();

            if (!completed)
            {
                try { process.Kill(); } catch { }
                throw new TimeoutException("Test execution exceeded 60 second timeout");
            }

            var fullOutput = output.ToString() + error.ToString();
            return ParseTestOutput(fullOutput, sw.ElapsedMilliseconds);
        }

        private static TestRunResult ParseTestOutput(string output, long durationMs)
        {
            var result = new TestRunResult
            {
                RawOutput = output,
                DurationMs = durationMs,
                Timestamp = DateTime.Now
            };

            // Parse summary line: "Pruebas totales: 65" or "Total tests: 65"
            var totalMatch = Regex.Match(output, @"(?:Pruebas totales|Total tests):\s*(\d+)", RegexOptions.IgnoreCase);
            if (totalMatch.Success)
                result.Total = int.Parse(totalMatch.Groups[1].Value);

            // Parse passed: "Correcto: 65" or "Passed: 65" or "Correctas: 65"
            var passedMatch = Regex.Match(output, @"(?:Correctas?|Passed):\s*(\d+)", RegexOptions.IgnoreCase);
            if (passedMatch.Success)
                result.Passed = int.Parse(passedMatch.Groups[1].Value);

            // Parse failed: "Con error: X" or "Failed: X" or "No superada: X"
            var failedMatch = Regex.Match(output, @"(?:Failed|Con error|No superadas?):\s*(\d+)", RegexOptions.IgnoreCase);
            if (failedMatch.Success)
                result.Failed = int.Parse(failedMatch.Groups[1].Value);

            // Parse individual failures
            var failureMatches = Regex.Matches(output, @"(?:Error|Failed)\s+([^\[]+?)(?:\s*\[)", RegexOptions.IgnoreCase);
            foreach (Match m in failureMatches)
            {
                var testName = m.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(testName) && testName.Contains('.'))
                {
                    result.Failures.Add(new TestFailure
                    {
                        TestName = testName,
                        ErrorMessage = "" // Full error details in raw output
                    });
                }
            }

            result.Success = result.Failed == 0 && result.Total > 0;

            return result;
        }
    }

    // ==================== DTOs ====================

    public class TestRunResult
    {
        public bool Success { get; set; }
        public int Total { get; set; }
        public int Passed { get; set; }
        public int Failed { get; set; }
        public long DurationMs { get; set; }
        public DateTime Timestamp { get; set; }
        public string RawOutput { get; set; } = "";
        public List<TestFailure> Failures { get; set; } = new();
    }

    public class TestFailure
    {
        public string TestName { get; set; } = "";
        public string ErrorMessage { get; set; } = "";
    }
}
