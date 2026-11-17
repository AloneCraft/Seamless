using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace Seamless.Execution;

/// <summary>動的コンパイルに必要な参照を、通常配置と単一ファイル配置から取得する。</summary>
public static class CompilationReferences{
    private const string Prefix="Seamless.CompilationReference.";
    private const string FrameworkPrefix="Seamless.FrameworkReference.";
    private static readonly Lazy<IReadOnlyDictionary<string,string>> bundled=new(Extract);
    private static readonly Lazy<IReadOnlySet<string>> frameworkRuntimeAssemblies=new(()=>ReadFrameworkRuntimeAssemblies(Path.Combine(AppContext.BaseDirectory,Assembly.GetEntryAssembly()?.GetName().Name+".deps.json")));

    public static IEnumerable<string> Paths{
        get{
            var platform=((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?.Split(Path.PathSeparator)??[];
            return platform.Where(File.Exists).Concat(bundled.Value.Values).GroupBy(Path.GetFileName,StringComparer.OrdinalIgnoreCase).Select(group=>group.First());
        }
    }

    public static string PathFor(Assembly assembly){
        ArgumentNullException.ThrowIfNull(assembly);
#pragma warning disable IL3000 // 単一ファイルでは埋込PEを取り出す経路へ進む。
        var location=assembly.Location;
#pragma warning restore IL3000
        if(location.Length>0){return location;}
        var name=assembly.GetName().Name+".dll";
        if(bundled.Value.TryGetValue(name,out var path)){return path;}
        throw new FileNotFoundException("単一EXEの動的コンパイル用参照がありません。Seamlessのsingle-file発行ターゲットを適用してください。",name);
    }

    public static bool IsFramework(Assembly assembly){
        if(assembly==typeof(object).Assembly){return true;}
        var name=assembly.GetName().Name+".dll";
        var entry=Assembly.GetEntryAssembly();
        if(frameworkRuntimeAssemblies.Value.Contains(name)){return true;}
        if(entry?.GetManifestResourceInfo(FrameworkPrefix+name) is not null){return true;}
        if(entry?.GetManifestResourceNames().Any(resource=>resource.StartsWith(FrameworkPrefix,StringComparison.Ordinal))==true){return false;}
#pragma warning disable IL3000 // 通常配置のshared frameworkの境界を判定する。
        var core=typeof(object).Assembly.Location;
        var location=assembly.Location;
#pragma warning restore IL3000
        if(core.Length==0||location.Length==0){return false;}
        var shared=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(core)!,"..",".."))+Path.DirectorySeparatorChar;
        return location.StartsWith(shared,StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlySet<string> ReadFrameworkRuntimeAssemblies(string path){
        var result=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if(!File.Exists(path)){return result;}
        using var stream=File.OpenRead(path);
        using var document=JsonDocument.Parse(stream);
        var root=document.RootElement;
        if(!root.TryGetProperty("runtimeTarget",out var runtimeTarget)||!runtimeTarget.TryGetProperty("name",out var name)||
            !root.TryGetProperty("targets",out var targets)||!targets.TryGetProperty(name.GetString()!,out var target)||
            !root.TryGetProperty("libraries",out var libraries)){return result;}
        foreach(var library in libraries.EnumerateObject()){
            if(!library.Value.TryGetProperty("type",out var type)||type.GetString()!="runtimepack"||
                !target.TryGetProperty(library.Name,out var assets)||!assets.TryGetProperty("runtime",out var runtime)){continue;}
            foreach(var asset in runtime.EnumerateObject()){
                if(asset.Name.EndsWith(".dll",StringComparison.OrdinalIgnoreCase)){result.Add(Path.GetFileName(asset.Name));}
            }
        }
        return result;
    }

    private static IReadOnlyDictionary<string,string> Extract(){
        var entry=Assembly.GetEntryAssembly()??throw new InvalidOperationException("Entry assembly is required.");
        var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        var platform=((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?.Split(Path.PathSeparator)??[];
        var existingPaths=platform.Where(File.Exists).GroupBy(Path.GetFileName,StringComparer.OrdinalIgnoreCase).ToDictionary(group=>group.Key!,group=>group.First(),StringComparer.OrdinalIgnoreCase);
        foreach(var resource in entry.GetManifestResourceNames().Where(name=>name.StartsWith(Prefix,StringComparison.Ordinal)||name.StartsWith(FrameworkPrefix,StringComparison.Ordinal)).Order(StringComparer.Ordinal)){
            var prefix=Prefix;
            if(resource.StartsWith(FrameworkPrefix,StringComparison.Ordinal)){prefix=FrameworkPrefix;}
            var name=resource[prefix.Length..];
            if(Path.GetFileName(name)!=name){throw new InvalidDataException("Invalid embedded compilation reference name.");}
            // Copy-localの実装PEを優先し、同名のframework参照PEを二重登録しない。
            if(prefix==FrameworkPrefix&&result.ContainsKey(name)){continue;}
            if(existingPaths.TryGetValue(name,out var existingPath)){result.Add(name,existingPath);continue;}
            using var input=entry.GetManifestResourceStream(resource)??throw new InvalidDataException("Missing embedded compilation reference.");
            using var bytes=new MemoryStream();
            input.CopyTo(bytes);
            var image=bytes.ToArray();
            var hash=Convert.ToHexString(SHA256.HashData(image));
            var directory=Path.Combine(Path.GetTempPath(),"Seamless","CompilationReferences",hash);
            Directory.CreateDirectory(directory);
            var path=Path.Combine(directory,name);
            if(!File.Exists(path)){
                var temporary=Path.Combine(directory,Guid.NewGuid().ToString("N")+".tmp");
                try{
                    File.WriteAllBytes(temporary,image);
                    try{File.Move(temporary,path,false);}
                    catch(IOException) when(File.Exists(path)){}
                }finally{if(File.Exists(temporary)){File.Delete(temporary);}}
            }
            using var existing=File.OpenRead(path);
            if(Convert.ToHexString(SHA256.HashData(existing))!=hash){throw new InvalidDataException("Embedded compilation reference fingerprint mismatch.");}
            result.Add(name,path);
        }
        return result;
    }
}
