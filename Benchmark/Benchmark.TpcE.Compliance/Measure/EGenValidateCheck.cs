using System.Diagnostics;
using System.Globalization;
namespace Benchmark.TpcE.Compliance;
/// <summary>
/// 仕様 6.7.4: 計測区間で、負荷単位(顧客 1,000 人)ごとの Trade-Result の件数がほぼそろっていること。
/// 負荷単位ごとの件数の標準偏差を求め、TPC が配る EGenValidate に渡して「Passed!」を得る。
/// </summary>
internal static class EGenValidateCheck{
    private const string Clause="6.7.4";
    private const string Name="負荷単位ごとの Trade-Result の件数(EGenValidate)";
    /// <summary>標準偏差 = sqrt(Σ(平均 − n_i)² / (負荷単位の数 − 1))。小数第 3 位まで、切り上げ(仕様 6.7.4.2)。</summary>
    public static double StandardDeviation(IReadOnlyList<long> counts){
        if(counts.Count<2){
            return 0;
        }
        var mean=counts.Average();
        var sum=0.0;
        foreach(var count in counts){
            sum+=(mean-count)*(mean-count);
        }
        var deviation=Math.Sqrt(sum/(counts.Count-1));
        return Math.Ceiling(deviation*1000-1e-9)/1000;
    }
    public static CheckResult Run(DriverResult result,string egenBuild,TimeSpan timeout){
        var counts=result.LoadUnitTradeResults;
        if(counts.Count<2){
            return new CheckResult(Clause,Name,CheckOutcome.NotExercised,$"負荷単位が {counts.Count} 個しかなく、標準偏差を求められない(顧客 2,000 人以上で検定できる)");
        }
        var deviation=StandardDeviation(counts);
        var exe=Path.Combine(egenBuild,"EGenValidate.exe");
        var summary=$"負荷単位 {counts.Count} 個、1 つあたり平均 {counts.Average():F1} 件、標準偏差 {deviation.ToString("F3",CultureInfo.InvariantCulture)}";
        if(!File.Exists(exe)){
            return new CheckResult(Clause,Name,CheckOutcome.NotExercised,summary+"。EGenValidate.exe が無い(build-egen.ps1 を実行する)");
        }
        var start=new ProcessStartInfo(exe){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true};
        foreach(var argument in new[]{"-s",deviation.ToString("F3",CultureInfo.InvariantCulture),"-c",result.Customers.ToString(CultureInfo.InvariantCulture),"-e",result.TpsE.ToString("F3",CultureInfo.InvariantCulture),"-r",Math.Max(1,(long)Math.Round(result.MeasureSeconds)).ToString(CultureInfo.InvariantCulture),"-t","2"}){
            start.ArgumentList.Add(argument);
        }
        using var process=Process.Start(start)!;
        var output=process.StandardOutput.ReadToEndAsync();
        process.StandardError.ReadToEndAsync();
        if(!process.WaitForExit(timeout)){
            process.Kill(true);
            return new CheckResult(Clause,Name,CheckOutcome.Fail,summary+$"。EGenValidate が {timeout.TotalMinutes:F0} 分で終わらなかった");
        }
        var text=output.GetAwaiter().GetResult();
        if(text.Contains("Passed!",StringComparison.Ordinal)){
            return new CheckResult(Clause,Name,CheckOutcome.Pass,summary+"。EGenValidate: Passed!");
        }
        return new CheckResult(Clause,Name,CheckOutcome.Fail,summary+"。EGenValidate: "+text.Trim().ReplaceLineEndings(" "));
    }
}
