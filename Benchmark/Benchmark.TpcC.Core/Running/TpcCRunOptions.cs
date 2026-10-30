using System.Globalization;
using System.Text;
namespace Benchmark.TpcC.Core;
/// <summary>実行の設定。再現コマンドには、結果に影響する項目をすべて書く(<see cref="ToArguments"/>)。</summary>
public sealed record TpcCRunOptions{
    public string Command{get;init;}="run";
    public ulong Seed{get;init;}=1;
    public int Warehouses{get;init;}=4;
    /// <summary>区画の数(倉庫の数を超える分は使われない)。</summary>
    public int Partitions{get;init;}=2;
    /// <summary>品目の数。既定は不具合探し向けに縮小(仕様は 100,000)。</summary>
    public int Items{get;init;}=1_000;
    /// <summary>地区あたりの顧客数。既定は不具合探し向けに縮小(仕様は 3,000)。</summary>
    public int CustomersPerDistrict{get;init;}=300;
    public TpcCMode Mode{get;init;}=TpcCMode.HammerDbMssql;
    /// <summary>入力を作る端末の数(HammerDB の VU)。仕様の形では 10×倉庫数 以下。</summary>
    public int Terminals{get;init;}=8;
    public long Transactions{get;init;}=15_000;
    /// <summary>時間の集計から外す最初の取引の件数(正しさの検査には含める)。</summary>
    public long Warmup{get;init;}=1_000;
    /// <summary>流す組み合わせ(略称 TL,TF,PL,PF)。</summary>
    public IReadOnlyList<string> Variants{get;init;}=["TL","TF","PL","PF"];
    /// <summary>区画が 2 つ以上のとき、区画 1 つの TF を加えて突き合わせる(区画の数に依らない結果・状態になることの確認)。</summary>
    public bool Shadow{get;init;}=true;
    /// <summary>runの各取引を専用threadへ所有移管して実行する。</summary>
    public bool HopThreads{get;init;}
    /// <summary>何件の取引ごとに保存するか(0 は保存しない)。</summary>
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
    public TpcCScale Scale=>this.ScaleWith(this.Partitions);
    public TpcCScale ScaleWith(int partitions)=>new(){
        Warehouses=this.Warehouses,
        Partitions=partitions,
        Items=this.Items,
        CustomersPerDistrict=this.CustomersPerDistrict,
        ExpectedRunOrders=Math.Max(1_000,this.Transactions/2),
    };
    /// <summary>実行に影響する引数をすべて並べる(再現コマンド用)。</summary>
    public IReadOnlyList<string> ToArguments(){
        var result=new List<string>{
            this.Command,
            "--seed",this.Seed.ToString(CultureInfo.InvariantCulture),
            "--warehouses",this.Warehouses.ToString(CultureInfo.InvariantCulture),
            "--partitions",this.Partitions.ToString(CultureInfo.InvariantCulture),
            "--items",this.Items.ToString(CultureInfo.InvariantCulture),
            "--customers-per-district",this.CustomersPerDistrict.ToString(CultureInfo.InvariantCulture),
            "--mode",ModeName(this.Mode),
            "--terminals",this.Terminals.ToString(CultureInfo.InvariantCulture),
            "--transactions",this.Transactions.ToString(CultureInfo.InvariantCulture),
            "--warmup",this.Warmup.ToString(CultureInfo.InvariantCulture),
            "--variants",string.Join(",",this.Variants),
            "--save-every",this.SaveEvery.ToString(CultureInfo.InvariantCulture),
            "--reopen-every",this.ReopenEvery.ToString(CultureInfo.InvariantCulture),
            "--disk-gb",this.DiskGigabytes.ToString(CultureInfo.InvariantCulture),
        };
        result.AddRange(["--priority",this.Priority]);
        if(this.HopThreads){
            result.AddRange(["--hop-threads","true"]);
        }
        if(this.Cpu is int cpu){
            result.AddRange(["--cpu",cpu.ToString(CultureInfo.InvariantCulture)]);
        }
        if(!this.Shadow){
            result.Add("--no-shadow");
        }
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
        var builder=new StringBuilder("dotnet run -c Release --project Benchmark/Benchmark.TpcC --");
        foreach(var argument in this.ToArguments()){
            builder.Append(' ');
            builder.Append(argument);
        }
        return builder.ToString();
    }
    public static string ModeName(TpcCMode mode){
        if(mode==TpcCMode.Spec){
            return "spec";
        }
        return "hammerdb";
    }
    /// <summary>引数を解析する。誤りは <see cref="ArgumentException"/>。</summary>
    public static TpcCRunOptions Parse(IReadOnlyList<string> args){
        var options=new TpcCRunOptions();
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
                "--warehouses"=>options with{Warehouses=PositiveInt(name,Value())},
                "--partitions"=>options with{Partitions=PositiveInt(name,Value())},
                "--items"=>options with{Items=PositiveInt(name,Value())},
                "--customers-per-district"=>options with{CustomersPerDistrict=PositiveInt(name,Value())},
                "--mode"=>options with{Mode=ParseMode(Value())},
                "--terminals"=>options with{Terminals=PositiveInt(name,Value())},
                "--transactions"=>options with{Transactions=long.Parse(Value(),CultureInfo.InvariantCulture)},
                "--warmup"=>options with{Warmup=long.Parse(Value(),CultureInfo.InvariantCulture)},
                "--variants"=>options with{Variants=ParseVariants(Value())},
                "--no-shadow"=>options with{Shadow=false},
                "--hop-threads"=>options with{HopThreads=ParseHopThreads(Value())},
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
        if(options.Transactions<0||options.Warmup<0||options.SaveEvery<0||options.ReopenEvery<0||options.MemoryMegabytes<0||options.DiskGigabytes<=0||options.StopAt<0||options.TraceFrom<0){
            throw new ArgumentException("Counts and budgets must be nonnegative, and --disk-gb must be positive.");
        }
        if(options.HopThreads&&options.Command!="run"){
            throw new ArgumentException("--hop-threads true is supported by run only.");
        }
        if(options.Command=="crash"&&options.SaveEvery==0){
            throw new ArgumentException("crash needs a positive --save-every.");
        }
        if(options.Cpu is int cpu&&(cpu<0||cpu>=Math.Min(Environment.ProcessorCount,64))){
            throw new ArgumentException("--cpu is outside the supported logical processor range.");
        }
        if(options.Priority.ToLowerInvariant() is not ("normal" or "idle" or "belownormal")){
            throw new ArgumentException("--priority must be normal, idle or belownormal.");
        }
        if(options.Mode==TpcCMode.Spec&&options.Terminals>options.Warehouses*TpcCScale.DistrictsPerWarehouse){
            throw new ArgumentException($"--terminals must be at most {options.Warehouses*TpcCScale.DistrictsPerWarehouse} (10 per warehouse) in spec mode.");
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
    private static bool ParseHopThreads(string text){
        if(!bool.TryParse(text,out var value)){
            throw new ArgumentException("--hop-threads must be true or false.");
        }
        return value;
    }
    private static TpcCMode ParseMode(string text){
        switch(text.ToLowerInvariant()){
            case "spec":
                return TpcCMode.Spec;
            case "hammerdb":
            case "hammerdb-mssql":
                return TpcCMode.HammerDbMssql;
            default:
                throw new ArgumentException($"Unknown mode {text}. Use spec or hammerdb.");
        }
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
