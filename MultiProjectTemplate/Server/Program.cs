using System.Reflection;
using MultiProjectTemplate.Database;
using MultiProjectTemplate.Database.Tables;
using Seamless.Database;
using Seamless.Database.Client;
using Seamless.Database.Gateway;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Native;

namespace MultiProjectTemplate.Server {
    public static class Program {
        public static async Task Main(string[] args) {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            int port = 51234;
            string? dataDir = null;
            bool persist = false;

            for (int i = 0; i < args.Length; i++) {
                if (args[i] == "--port" && i + 1 < args.Length && int.TryParse(args[i + 1], out var parsedPort)) {
                    port = parsedPort;
                    i++;
                } else if (args[i] == "--persist") {
                    persist = true;
                } else if (args[i] == "--data-dir" && i + 1 < args.Length) {
                    dataDir = args[i + 1];
                    persist = true;
                    i++;
                }
            }

            DatabaseContainerOptions options;
            if (persist) {
                var storagePath = dataDir ?? Path.Combine(AppContext.BaseDirectory, "Data");
                Directory.CreateDirectory(storagePath);
                options = DatabaseContainerOptions.OnStorage(storagePath) with {
                    IndexSize = 128,
                    PageSize = 4096,
                    SegmentSize = 65536,
                    MemorySize = 65536
                };
                Console.WriteLine($"ストレージモード: ディスク永続化 ({storagePath})");
            } else {
                options = DatabaseContainerOptions.InMemory with {
                    IndexSize = 128,
                    PageSize = 4096,
                    SegmentSize = 65536,
                    MemorySize = 65536
                };
                Console.WriteLine("ストレージモード: インメモリ");
            }

            using var database = new TemplateDatabase(options);

            // 初期シードデータ投入（テーブルが空の場合）
            if (database.Items.Count == 0) {
                Console.WriteLine("初期シードデータを投入します...");
                // 表へは取引(tx)からだけ書ける。
                using (var transaction = database.BeginTransaction()) {
                    transaction.Items.Add(new Items { Id = 1, Name = "High-Performance Laptop", Price = 189000m, Version = 1 });
                    transaction.Items.Add(new Items { Id = 2, Name = "Mechanical Keyboard", Price = 19800m, Version = 1 });
                    transaction.Items.Add(new Items { Id = 3, Name = "4K Gaming Monitor", Price = 54800m, Version = 1 });
                    transaction.Items.Add(new Items { Id = 4, Name = "Ergonomic Mouse", Price = 8800m, Version = 1 });
                    transaction.Items.Add(new Items { Id = 5, Name = "USB-C Multi Hub", Price = 4500m, Version = 1 });
                    transaction.Commit();
                }
                Console.WriteLine($"シード完了: {database.Items.Count} 件のアイテムが利用可能です。");
            }

            const string schemaFingerprint = "1.0.0";
            const string databaseName = "TemplateDatabase";

            var registry = new DatabaseRegistry(transactions: null);
            registry.Register(databaseName, new InProcessDatabaseAdapter(database), schemaFingerprint: schemaFingerprint);

            var module = new ItemQueriesModule(context => new ItemQueriesImplementation(database));
            var definitions = module.Describe(schemaFingerprint).ToArray();
            var assemblyVersion = typeof(INativeQueryModule).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";
            var manifest = new NativeModuleManifest("ItemQueries", "", "", "", assemblyVersion, definitions);

            using var server = new NativeQueryServer(registry, port);
            server.Register(manifest, module, assemblyVersion);
            server.Start();

            Console.WriteLine($"Seamless Native RPC Server listening on 127.0.0.1:{server.Port} (Database: {databaseName})");
            Console.WriteLine("READY");

            // シャットダウン待機（シグナルまたは標準入力 EOF）
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, eventArgs) => {
                eventArgs.Cancel = true;
                cts.Cancel();
            };

            var exitSignal = new TaskCompletionSource();
            using var reg = cts.Token.Register(() => exitSignal.TrySetResult());

            // stdin 読み取りタスク（パイプ経由でプロセス起動された場合の終了検知）
            _ = Task.Run(async () => {
                try {
                    while (!cts.IsCancellationRequested) {
                        var line = await Console.In.ReadLineAsync();
                        if (line == null || line.Trim().Equals("QUIT", StringComparison.OrdinalIgnoreCase)) {
                            cts.Cancel();
                            break;
                        }
                    }
                } catch {
                    cts.Cancel();
                }
            });

            await exitSignal.Task;
            Console.WriteLine("Server shutting down gracefully...");
        }
    }
}
