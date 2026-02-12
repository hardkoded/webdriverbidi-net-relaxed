// <copyright file="AotSmokeTests.cs" company="WebDriverBiDi.NET Committers">
// Copyright (c) WebDriverBiDi.NET Committers. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace WebDriverBiDi.Integration;

using System.Diagnostics;
using System.Runtime.InteropServices;

[TestFixture]
[Category("Integration")]
public class AotSmokeTests
{
    private static readonly string SmokeTestProjectDir = Path.GetFullPath(
        Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "WebDriverBiDi.AotSmokeTest"));

    [SetUp]
    public void SkipIfNotCI()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
        {
            Assert.Ignore("Skipped outside CI. Set CI=true to run locally.");
        }
    }

    [Test]
    public async Task AotSmokeTestPassesWithFirefox()
    {
        string publishDir = Path.Combine(SmokeTestProjectDir, "bin", "AotTestPublish");

        // Publish the AOT smoke test as a native binary.
        int publishExit = await RunProcessAsync(
            "dotnet",
            $"publish \"{SmokeTestProjectDir}\" -c Release -o \"{publishDir}\"",
            workingDirectory: SmokeTestProjectDir,
            timeoutSeconds: 300);

        Assert.That(publishExit, Is.EqualTo(0), "dotnet publish of AotSmokeTest failed.");

        string executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "WebDriverBiDi.AotSmokeTest.exe"
            : "WebDriverBiDi.AotSmokeTest";
        string executablePath = Path.Combine(publishDir, executableName);

        Assert.That(File.Exists(executablePath), Is.True, $"Published AOT executable not found at: {executablePath}");

        // Run the published native binary.
        int runExit = await RunProcessAsync(
            executablePath,
            string.Empty,
            workingDirectory: publishDir,
            timeoutSeconds: 120);

        Assert.That(runExit, Is.EqualTo(0), "AotSmokeTest process exited with non-zero exit code.");
    }

    private static async Task<int> RunProcessAsync(string fileName, string arguments, string workingDirectory, int timeoutSeconds)
    {
        using Process process = new();
        process.StartInfo.FileName = fileName;
        process.StartInfo.Arguments = arguments;
        process.StartInfo.WorkingDirectory = workingDirectory;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;

        process.Start();

        // Read stdout/stderr concurrently to avoid deadlocks.
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();

        bool exited = await Task.Run(() => process.WaitForExit(timeoutSeconds * 1000));

        string stdout = await stdoutTask;
        string stderr = await stderrTask;

        TestContext.Out.WriteLine($"[{Path.GetFileName(fileName)}] stdout:\n{stdout}");
        if (!string.IsNullOrWhiteSpace(stderr))
        {
            TestContext.Error.WriteLine($"[{Path.GetFileName(fileName)}] stderr:\n{stderr}");
        }

        if (!exited)
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail($"Process '{fileName}' timed out after {timeoutSeconds}s.");
        }

        return process.ExitCode;
    }
}
