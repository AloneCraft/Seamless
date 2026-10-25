using System.Security.Cryptography;

namespace Benchmark.Infrastructure;

/// <summary>日時だけで代用せず、測定に使う配布DLLの内容を識別する。</summary>
public static class BuildFingerprint{
    public static string Capture(string entryAssemblyPath){
        var directory=Path.GetDirectoryName(Path.GetFullPath(entryAssemblyPath))!;
        var files=Directory.EnumerateFiles(directory,"*.dll")
            .Where(path=>{
                var name=Path.GetFileName(path);
                return name.StartsWith("Seamless.",StringComparison.Ordinal)||name.StartsWith("Benchmark.",StringComparison.Ordinal)
                    ||name.StartsWith("Tsavorite.",StringComparison.Ordinal)||name=="Seamless.dll"||name=="ZLinq.dll";
            }).Append(entryAssemblyPath).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(Path.GetFileName,StringComparer.Ordinal);
        return Fingerprint.Of(string.Join("\n",files.Select(path=>Path.GetFileName(path)+":"+Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))))));
    }
}
