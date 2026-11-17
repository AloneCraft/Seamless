using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Seamless.Management;

public sealed partial class SourceWorkspace{
    private sealed record PackageFeedManifest(int Format,string? Version,Dictionary<string,string>? Assemblies);

    private static string ResolvePackageReferenceVersion(string feed){
        var path=Path.Combine(feed,"source-packages.json");
        if(!File.Exists(path)){return "0.0.0";}
        try{
            using var stream=File.OpenRead(path);
            if(stream.Length is <1 or >65536){throw InvalidPackageManifest();}
            var bytes=new byte[(int)stream.Length];stream.ReadExactly(bytes);
            if(stream.ReadByte()!=-1){throw InvalidPackageManifest();}
            var manifest=JsonSerializer.Deserialize<PackageFeedManifest>(bytes,ManagementJson.Options);
            if(manifest is not{Format:1,Version:{Length:>0 and <=96} version,Assemblies:{Count:>0 and <=128} assemblies}){throw InvalidPackageManifest();}
            if(!Regex.IsMatch(version,@"\A[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9]+(?:[.-][A-Za-z0-9]+)*)?\z",RegexOptions.CultureInvariant|RegexOptions.NonBacktracking)){
                throw InvalidPackageManifest();
            }
            if(!assemblies.ContainsKey("Database.dll")||!assemblies.ContainsKey("Runtime.dll")){throw InvalidPackageManifest();}
            foreach(var (name,expected) in assemblies){
                var knownProductFile=name is "Runtime.dll" or "Database.dll" or "Client.dll" or "Server.dll" or "en/Server.resources.dll" or "Sample.NetShop.Core.dll" or "Tsavorite.core.dll" or "Garnet.common.dll" or "Tsavorite.devices.AzureStorageDevice.dll";
                if((!knownProductFile&&!Regex.IsMatch(name,@"\ASeamless(?:\.[A-Za-z0-9]+)*\.dll\z",RegexOptions.CultureInvariant|RegexOptions.NonBacktracking))||expected is not{Length:64}||!expected.All(char.IsAsciiHexDigit)){
                    throw InvalidPackageManifest();
                }
                var runtimePath=Path.Combine(Path.GetDirectoryName(global::Seamless.Execution.CompilationReferences.PathFor(typeof(SourceWorkspace).Assembly))!,name);
                if(!File.Exists(runtimePath)){
                    var assembly=global::System.Reflection.Assembly.Load(Path.GetFileNameWithoutExtension(name));
                    runtimePath=global::Seamless.Execution.CompilationReferences.PathFor(assembly);
                }
                using var runtime=File.OpenRead(runtimePath);
                var actual=Convert.ToHexString(SHA256.HashData(runtime));
                if(!string.Equals(expected,actual,StringComparison.OrdinalIgnoreCase)){
                    throw new ManagementException("PackageFeed","パッケージ一式の"+name+"がサーバーと異なります。同じ配布物のpackagesを指定してください。");
                }
            }
            return "["+version+"]";
        }catch(JsonException){throw InvalidPackageManifest();}
        catch(IOException error){throw new ManagementException("PackageFeed","パッケージ一式のマニフェストを読み取れません。"+error.Message);}
    }

    private static ManagementException InvalidPackageManifest()=>new("PackageFeed","パッケージ一式のマニフェストが不正です。同じ配布物のpackagesを指定してください。");
}
