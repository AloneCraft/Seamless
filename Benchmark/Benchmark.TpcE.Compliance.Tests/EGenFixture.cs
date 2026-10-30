using System.Diagnostics;
namespace Benchmark.TpcE.Compliance.Tests;
/// <summary>EGen のビルド(build-egen.ps1 の出力)があるときだけ、EGenLoader で試験用のデータを作る。</summary>
internal static class EGenFixture{
    private const string DefaultBuild=@"G:\GitHub\Seamless\Work\TpcE\egen-build";
    private const string DefaultInputs=@"G:\GitHub\Seamless\Work\TpcE\egen\flat_in";
    /// <summary>データを作ったフォルダ。EGen のビルドが無ければ null(試験は何もせず抜ける。CI には入れない前提)。</summary>
    public static string? Generate(int customers,int days){
        var build=Environment.GetEnvironmentVariable("TPCE_EGEN_BUILD") ?? DefaultBuild;
        var inputs=Environment.GetEnvironmentVariable("TPCE_EGEN_INPUTS") ?? DefaultInputs;
        var exe=Path.Combine(build,"EGenLoader.exe");
        if(!File.Exists(exe)||!Directory.Exists(inputs)){
            return null;
        }
        var output=Path.Combine(Path.GetTempPath(),"tpce-egen-test-"+Guid.NewGuid().ToString("N"))+Path.DirectorySeparatorChar;
        Directory.CreateDirectory(output);
        var start=new ProcessStartInfo(exe){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
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
