using System.Diagnostics;
using Xunit;

namespace Seamless.Database.Gateway.Tests {
    public sealed class MultiProjectTemplateE2ETests {
        [Fact]
        public async Task MultiProjectTemplate_ServerAndClient_CommunicateSuccessfullyViaAotNativeRpc() {
            var repoRoot = FindRepositoryRoot();
            var serverDll = Path.Combine(repoRoot, "MultiProjectTemplate", "Server", "bin", "Debug", "net10.0", "MultiProjectTemplate.Server.dll");
            var clientDll = Path.Combine(repoRoot, "MultiProjectTemplate", "Client", "bin", "Debug", "net10.0", "MultiProjectTemplate.Client.dll");

            Assert.True(File.Exists(serverDll), $"Server DLL not found: {serverDll}");
            Assert.True(File.Exists(clientDll), $"Client DLL not found: {clientDll}");

            var dotnetHost = ResolveDotNetHost();

            var (serverProcess, allocatedPort) = await StartServerAsync(dotnetHost, serverDll, repoRoot, extraArgs: "--port 0");
            try {
                var (exitCode, outputText, errorText) = await RunClientAsync(dotnetHost, clientDll, repoRoot, allocatedPort);

                Assert.Equal(0, exitCode);
                Assert.True(outputText.Contains("CLIENT_SUCCESS"), $"CLIENT_SUCCESS not found.\nOutput:\n{outputText}\nError:\n{errorText}");
                Assert.Contains("High-Performance Laptop", outputText);
            } finally {
                await StopServerAsync(serverProcess);
            }
        }

