using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Seamless.Execution;

/// <summary>通常アプリでも測定DBを参照せずに求められる、プロファイルの環境識別子。</summary>
public static class ProfileEnvironment{
    /// <summary>実行ごとのフォルダ名を除き、測定した保存先ボリュームを識別する。nullはメモリ。</summary>
    public static string Storage(string? directory){
        if(directory is null){return "Memory";}
        var path=Path.GetFullPath(directory);
        var comparison=StringComparison.Ordinal;
        if(OperatingSystem.IsWindows()){comparison=StringComparison.OrdinalIgnoreCase;}
        try{
            var drive=DriveInfo.GetDrives().Where(drive=>path.StartsWith(drive.Name.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar,comparison)
                ||string.Equals(Path.TrimEndingDirectorySeparator(path),Path.TrimEndingDirectorySeparator(drive.Name),comparison))
                .OrderByDescending(static drive=>drive.Name.Length).FirstOrDefault();
            if(drive is null){return "Unknown";}
            var root=drive.Name;
            if(OperatingSystem.IsWindows()){root=root.ToUpperInvariant();}
            return "volume-v1:"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",root,drive.DriveFormat,drive.VolumeLabel,drive.TotalSize))));
        }catch(IOException){return "Unknown";}
        catch(UnauthorizedAccessException){return "Unknown";}
    }
    public static string Current{get;}=Capture();
    public static string Identify(string os,string architecture,string runtime,string cpu)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",os,architecture,runtime,cpu))));
    private static string Capture(){
        var cpu=Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER")??RuntimeInformation.ProcessArchitecture.ToString();
        if(OperatingSystem.IsWindows()){
            using var key=Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            cpu=key?.GetValue("ProcessorNameString")?.ToString()?.Trim()??cpu;
        }
        return Identify(RuntimeInformation.OSDescription,RuntimeInformation.ProcessArchitecture.ToString(),RuntimeInformation.FrameworkDescription,cpu);
    }
}
