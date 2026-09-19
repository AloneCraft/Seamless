using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using System.Runtime.InteropServices;
using Verification.Core;
namespace Verification.Infrastructure;

public static class RepositoryInspection{
    public static string[] Files(string root)=>Git(root,"ls-files","--cached","--others","--exclude-standard","-z").Split('\0',StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    public static string Fingerprint(string root){
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach(var path in Files(root).Where(IsSource).Order(StringComparer.Ordinal)){
            hash.AppendData(Encoding.UTF8.GetBytes(path+"\0"));
            var full=Path.Combine(root,path);
            if(File.Exists(full)){
                using var stream=File.OpenRead(full);
                hash.AppendData(SHA256.HashData(stream));
            }else{hash.AppendData([0]);}
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    public static VerificationPlan Plan(string root,string profile,bool changedOnly=true,bool includeRepresentativeBenchmarks=true){
        root=Path.GetFullPath(root);
        if(profile is not ("quick" or "full")){throw new ArgumentException("profile は quick または full です。",nameof(profile));}
        var files=Files(root);
        var projects=files.Where(static x=>x.EndsWith(".csproj",StringComparison.OrdinalIgnoreCase)).Where(x=>File.Exists(Path.Combine(root,x))).ToArray();
        var changed=Git(root,"diff","--name-only","HEAD","-z").Split('\0',StringSplitOptions.RemoveEmptyEntries)
            .Concat(Git(root,"ls-files","--others","--exclude-standard","-z").Split('\0',StringSplitOptions.RemoveEmptyEntries)).ToArray();
        var graph=new Dictionary<string,HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach(var project in projects){
            var full=Path.GetFullPath(Path.Combine(root,project));
            var dependencies=new HashSet<string>(StringComparer.OrdinalIgnoreCase){Path.GetDirectoryName(full)!+Path.DirectorySeparatorChar};
            ReadDependencies(full,dependencies,new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            graph[project]=dependencies;
        }
        bool Matches(string project,string path){
            var full=Path.GetFullPath(Path.Combine(root,path));
            return graph[project].Any(p=>full.Equals(p,StringComparison.OrdinalIgnoreCase)||(p.EndsWith(Path.DirectorySeparatorChar)&&full.StartsWith(p,StringComparison.OrdinalIgnoreCase)));
        }
        var broad=!changedOnly||changed.Length==0||changed.Any(path=>!projects.Any(project=>Matches(project,path)));
        var selected=new HashSet<string>(projects.Where(p=>broad||changed.Any(c=>Matches(p,c))),StringComparer.OrdinalIgnoreCase);
        bool added;
        do{
            added=false;
            foreach(var project in projects){
                if(selected.Contains(project)){continue;}
                if(selected.Any(p=>graph[project].Contains(Path.GetFullPath(Path.Combine(root,p))))){selected.Add(project);added=true;}
            }
        }while(added);
        var conditions=new ComparisonConditions("tests-v1","repository-fixtures","dotnet-v1","none","test-outcome-v1",
            Environment.MachineName+"|"+RuntimeInformation.OSDescription+"|"+RuntimeInformation.FrameworkDescription,
            (Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER")??RuntimeInformation.ProcessArchitecture.ToString())+"|"+Environment.ProcessorCount,"Release");
        var cases=new List<VerificationCase>();
        foreach(var project in projects.Order(StringComparer.Ordinal)){
            var name=Path.GetFileNameWithoutExtension(project);
            var document=XDocument.Load(Path.Combine(root,project));
            var isTest=document.Descendants().Any(x=>x.Name.LocalName=="IsTestProject"&&x.Value=="true")
                ||name=="Test"||name.StartsWith("Test.",StringComparison.Ordinal)||name.EndsWith(".Tests",StringComparison.Ordinal)||name.EndsWith(".UITests",StringComparison.Ordinal);
            if(!isTest||!selected.Contains(project)){continue;}
            if(profile=="quick"&&(name.Contains("UITests",StringComparison.Ordinal)||name.Contains("Scalable",StringComparison.Ordinal)||name.Contains("ParameterSweep",StringComparison.Ordinal))){continue;}
            var tfm=document.Descendants().FirstOrDefault(static x=>x.Name.LocalName=="TargetFramework")?.Value;
            if(string.IsNullOrEmpty(tfm)||tfm.Contains('$')){tfm="net10.0";}
            var prepare=new List<string>{"build",project,"-c","Release","-f",tfm,"--nologo"};
            var arguments=new List<string>{"test",project,"-c","Release","-f",tfm,"--no-build","--nologo","--logger","trx;LogFileName=results.trx","--results-directory","{artifacts}"};
            if(profile=="quick"){
                arguments.AddRange(["--filter","Category!=Stress&Category!=Corpus&Category!=Performance&Category!=UI"]);
                if(name=="Benchmark.Dynamic.Tests"){prepare.Add("-p:CoreOnly=true");arguments.Add("-p:CoreOnly=true");}
            }
            var reason="変更されたプロジェクトまたはその依存先を検証します。";
            if(broad){reason="影響範囲を限定できないため共通スイートを選択しました。";}
            cases.Add(new(project,name,Area(name),"dotnet-test",project,prepare.ToArray(),arguments.ToArray(),conditions with{CaseVersion=project+"|"+profile,InputHash=FixtureFingerprint(root,project,graph[project],files,arguments)},reason));
        }
        if(includeRepresentativeBenchmarks){
            cases.AddRange(VerificationProfiles.RepresentativeBenchmarks(conditions,profile));
            cases.AddRange(VerificationProfiles.SweepCases(conditions,profile,representativeOnly:true));
            cases.AddRange(VerificationProfiles.ScalableCases(conditions,profile,representativeOnly:true));
        }
        var budget=0;
        if(profile=="quick"){budget=300;}
        return new(1,root,Fingerprint(root),profile,budget,cases.ToArray());
    }
    private static string FixtureFingerprint(string root,string project,HashSet<string> dependencies,string[] files,List<string> arguments){
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(string.Join("\0",arguments)));
        var directory=Path.GetDirectoryName(Path.GetFullPath(Path.Combine(root,project)))!+Path.DirectorySeparatorChar;
        foreach(var file in files.Order(StringComparer.Ordinal)){
            var full=Path.GetFullPath(Path.Combine(root,file));
            if(!File.Exists(full)||!IsSource(file)){continue;}
            if(!full.StartsWith(directory,StringComparison.OrdinalIgnoreCase)&&!dependencies.Any(d=>d.EndsWith(Path.DirectorySeparatorChar)&&full.StartsWith(d,StringComparison.OrdinalIgnoreCase)||d.Equals(full,StringComparison.OrdinalIgnoreCase)&&!d.EndsWith(".csproj",StringComparison.OrdinalIgnoreCase))){continue;}
            hash.AppendData(Encoding.UTF8.GetBytes(file+"\0"));
            using var input=File.OpenRead(full);
            hash.AppendData(SHA256.HashData(input));
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    private static string Area(string name){
        if(name.Contains("Dynamic",StringComparison.Ordinal)){return "クエリの正しさ検証";}
        if(name.Contains("Scalable",StringComparison.Ordinal)||name.Contains("Sweep",StringComparison.Ordinal)){return "容量・設定探索";}
        if(name.Contains("Database",StringComparison.Ordinal)){return "DB";}
        return "変更後の確認";
    }
    private static bool IsSource(string path)=>Path.GetExtension(path).ToLowerInvariant() is ".cs" or ".csproj" or ".projitems" or ".shproj" or ".props" or ".targets" or ".slnx" or ".json" or ".xaml";
    private static void ReadDependencies(string path,HashSet<string> dependencies,HashSet<string> seen){
        if(!seen.Add(path)||!File.Exists(path)){return;}
        foreach(var element in XDocument.Load(path).Descendants()){
            string? reference=null;
            if(element.Name.LocalName=="ProjectReference"){reference=element.Attribute("Include")?.Value;}
            if(element.Name.LocalName=="Import"){reference=element.Attribute("Project")?.Value;}
            if(element.Name.LocalName=="Compile"){reference=element.Attribute("Include")?.Value;}
            if(string.IsNullOrEmpty(reference)){continue;}
            reference=reference.Replace("$(MSBuildThisFileDirectory)",Path.GetDirectoryName(path)!+Path.DirectorySeparatorChar,StringComparison.Ordinal);
            if(reference.Contains('$')){continue;}
            var wildcard=reference.IndexOfAny(['*','?']);
            if(wildcard>=0){
                var prefix=reference[..wildcard];
                var folder=Path.GetDirectoryName(prefix.TrimEnd('\\','/'))??Path.GetDirectoryName(path)!;
                if(prefix.EndsWith('\\')||prefix.EndsWith('/')){folder=prefix;}
                dependencies.Add(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!,folder))+Path.DirectorySeparatorChar);
                continue;
            }
            var full=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!,reference.Replace('\\',Path.DirectorySeparatorChar)));
            dependencies.Add(full);
            if(full.EndsWith(".projitems",StringComparison.OrdinalIgnoreCase)){ReadDependencies(full,dependencies,seen);}
        }
    }
    private static string Git(string root,params string[] arguments){
        using var process=new Process{StartInfo=new("git"){WorkingDirectory=root,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true}};
        foreach(var argument in arguments){process.StartInfo.ArgumentList.Add(argument);}
        process.Start();
        var errorTask=process.StandardError.ReadToEndAsync();
        var output=process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        var error=errorTask.GetAwaiter().GetResult();
        if(process.ExitCode!=0){throw new InvalidOperationException("Gitの情報を取得できません: "+error);}
        return output;
    }
}
