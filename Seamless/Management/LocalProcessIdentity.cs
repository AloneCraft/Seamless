using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Seamless.Management;

internal static class LocalProcessIdentity{
    internal static string? Capture(int pid,DateTime creationUtc){
        if(!OperatingSystem.IsLinux()){return null;}
        var boot=File.ReadAllText("/proc/sys/kernel/random/boot_id").Trim();
        var stat=File.ReadAllText("/proc/"+pid.ToString(CultureInfo.InvariantCulture)+"/stat");
        var end=stat.LastIndexOf(')');
        if(end<0){throw new InvalidDataException("Invalid Linux process identity.");}
        var fields=stat[(end+1)..].Split(' ',StringSplitOptions.RemoveEmptyEntries);
        if(!Guid.TryParse(boot,out var bootId)||fields.Length<=19||!ulong.TryParse(fields[19],NumberStyles.None,CultureInfo.InvariantCulture,out var start)){
            throw new InvalidDataException("Invalid Linux process identity.");
        }
        // カーネルの起動IDと相対開始時刻は、WSLの時計補正や別プロセスのUTC換算差に依存しない。
        var identity=bootId.ToString("N")+":"+start.ToString(CultureInfo.InvariantCulture)+":"+creationUtc.Ticks.ToString(CultureInfo.InvariantCulture);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }
    internal static bool Matches(int pid,DateTime creationUtc,string? identity){
        try{
            using var process=Process.GetProcessById(pid);
            if(process.HasExited){return false;}
            if(OperatingSystem.IsLinux()&&identity is not null){return string.Equals(identity,Capture(pid,creationUtc),StringComparison.Ordinal);}
            return process.StartTime.ToUniversalTime()==creationUtc;
        }catch(ArgumentException){return false;}catch(InvalidOperationException){return false;}
        catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}
    }
}
