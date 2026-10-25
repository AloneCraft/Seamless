using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Seamless.Execution;

/// <summary>通常アプリでも測定DBを参照せずに求められる、プロファイルの環境識別子。</summary>
public static class ProfileEnvironment{
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
