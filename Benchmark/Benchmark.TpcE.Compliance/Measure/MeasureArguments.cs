using System.Globalization;
namespace Benchmark.TpcE.Compliance;
/// <summary>`measure` コマンドの引数。</summary>
internal sealed record MeasureArguments{
    private const string DefaultWork=@"G:\GitHub\Seamless\Work\TpcE";
    /// <summary>作業フォルダ(データ・ストア・結果)。速いドライブに置く。ストアと再実行ログは fsync が多いので特に。</summary>
    public string Work{get;init;}=Path.Combine(Path.GetTempPath(),"tpce-compliance");
    /// <summary>build-egen.ps1 の出力(EGenLoader.exe と EGenDriver.exe)のフォルダ。</summary>
    public string EGenBuild{get;init;}=Path.Combine(Environment.GetEnvironmentVariable("TPCE_WORK") ?? DefaultWork,"egen-build");
    /// <summary>EGen の入力ファイル(flat_in)のフォルダ。</summary>
    public string EGenInput{get;init;}=Path.Combine(Environment.GetEnvironmentVariable("TPCE_WORK") ?? DefaultWork,"egen","flat_in");
    /// <summary>既存の実行フォルダ。指定すると新しく作らず、そのフォルダのストアと再実行ログから SUT を起動し直す(取り込みを省く。中断した実行の続きや、取り込みに時間がかかる大きなデータ用)。</summary>
    public string? Resume{get;init;}
    public int Customers{get;init;}=1000;
    public int Days{get;init;}=1;
    public int Users{get;init;}=8;
    /// <summary>立ち上がり。待っている指値注文が溜まって定常になるまでの時間。1 分では Trade-Result の比率が約1割低く出た(20 分立ち上げた計測では許容幅に入った)。</summary>
    public double RampMinutes{get;init;}=20;
    public double MeasureMinutes{get;init;}=10;
    public double DownMinutes{get;init;}=0.5;
    /// <summary>目標の tpsE。0 以下で公称(顧客数÷500)にする。</summary>
    public double TargetTpse{get;init;}
    /// <summary>true ならペース調整なし(最大速度)。</summary>
    public bool Unthrottled{get;init;}
    public int DataMaintenanceSeconds{get;init;}=60;
    public double CheckpointMinutes{get;init;}=7.5;
    public int NewsItemBytes{get;init;}=100_000;
    public long MemoryMegabytes{get;init;}=2048;
    public long DiskGigabytes{get;init;}=64;
    public double LoadTimeoutMinutes{get;init;}=240;
    /// <summary>`acid`: 処理の喪失の試験を繰り返す回数。</summary>
    public int Rounds{get;init;}=2;
    /// <summary>`acid`: 運転側が動き始めてから SUT を強制停止するまでの秒数。</summary>
    public double KillAfterSeconds{get;init;}=15;
    /// <summary>運転側に渡す目標の tpsE。ペース調整なしは 0。</summary>
    public double EffectiveTargetTpse(){
        if(this.Unthrottled){
            return 0;
        }
        if(this.TargetTpse>0){
            return this.TargetTpse;
        }
        return ComplianceSpec.NominalTpse(this.Customers);
    }
    public static MeasureArguments Parse(IReadOnlyList<string> args){
        var result=new MeasureArguments();
        var index=0;
        while(index<args.Count){
            var name=args[index++];
            if(name=="--unthrottled"){
                result=result with{Unthrottled=true};
                continue;
            }
            if(index>=args.Count){
                throw new ArgumentException($"{name} needs a value.");
            }
            var value=args[index++];
            result=name switch{
                "--work"=>result with{Work=value},
                "--resume"=>result with{Resume=value},
                "--egen-build"=>result with{EGenBuild=value},
                "--egen-input"=>result with{EGenInput=value},
                "--customers"=>result with{Customers=int.Parse(value,CultureInfo.InvariantCulture)},
                "--days"=>result with{Days=int.Parse(value,CultureInfo.InvariantCulture)},
                "--users"=>result with{Users=int.Parse(value,CultureInfo.InvariantCulture)},
                "--ramp-minutes"=>result with{RampMinutes=double.Parse(value,CultureInfo.InvariantCulture)},
                "--measure-minutes"=>result with{MeasureMinutes=double.Parse(value,CultureInfo.InvariantCulture)},
                "--down-minutes"=>result with{DownMinutes=double.Parse(value,CultureInfo.InvariantCulture)},
                "--tpse"=>result with{TargetTpse=double.Parse(value,CultureInfo.InvariantCulture)},
                "--dm-seconds"=>result with{DataMaintenanceSeconds=int.Parse(value,CultureInfo.InvariantCulture)},
                "--checkpoint-minutes"=>result with{CheckpointMinutes=double.Parse(value,CultureInfo.InvariantCulture)},
                "--news-bytes"=>result with{NewsItemBytes=int.Parse(value,CultureInfo.InvariantCulture)},
                "--memory-mb"=>result with{MemoryMegabytes=long.Parse(value,CultureInfo.InvariantCulture)},
                "--disk-gb"=>result with{DiskGigabytes=long.Parse(value,CultureInfo.InvariantCulture)},
                "--rounds"=>result with{Rounds=int.Parse(value,CultureInfo.InvariantCulture)},
                "--kill-after-seconds"=>result with{KillAfterSeconds=double.Parse(value,CultureInfo.InvariantCulture)},
                "--load-timeout-minutes"=>result with{LoadTimeoutMinutes=double.Parse(value,CultureInfo.InvariantCulture)},
                _=>throw new ArgumentException($"Unknown option {name}."),
            };
        }
        if(result.Customers<=0||result.Customers%1000!=0){
            throw new ArgumentException("--customers must be a positive multiple of 1000 (the TPC-E load unit).");
        }
        if(result.Users<=0||result.Days<=0){
            throw new ArgumentException("--users and --days must be positive.");
        }
        return result;
    }
}
