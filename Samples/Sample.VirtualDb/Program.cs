using MemoryPack;
using Seamless.Collections.Persisted;
using Seamless.Collections.Tsavorite;
using Seamless.Execution;
using Store=Seamless.Execution.Store;

internal static class Program{
    public static void Main(string[] args){
        var backend=args.FirstOrDefault()?.ToLowerInvariant() switch{
            null or "tsavorite"=>Backend.Tsavorite,
            "persisted"=>Backend.Persisted,
            var value=>throw new ArgumentException($"Unknown backend '{value}'. Use tsavorite or persisted."),
        };
        var dataPath=args.Length>1?args[1]:Path.Combine(AppContext.BaseDirectory,"VirtualDbData",backend.ToString());
        Directory.CreateDirectory(dataPath);

        using var database=Store.Open(dataPath);
        var queries=new StockQueries();
        switch(backend){
            case Backend.Tsavorite: RunTsavorite(database,queries); break;
            case Backend.Persisted: RunPersisted(database,queries); break;
            default: throw new ArgumentOutOfRangeException();
        }
    }

    private static void RunTsavorite(Store store,StockQueries queries){
        using var table=store.CreateTsavorite<int,Stock>("securities-tsavorite",expectedCount:8);
        using var rows=table.Create();
        if(rows.Count==0){
            foreach(var row in SeedData()) rows.Add(row.Id,row);
            store.Save();
        }
        PrintReport(Backend.Tsavorite,store,rows.Values,queries);
    }

    private static void RunPersisted(Store store,StockQueries queries){
        using var table=store.CreatePersistedDictionary<int,Stock>("securities-persisted",initialCapacity:8);
        if(table.Count==0){
            foreach(var row in SeedData()) table.Add(row.Id,row);
            store.Save();
        }
        PrintReport(Backend.Persisted,store,table.Values,queries);
    }

    private static void PrintReport(Backend backend,Store database,IEnumerable<Stock> stocks,StockQueries queries){
        var active=queries.QueryActiveStocks(stocks);
        var top=queries.QueryTopGainers(stocks);
        Console.WriteLine($"Backend: {backend}");
        Console.WriteLine($"Checkpoint: {database.CurrentCheckpoint?.ToString() ?? "none"}");
        Console.WriteLine($"Active securities: {active.Length}");
        foreach(var stock in top){
            var change=stock.ChangePercent.ToString("+0.00;-0.00;0.00",System.Globalization.CultureInfo.InvariantCulture);
            Console.WriteLine($"{stock.Symbol,-6} {stock.Name,-20} ${stock.Price,8:F2} ({change}%)");
        }
    }

    private static Stock[] SeedData()=>[
        new(1,"NOVA","Nova Systems",142.75m,2.4m,true),
        new(2,"ORBT","Orbit Networks",88.20m,1.1m,true),
        new(3,"PINE","Pine Robotics",54.10m,-0.8m,true),
        new(4,"QUAD","Quadrant Energy",206.40m,3.7m,true),
        new(5,"RIVR","River Financial",73.95m,0.0m,false),
        new(6,"SOLR","Solaris Group",119.30m,1.8m,true),
        new(7,"TIDE","Tidewater Labs",31.60m,-2.2m,true),
        new(8,"UMBR","Umbra Medical",97.50m,2.4m,true),
    ];
}

enum Backend{Tsavorite,Persisted}

[MemoryPackable]
readonly partial record struct Stock(int Id,string Symbol,string Name,decimal Price,decimal ChangePercent,bool IsTrading);

sealed class StockQueries{
    public Stock[] QueryActiveStocks(IEnumerable<Stock> rows)=>rows
        .Where(stock=>stock.IsTrading)
        .OrderBy(stock=>stock.Symbol,StringComparer.Ordinal)
        .ToArray();

    public Stock[] QueryTopGainers(IEnumerable<Stock> rows)=>rows
        .Where(stock=>stock.IsTrading&&stock.ChangePercent>0)
        .OrderByDescending(stock=>stock.ChangePercent)
        .ThenBy(stock=>stock.Symbol,StringComparer.Ordinal)
        .Take(5)
        .ToArray();
}
