using MultiProjectTemplate.Database;
using Seamless.Database.Client;

namespace MultiProjectTemplate.Client {
    public static class Program {
        public static async Task<int> Main(string[] args) {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            int port = 51234;
            for (int i = 0; i < args.Length; i++) {
                if (args[i] == "--port" && i + 1 < args.Length && int.TryParse(args[i + 1], out var parsedPort)) {
                    port = parsedPort;
                    i++;
                }
            }

            Console.WriteLine($"Seamless Native RPC Client starting... connecting to 127.0.0.1:{port}");

            const string schemaFingerprint = "1.0.0";
            await using var connection = new NativeQueryClient("127.0.0.1", port);
            var client = new ItemQueriesClient(connection, schemaFingerprint);

            // 1. ID による単一検索
            Console.WriteLine("Executing: FindByIdAsync(Id = 1)...");
            var item1 = await client.FindByIdAsync(new FindItemRequest(1));
            Console.WriteLine($"[FindById 成功] Id={item1.Id}, Name={item1.Name}, Price={item1.Price:C}");
            if (item1.Id != 1 || item1.Name != "High-Performance Laptop") {
                Console.Error.WriteLine("Error: Item 1 の検証に失敗しました。");
                return 1;
            }

            // 2. 価格範囲検索（10,000円〜60,000円、ストリーミング受信）
            Console.WriteLine("Executing: SearchByPriceAsync(10000m, 60000m)...");
            var itemsInRange = new List<ItemDto>();
            await foreach (var item in client.SearchByPriceAsync(new SearchByPriceRequest(10000m, 60000m))) {
                itemsInRange.Add(item);
                Console.WriteLine($"  - Id={item.Id}, Name={item.Name}, Price={item.Price:C}");
            }
            Console.WriteLine($"[SearchByPrice 成功] 取得件数: {itemsInRange.Count} 件");
            if (itemsInRange.Count != 2 || itemsInRange[0].Id != 2 || itemsInRange[1].Id != 3) {
                Console.Error.WriteLine("Error: 価格範囲検索結果の件数または順序が一致しません。");
                return 2;
            }

            // 3. 全件数取得
            Console.WriteLine("Executing: CountAllAsync()...");
            int count = await client.CountAllAsync(new CountAllRequest());
            Console.WriteLine($"[CountAll 成功] 全件数: {count} 件");
            if (count != 5) {
                Console.Error.WriteLine($"Error: 全件数の検証に失敗しました（期待値: 5, 実際: {count}）。");
                return 3;
            }

            Console.WriteLine("CLIENT_SUCCESS: すべての AOT Native RPC クエリの実行および結果検証が 100% 成功しました。");
            return 0;
        }
    }
}
