using System.Diagnostics;
namespace Benchmark.TpcE.Compliance.Tests;
/// <summary>EGen のビルド(build-egen.ps1 の出力)があるときだけ、EGenLoader で試験用のデータを作る。</summary>
internal static class EGenFixture{
    private const string DefaultBuild=@"G:\GitHub\Seamless\Work\TpcE\egen-build";
    private const string DefaultInputs=@"G:\GitHub\Seamless\Work\TpcE\egen\flat_in";
    internal static string BuildDirectory=>Environment.GetEnvironmentVariable("TPCE_EGEN_BUILD")??DefaultBuild;
    internal static string InputDirectory=>Environment.GetEnvironmentVariable("TPCE_EGEN_INPUTS")??DefaultInputs;
    /// <summary>実行した試験は、データ生成の前提が欠けていれば失敗させる。</summary>
    public static string Generate(int customers,int days){
        var build=BuildDirectory;
        var inputs=InputDirectory;
        var exe=Path.Combine(build,"EGenLoader.exe");
        if(!File.Exists(exe)||!Directory.Exists(inputs)){
            throw new InvalidOperationException("EGenLoader.exe と入力データが必要です。");
        }
        var output=Path.Combine(Path.GetTempPath(),"tpce-egen-test-"+Guid.NewGuid().ToString("N"))+Path.DirectorySeparatorChar;
        Directory.CreateDirectory(output);
        var start=new ProcessStartInfo(exe){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true};
        var count=customers.ToString(System.Globalization.CultureInfo.InvariantCulture);
        foreach(var argument in new[]{"-b","1","-c",count,"-t",count,"-f","500","-w",days.ToString(System.Globalization.CultureInfo.InvariantCulture),"-i",inputs+Path.DirectorySeparatorChar,"-o",output,"-x"}){
            start.ArgumentList.Add(argument);
        }
        using var process=Process.Start(start)!;
        process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        if(process.ExitCode!=0){
            throw new InvalidOperationException($"EGenLoader failed with exit code {process.ExitCode}.");
        }
        return output;
    }
    public static void Delete(string directory){
        try{
            Directory.Delete(directory,true);
        }catch(IOException){
        }
    }
}
