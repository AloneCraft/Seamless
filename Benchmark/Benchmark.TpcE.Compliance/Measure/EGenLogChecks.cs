using System.Text;
using System.Text.RegularExpressions;
namespace Benchmark.TpcE.Compliance;
/// <summary>
/// 仕様 10.6.2.2: EGenLogger の出力に `NO` があれば、その設定は現行の TPC-E 仕様に合わない(5.2.9)。
/// 運転側のログ(EGenDriver.log)を読み、`NO` の行と、各オブジェクトの『valid for publication』の判定を点検する。
/// </summary>
internal static class EGenLogChecks{
    public static CheckResult Scan(string logPath){
        if(!File.Exists(logPath)){
            return new CheckResult("10.6.2.2","EGenLogger の出力に NO が無い",CheckOutcome.NotExercised,$"ログが無い: {logPath}");
        }
        return Scan(File.ReadLines(logPath));
    }
    public static CheckResult Scan(IEnumerable<string> lines){
        var noLines=new SortedSet<string>(StringComparer.Ordinal);
        var invalidObjects=new SortedSet<string>(StringComparer.Ordinal);
        var validObjects=0;
        foreach(var line in lines){
            // 設定の表: `名前:   既定  現在  NO`(最後の欄が既定値かどうか)
            if(Regex.IsMatch(line,@"\tNO\s*$")){
                noLines.Add(Normalize(line));
            }
            // `... constructed using ... (valid for publication: YES|NO).`
            var match=Regex.Match(line,@"(\w+) object constructed using .*\(valid for publication: (YES|NO)\)");
            if(match.Success){
                if(match.Groups[2].Value=="YES"){
                    validObjects++;
                }else{
                    invalidObjects.Add(match.Groups[1].Value);
                }
            }
        }
        if(noLines.Count==0&&invalidObjects.Count==0){
            return new CheckResult("10.6.2.2","EGenLogger の出力に NO が無い",CheckOutcome.Pass,$"設定はすべて既定または許容。公開に有効な EGen オブジェクト {validObjects} 個");
        }
        var builder=new StringBuilder();
        foreach(var line in noLines){
            builder.Append(line).Append("; ");
        }
        foreach(var name in invalidObjects){
            builder.Append(name).Append(" は公開に有効でない; ");
        }
        return new CheckResult("10.6.2.2","EGenLogger の出力に NO が無い",CheckOutcome.Fail,builder.ToString().TrimEnd());
    }
    private static string Normalize(string line)=>Regex.Replace(line.Trim(),@"\s+"," ");
}