        [Fact]
        public async Task MultiProjectTemplate_PersistentStorage_RecoversDataAfterRestart() {
            var repoRoot = FindRepositoryRoot();
            var serverDll = Path.Combine(repoRoot, "MultiProjectTemplate", "Server", "bin", "Debug", "net10.0", "MultiProjectTemplate.Server.dll");
            var clientDll = Path.Combine(repoRoot, "MultiProjectTemplate", "Client", "bin", "Debug", "net10.0", "MultiProjectTemplate.Client.dll");

            Assert.True(File.Exists(serverDll), $"Server DLL not found: {serverDll}");
            Assert.True(File.Exists(clientDll), $"Client DLL not found: {clientDll}");

            var dotnetHost = ResolveDotNetHost();
            var testDataRoot = Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT") ?? @"F:\Seamless\TestData";
            var persistDir = Path.Combine(testDataRoot, "PersistE2E_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(persistDir);

            try {
                // 第1セッション: 永続化ストレージでサーバー起動 -> クライアントでアクセス -> Graceful Shutdown
                {
                    var (server1, port1) = await StartServerAsync(dotnetHost, serverDll, repoRoot, extraArgs: $"--port 0 --data-dir \"{persistDir}\"");
                    try {
                        var (exitCode1, output1, error1) = await RunClientAsync(dotnetHost, clientDll, repoRoot, port1);
                        Assert.Equal(0, exitCode1);
                        Assert.Contains("CLIENT_SUCCESS", output1);
                        Assert.Contains("High-Performance Laptop", output1);
                    } finally {
                        await StopServerAsync(server1);
                    }
                }

                // ディスク上に永続化データ（WAL/Tsavoriteファイル）が存在することを確認
                var persistedFiles = Directory.GetFiles(persistDir, "*.*", SearchOption.AllDirectories);
                Assert.True(persistedFiles.Length > 0, "No persisted files were found in data directory.");

                // 第2セッション: 同じ永続化ストレージでサーバーを再起動 -> クライアントでデータ復元を確認
                {
                    var (server2, port2) = await StartServerAsync(dotnetHost, serverDll, repoRoot, extraArgs: $"--port 0 --data-dir \"{persistDir}\"");
                    try {
                        var (exitCode2, output2, error2) = await RunClientAsync(dotnetHost, clientDll, repoRoot, port2);
                        Assert.Equal(0, exitCode2);
                        Assert.Contains("CLIENT_SUCCESS", output2);
                        Assert.Contains("High-Performance Laptop", output2);
                    } finally {
                        await StopServerAsync(server2);
                    }
                }
            } finally {
                // 後始末
                try {
                    if (Directory.Exists(persistDir)) {
                        Directory.Delete(persistDir, recursive: true);
                    }
                } catch {
                    // ignore cleanup errors
                }
            }
        }

        private static async Task<(Process Process, int Port)> StartServerAsync(string dotnetHost, string serverDll, string repoRoot, string extraArgs) {
            var serverStartInfo = new ProcessStartInfo {
                FileName = dotnetHost,
                Arguments = $"\"{serverDll}\" {extraArgs}",
                WorkingDirectory = repoRoot,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var serverProcess = new Process { StartInfo = serverStartInfo };
            var portSource = new TaskCompletionSource<int>();

            serverProcess.OutputDataReceived += (sender, args) => {
                if (args.Data != null) {
                    if (args.Data.Contains("listening on 127.0.0.1:", StringComparison.OrdinalIgnoreCase)) {
                        var idx = args.Data.IndexOf("127.0.0.1:", StringComparison.OrdinalIgnoreCase);
                        var portStr = args.Data.Substring(idx + "127.0.0.1:".Length).Split(' ')[0];
                        if (int.TryParse(portStr, out var port)) {
                            portSource.TrySetResult(port);
                        }
                    }
                }
            };

            var serverStarted = serverProcess.Start();
            Assert.True(serverStarted, "Failed to start Server process.");
            serverProcess.BeginOutputReadLine();
            serverProcess.BeginErrorReadLine();

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using (timeoutCts.Token.Register(() => portSource.TrySetCanceled())) {
                var allocatedPort = await portSource.Task;
                Assert.True(allocatedPort > 0, $"Invalid allocated port: {allocatedPort}");
                return (serverProcess, allocatedPort);
            }
        }

        private static async Task<(int ExitCode, string Output, string Error)> RunClientAsync(string dotnetHost, string clientDll, string repoRoot, int port) {
            var clientStartInfo = new ProcessStartInfo {
                FileName = dotnetHost,
                Arguments = $"\"{clientDll}\" --port {port}",
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var clientProcess = new Process { StartInfo = clientStartInfo };
            var clientOutput = new List<string>();
            var clientError = new List<string>();

            clientProcess.OutputDataReceived += (sender, args) => {
                if (args.Data != null) {
                    lock (clientOutput) { clientOutput.Add(args.Data); }
                }
            };
            clientProcess.ErrorDataReceived += (sender, args) => {
                if (args.Data != null) {
                    lock (clientError) { clientError.Add(args.Data); }
                }
            };

            var clientStarted = clientProcess.Start();
            Assert.True(clientStarted, "Failed to start Client process.");
            clientProcess.BeginOutputReadLine();
            clientProcess.BeginErrorReadLine();

            var clientExited = await Task.Run(() => clientProcess.WaitForExit(30000));
            Assert.True(clientExited, "Client process timed out.");

            lock (clientOutput)
            lock (clientError) {
                return (clientProcess.ExitCode, string.Join("\n", clientOutput), string.Join("\n", clientError));
            }
        }

        private static async Task StopServerAsync(Process serverProcess) {
            try {
                await serverProcess.StandardInput.WriteLineAsync("QUIT");
                await serverProcess.StandardInput.FlushAsync();
            } catch {
                // stdin closed
            }

            var serverExited = await Task.Run(() => serverProcess.WaitForExit(10000));
            if (!serverExited) {
                serverProcess.Kill(entireProcessTree: true);
            }
            serverProcess.Dispose();
        }

        private static string ResolveDotNetHost() {
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(processPath) && Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) {
                return processPath;
            }
            var defaultPath = @"C:\Program Files\dotnet\dotnet.exe";
            if (File.Exists(defaultPath)) {
                return defaultPath;
            }
            return "dotnet";
        }

        private static string FindRepositoryRoot() {
            var current = AppContext.BaseDirectory;
            while (current != null) {
                if (File.Exists(Path.Combine(current, "Seamless.slnx"))) {
                    return current;
                }
                current = Path.GetDirectoryName(current);
            }
            return @"G:\GitHub\Seamless";
        }
    }
}
