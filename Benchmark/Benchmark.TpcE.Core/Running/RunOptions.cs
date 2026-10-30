using System.Globalization;
using System.Text;
namespace Benchmark.TpcE.Core;
/// <summary>実行の設定。再現コマンドには、結果に影響する項目をすべて書く(<see cref="ToArguments"/>)。</summary>
public sealed record RunOptions{
    public string Command{get;init;}="run";
    public ulong Seed{get;init;}=1;
    public int Customers{get;init;}=1000;
    public int InitialTradeDays{get;init;}=1;
    public int MarketDays{get;init;}=260;
    public int NewsItemBytes{get;init;}=1024;
    /// <summary>流す CE の取引の件数。</summary>
    public long Transactions{get;init;}=15_000;
    /// <summary>時間の集計から外す最初の CE の件数(正しさの検査には含める)。</summary>
    public long Warmup{get;init;}=1_000;
    /// <summary>流す組み合わせ(略称 TL,TF,PL,PF)。</summary>
    public IReadOnlyList<string> Variants{get;init;}=["TL","TF","PL","PF"];
    /// <summary>何件の CE ごとに保存するか(0 は保存しない)。</summary>
    public long SaveEvery{get;init;}=5_000;
    /// <summary>保存の何回に 1 回開き直すか(0 は開き直さない)。最後には必ず開き直す。</summary>
    public int ReopenEvery{get;init;}=2;
    public long MemoryMegabytes{get;init;}
    public long DiskGigabytes{get;init;}=16;
    public string? WorkDirectory{get;init;}
    public bool KeepStores{get;init;}
    /// <summary>この通し番号の取引の直前で止める(0 は止めない)。</summary>
    public long StopAt{get;init;}
    /// <summary>この通し番号以降の取引を詳しく記録する(0 は記録しない)。</summary>
    public long TraceFrom{get;init;}
    /// <summary>故障注入 ENGINE:TXN:KIND(検出の確認用)。</summary>
    public string? InjectFault{get;init;}
    /// <summary>強制終了の検査の周期の数(組み合わせごと)。</summary>
    public int Cycles{get;init;}=12;
    public int? Cpu{get;init;}
    public string Priority{get;init;}="belownormal";
    public TpcEScale Scale=>new(){
        Customers=this.Customers,
        InitialTradeDays=this.InitialTradeDays,
        MarketDays=this.MarketDays,
        NewsItemBytes=this.NewsItemBytes,
        ExpectedRunTrades=Math.Max(1_000,this.Transactions/10),
    };
    /// <summary>実行に影響する引数をすべて並べる(再現コマンド用)。</summary>
    public IReadOnlyList<string> ToArguments(){
        var result=new List<string>{
            this.Command,
            "--seed",this.Seed.ToString(CultureInfo.InvariantCulture),
            "--customers",this.Customers.ToString(CultureInfo.InvariantCulture),
            "--initial-trade-days",this.InitialTradeDays.ToString(CultureInfo.InvariantCulture),
            "--market-days",this.MarketDays.ToString(CultureInfo.InvariantCulture),
            "--news-item-bytes",this.NewsItemBytes.ToString(CultureInfo.InvariantCulture),
            "--transactions",this.Transactions.ToString(CultureInfo.InvariantCulture),
            "--warmup",this.Warmup.ToString(CultureInfo.InvariantCulture),
            "--variants",string.Join(",",this.Variants),
            "--save-every",this.SaveEvery.ToString(CultureInfo.InvariantCulture),
            "--reopen-every",this.ReopenEvery.ToString(CultureInfo.InvariantCulture),
            "--disk-gb",this.DiskGigabytes.ToString(CultureInfo.InvariantCulture),
        };
        if(this.MemoryMegabytes>0){
            result.Add("--memory-mb");
            result.Add(this.MemoryMegabytes.ToString(CultureInfo.InvariantCulture));
        }
        if(this.StopAt>0){
            result.Add("--stop-at");
            result.Add(this.StopAt.ToString(CultureInfo.InvariantCulture));
        }
        if(this.TraceFrom>0){
            result.Add("--trace-from");
            result.Add(this.TraceFrom.ToString(CultureInfo.InvariantCulture));
        }
        if(this.InjectFault is not null){
            result.Add("--inject-fault");
            result.Add(this.InjectFault);
        }
        if(this.KeepStores){
            result.Add("--keep-stores");
        }
        if(this.Command=="crash"){
            result.Add("--cycles");
            result.Add(this.Cycles.ToString(CultureInfo.InvariantCulture));
        }
        return result;
    }
    public string ToCommandLine(){
        var builder=new StringBuilder("dotnet run -c Release --project Benchmark/Benchmark.TpcE --");
        foreach(var argument in this.ToArguments()){
            builder.Append(' ');
            builder.Append(argument);
        }
        return builder.ToString();
    }
    /// <summary>引数を解析する。誤りは <see cref="ArgumentException"/>。</summary>
    public static RunOptions Parse(IReadOnlyList<string> args){
        var options=new RunOptions();
        var index=0;
        if(index<args.Count&&!args[index].StartsWith("--",StringComparison.Ordinal)){
            options=options with{Command=args[index]};
            index++;
        }
        while(index<args.Count){
            var name=args[index++];
            string Value(){
                if(index>=args.Count){
                    throw new ArgumentException($"{name} needs a value.");
                }
                return args[index++];
            }
            options=name switch{
                "--seed"=>options with{Seed=ulong.Parse(Value(),CultureInfo.InvariantCulture)},
                "--customers"=>options with{Customers=PositiveInt(name,Value())},
                "--initial-trade-days"=>options with{InitialTradeDays=PositiveInt(name,Value())},
                "--market-days"=>options with{MarketDays=PositiveInt(name,Value())},
                "--news-item-bytes"=>options with{NewsItemBytes=PositiveInt(name,Value())},
                "--transactions"=>options with{Transactions=long.Parse(Value(),CultureInfo.InvariantCulture)},
                "--warmup"=>options with{Warmup=long.Parse(Value(),CultureInfo.InvariantCulture)},
                "--variants"=>options with{Variants=ParseVariants(Value())},
                "--save-every"=>options with{SaveEvery=long.Parse(Value(),CultureInfo.InvariantCulture)},
                "--reopen-every"=>options with{ReopenEvery=int.Parse(Value(),CultureInfo.InvariantCulture)},
                "--memory-mb"=>options with{MemoryMegabytes=long.Parse(Value(),CultureInfo.InvariantCulture)},
                "--disk-gb"=>options with{DiskGigabytes=long.Parse(Value(),CultureInfo.InvariantCulture)},
                "--work"=>options with{WorkDirectory=Value()},
                "--keep-stores"=>options with{KeepStores=true},
                "--stop-at"=>options with{StopAt=long.Parse(Value(),CultureInfo.InvariantCulture)},
                "--trace-from"=>options with{TraceFrom=long.Parse(Value(),CultureInfo.InvariantCulture)},
                "--inject-fault"=>options with{InjectFault=Value()},
                "--cpu"=>options with{Cpu=int.Parse(Value(),CultureInfo.InvariantCulture)},
                "--cycles"=>options with{Cycles=PositiveInt(name,Value())},
                "--priority"=>options with{Priority=Value()},
                _=>throw new ArgumentException($"Unknown option {name}."),
            };
        }
        if(options.MarketDays<TpcESpec.MaxDayLength){
            throw new ArgumentException($"--market-days must be at least {TpcESpec.MaxDayLength}.");
        }
        return options;
    }
    private static int PositiveInt(string name,string text){
        var value=int.Parse(text,CultureInfo.InvariantCulture);
        if(value<=0){
            throw new ArgumentException($"{name} must be positive.");
        }
        return value;
    }
    private static IReadOnlyList<string> ParseVariants(string text){
        if(string.Equals(text,"all",StringComparison.OrdinalIgnoreCase)){
            return ["TL","TF","PL","PF"];
        }
        var result=new List<string>();
        foreach(var part in text.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)){
            var upper=part.ToUpperInvariant();
            if(upper is not ("TL" or "TF" or "PL" or "PF")){
                throw new ArgumentException($"Unknown variant {part}. Use TL, TF, PL, PF or all.");
            }
            if(!result.Contains(upper)){
                result.Add(upper);
            }
        }
        if(result.Count==0){
            throw new ArgumentException("--variants is empty.");
        }
        return result;
    }
}
