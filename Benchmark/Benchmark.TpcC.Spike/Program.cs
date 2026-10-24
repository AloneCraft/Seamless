using System.Diagnostics;
using System.Globalization;
namespace Benchmark.TpcC.Spike;
/// <summary>
/// TPC-C の区画並列 SUT の前提を確かめる実験(C3)。
/// exp1 区画ごとの同時書き込みと同時保存 / exp2 排他用コレクションの所有スレッドの受け渡し / exp3 LightEpoch の上限 /
/// exp4 並列の開き直しと強制終了からの復旧 / exp5 Remove のあとの保存→開き直し(found-bugs #2) / exp6 保存の大きさと時間。
/// 既定で CPU 14 番(性能コア)1 個に固定する。--cpu で変える(例: --cpu 12-15)。
/// </summary>
public static class Program{
    public static int Main(string[] args){
        if(args.Length==0){
            Console.Error.WriteLine("usage: exp1|exp2|exp3|exp4|exp4child|exp5|exp6 [--key value]...");
            return 2;
        }
        var options=new Args(args[1..]);
        Affinity.Apply(options.Str("cpu","14"));
        Console.WriteLine($"# {args[0]} {string.Join(' ',args[1..])} | pid={Environment.ProcessId} cpus={Environment.ProcessorCount} affinity=0x{(long)Process.GetCurrentProcess().ProcessorAffinity:X} {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        try{
            return args[0] switch{
                "exp1"=>Exp1.Run(options),
                "exp2"=>Exp2.Run(options),
                "exp3"=>Exp3.Run(options),
                "exp4"=>Exp4.Run(options),
                "exp4child"=>Exp4.Child(options),
                "exp5"=>Exp5.Run(options),
                "exp6"=>Exp6.Run(options),
                _=>throw new ArgumentException("unknown command "+args[0]),
            };
        }catch(Exception error){
            Console.WriteLine("FAIL(exception) "+error);
            return 1;
        }
    }
}
public sealed class Args{
    private readonly Dictionary<string,string> values=new(StringComparer.Ordinal);
    public Args(string[] args){
        for(var index=0;index<args.Length;index++){
            var key=args[index];
            if(!key.StartsWith("--",StringComparison.Ordinal)){
                throw new ArgumentException("unexpected "+key);
            }
            var value="true";
            if(index+1<args.Length&&!args[index+1].StartsWith("--",StringComparison.Ordinal)){
                value=args[index+1];
                index++;
            }
            this.values[key[2..]]=value;
        }
    }
    public string Str(string key,string fallback){
        if(this.values.TryGetValue(key,out var value)){
            return value;
        }
        return fallback;
    }
    public int Int(string key,int fallback)=>int.Parse(this.Str(key,fallback.ToString(CultureInfo.InvariantCulture)),CultureInfo.InvariantCulture);
    public long Long(string key,long fallback)=>long.Parse(this.Str(key,fallback.ToString(CultureInfo.InvariantCulture)),CultureInfo.InvariantCulture);
    public double Double(string key,double fallback)=>double.Parse(this.Str(key,fallback.ToString(CultureInfo.InvariantCulture)),CultureInfo.InvariantCulture);
    public bool Bool(string key,bool fallback){
        var text=this.Str(key,fallback.ToString());
        return text is "1" or "true" or "True" or "yes";
    }
}
public static class Affinity{
    /// <summary>"14" や "12-15"、"12,14" の形の CPU 番号に固定する。</summary>
    public static void Apply(string spec){
        long mask=0;
        foreach(var part in spec.Split(',',StringSplitOptions.RemoveEmptyEntries)){
            var range=part.Split('-');
            var first=int.Parse(range[0],CultureInfo.InvariantCulture);
            var last=first;
            if(range.Length>1){
                last=int.Parse(range[1],CultureInfo.InvariantCulture);
            }
            for(var cpu=first;cpu<=last;cpu++){
                mask|=1L<<cpu;
            }
        }
        if(mask!=0){
            Process.GetCurrentProcess().ProcessorAffinity=(nint)mask;
        }
    }
}
public static class Report{
    public static string Ms(TimeSpan value)=>value.TotalMilliseconds.ToString("0.0",CultureInfo.InvariantCulture);
    public static string Stats(IEnumerable<TimeSpan> values){
        var sorted=values.Select(static value=>value.TotalMilliseconds).Order().ToArray();
        if(sorted.Length==0){
            return "n=0";
        }
        return string.Create(CultureInfo.InvariantCulture,$"n={sorted.Length} min={sorted[0]:0.0} med={sorted[sorted.Length/2]:0.0} p90={sorted[(int)(sorted.Length*0.9)]:0.0} max={sorted[^1]:0.0} ms");
    }
    public static long DirectoryBytes(string path){
        if(!Directory.Exists(path)){
            return 0;
        }
        long total=0;
        foreach(var file in Directory.EnumerateFiles(path,"*",SearchOption.AllDirectories)){
            try{
                total+=new FileInfo(file).Length;
            }catch(FileNotFoundException){
            }
        }
        return total;
    }
    public static string Mb(long bytes)=>(bytes/1024.0/1024.0).ToString("0.00",CultureInfo.InvariantCulture)+"MB";
    public static string NewRoot(Args options,string name){
        var root=options.Str("root",Path.Combine(@"C:\tpccspike",name,DateTime.Now.ToString("yyyyMMdd-HHmmss",CultureInfo.InvariantCulture)));
        if(Directory.Exists(root)&&!options.Bool("keep",false)){
            Directory.Delete(root,true);
        }
        Directory.CreateDirectory(root);
        return root;
    }
}
