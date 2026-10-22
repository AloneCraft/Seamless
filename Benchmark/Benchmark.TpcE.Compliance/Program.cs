namespace Benchmark.TpcE.Compliance;
/// <summary>
/// TPC-E 準拠リハーサルの入口。既存の Benchmark.TpcE(不具合探しの模擬)とは別の exe。
/// ここで出す数字は TPC-E 由来の非監査値で、公式結果とは比較できない。
/// </summary>
public static class Program{
    public const string Notice="TPC-E 由来・非監査・公式結果と比較不可(tpsE 相当)";
    private static readonly string[] EGenCommands=["load","drive","measure","consistency","acid","sut"];
    public static int Main(string[] args){
        Console.OutputEncoding=System.Text.Encoding.UTF8;
        var command="help";
        if(args.Length>0){
            command=args[0];
        }
        Console.WriteLine(Notice);
        if(command=="help"||command=="--help"){
            return Help();
        }
        if(Array.IndexOf(EGenCommands,command)>=0){
            Console.Error.WriteLine($"{command}: EGen 1.14.0 が必要です(Work/TpcE/egen/)。まだ組み込んでいません。");
            return 6;
        }
        Console.Error.WriteLine($"不明なコマンド: {command}");
        return 3;
    }
    private static int Help(){
        Console.WriteLine("使い方: Benchmark.TpcE.Compliance <command>");
        Console.WriteLine("  load / consistency / sut / drive / acid / measure   EGen を組み込んだ後に使える");
        Console.WriteLine("  audit-report                                         点検表(EGen 不要)");
        return 0;
    }
}
