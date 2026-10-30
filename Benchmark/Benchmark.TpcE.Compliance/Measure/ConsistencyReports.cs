using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
namespace Benchmark.TpcE.Compliance;
/// <summary>SUT が出す一貫性の点検の結果(起動時のログ行、または管理要求の応答)を、報告の点検項目にする。</summary>
internal static class ConsistencyReports{
    /// <summary>起動時のログ行 `[sut] consistency after-load 7.3.2.1 PASS checked=10 violations=0 ...` を読む。</summary>
    public static IReadOnlyList<CheckResult> FromStartupLines(IEnumerable<string> lines){
        var checks=new List<CheckResult>();
        foreach(var line in lines){
            var at=line.IndexOf("[sut] consistency ",StringComparison.Ordinal);
            if(at<0){
                continue;
            }
            var parts=line[(at+"[sut] consistency ".Length)..].Split(' ',5,StringSplitOptions.RemoveEmptyEntries);
            if(parts.Length<4||!parts[1].StartsWith("7.3.",StringComparison.Ordinal)){
                continue;
            }
            var passed=parts[2]=="PASS";
            var detail=string.Join(" ",parts.Skip(3));
            checks.Add(new CheckResult(parts[1],Name(parts[1],Describe(parts[0])),Outcome(passed),detail));
        }
        return checks;
    }
    /// <summary>管理要求 `{"admin":"consistency"}` の応答を読む。</summary>
    public static IReadOnlyList<CheckResult> FromAdminResponse(string json,string when){
        var checks=new List<CheckResult>();
        using var document=JsonDocument.Parse(json);
        foreach(var result in document.RootElement.GetProperty("results").EnumerateArray()){
            var clause=result.GetProperty("clause").GetString() ?? "";
            var passed=result.GetProperty("passed").GetBoolean();
            var detail=string.Create(CultureInfo.InvariantCulture,$"checked={result.GetProperty("checked").GetInt64()} violations={result.GetProperty("violations").GetInt64()} {result.GetProperty("detail").GetString()}");
            checks.Add(new CheckResult(clause,Name(clause,when),Outcome(passed),detail));
        }
        return checks;
    }
    /// <summary>SUT へ管理要求を 1 件送って応答を返す。</summary>
    public static string Ask(int port,string command){
        using var client=new TcpClient("127.0.0.1",port);
        var stream=client.GetStream();
        Framing.Write(stream,Encoding.UTF8.GetBytes($"{{\"admin\":\"{command}\"}}"));
        var body=Framing.Read(stream) ?? throw new IOException("The SUT closed the connection.");
        return Encoding.UTF8.GetString(body);
    }
    private static string Describe(string phase){
        switch(phase){
            case "after-load":
                return "初期投入後";
            case "after-recovery":
                return "復旧後";
            default:
                return phase;
        }
    }
    private static string Name(string clause,string when){
        var condition="";
        switch(clause){
            case "7.3.2.1":
                condition="条件1 ブローカーの取引件数";
                break;
            case "7.3.2.2":
                condition="条件2 ブローカーの手数料合計";
                break;
            case "7.3.2.3":
                condition="条件3 保有の数量の合計";
                break;
        }
        return $"一貫性 {condition}({when})";
    }
    private static CheckOutcome Outcome(bool passed){
        if(passed){
            return CheckOutcome.Pass;
        }
        return CheckOutcome.Fail;
    }
}
