using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Seamless.Database;

namespace Seamless.Management.Server;

/// <summary>
/// DB 定義の下書きを dotnet SDK でビルドし、ソースプロジェクトの成果物と同じ検査・保管を通して読み込む。
/// 11/12 の決定：利用者の DB 定義には内蔵のコンパイル(RuntimeCompiler)を使わず、VS・VS Code と同じ SDK のビルドで作る。
/// 同じ定義(ハッシュ)のビルドは作業用のフォルダーに残し、2回目からはビルドせずに検査だけを行う。
/// 設計：Document/refactoring-designs/20261115-Managerの設計と実装の照合.md の段階7。
/// </summary>
internal sealed partial class DraftSdkBuilder(string root,SourceBuildArtifactStore artifacts){
    private static readonly string[] AnalyzerAssemblies=["Seamless.Database.Generator","Seamless.FLinq.Generator"];
    private readonly Lock gate=new();

    internal CompiledSource Build(Definition definition,CancellationToken cancellationToken=default){
        if(definition.Kind!=DefinitionKind.Database){throw new InvalidOperationException("SDK のビルドは DB 定義だけに使います。");}
        SourceEditor.Validate(definition.Source);
        lock(this.gate){
            var directory=Path.Combine(root,definition.Hash()[..32]);
            var name="Seamless.Managed_"+definition.Source.Hash()[..24];
            var project=Path.Combine(directory,name+".csproj");
            var assembly=Path.Combine(directory,"bin",name+".dll");
            Directory.CreateDirectory(directory);SourceWorkspace.CheckLinks(directory);
            var files=new List<SourceProjectFile>();
            foreach(var file in definition.Source.Files){
                var path=Path.GetFullPath(Path.Combine(directory,file.Path));
                if(!path.StartsWith(Path.GetFullPath(directory)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)){throw new ManagementException("InvalidRequest","ソースの場所が作業用のフォルダーを外れています: "+file.Path);}
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                WriteIfChanged(path,file.Text);
                files.Add(new(file.Path,path));
            }
            WriteIfChanged(project,Project(definition,name));
            if(!File.Exists(assembly)){
                var failure=Run(project,cancellationToken);
                if(failure is not null){return new([],new(definition.Source.Hash(),"",Diagnostics(failure,directory,definition),[]));}
            }
            var context=new SourceBuildInspectionContext("server-draft",new(definition,0,0),new(project,files.ToArray()));
            SourceProjectBuild? inspected=null;
            SourceBuildArtifactInspector.Inspect(context,assembly,cancellationToken,(build,captured)=>{artifacts.Store(build,captured,cancellationToken);inspected=build;});
            return NativeRuntime.FromImported(artifacts.Read(inspected!,cancellationToken));
        }
    }

    // 内蔵のコンパイルと同じ参照(Seamless.Database と Seamless.Linq と、それらが参照する部品)をサーバーの部品から直接参照し、出力へ写す(共有の部品のバイトがサーバーと一致する)。
    private static string Project(Definition definition,string name){
        var references=new ItemsFor().Closure([typeof(DatabaseContainer).Assembly,typeof(global::Seamless.Linq.Enumerable).Assembly]);
        var analyzers=AnalyzerAssemblies.Select(item=>Path.Combine(AppContext.BaseDirectory,item+".dll")).Where(File.Exists).ToArray();
        var project=new XElement("Project",new XAttribute("Sdk","Microsoft.NET.Sdk"),
            new XElement("PropertyGroup",
                new XElement("TargetFramework","net10.0"),new XElement("LangVersion","14"),new XElement("Nullable","enable"),new XElement("AllowUnsafeBlocks","true"),
                new XElement("AssemblyName",name),new XElement("RootNamespace",name),new XElement("OutDir","$(MSBuildProjectDirectory)\\bin\\"),
                new XElement("EnableDefaultCompileItems","false"),new XElement("DebugType","portable"),new XElement("Deterministic","true"),
                new XElement("EnableNETAnalyzers","false"),new XElement("NuGetAudit","false"),new XElement("GenerateDocumentationFile","false"),
                new XElement("InterceptorsNamespaces","$(InterceptorsNamespaces);Intercept"),new XElement("SeamlessManagedDatabase","true"),
                new XElement("DefineConstants",string.Join(";",definition.Source.Defines??[]))),
            new XElement("ItemGroup",
                definition.Source.Files.Select(file=>new XElement("Compile",new XAttribute("Include",file.Path))),
                references.Select(path=>new XElement("Reference",new XAttribute("Include",Path.GetFileNameWithoutExtension(path)),new XElement("HintPath",path),new XElement("Private","true"))),
                analyzers.Select(path=>new XElement("Analyzer",new XAttribute("Include",path))),
                new XElement("CompilerVisibleProperty",new XAttribute("Include","SeamlessManagedDatabase"))),
            // SDK が自動で足す条件定義(TRACE・RELEASE・NET など)を外し、下書きの条件定義だけにする(内蔵のコンパイルと同じ。検査は PDB の定義と下書きの一致を求める)。
            new XElement("Target",new XAttribute("Name","SeamlessDraftDefines"),new XAttribute("BeforeTargets","CoreCompile"),
                new XElement("PropertyGroup",new XElement("DefineConstants",string.Join(";",definition.Source.Defines??[])))));
        return project.ToString();
    }

    private sealed class ItemsFor{
        private readonly HashSet<string> seen=new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> paths=[];
        private static readonly string Shared=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!,"..",".."))+Path.DirectorySeparatorChar;
        internal string[] Closure(IEnumerable<Assembly> roots){foreach(var assembly in roots){this.Add(assembly);}return this.paths.ToArray();}
        private void Add(Assembly assembly){
            if(assembly.IsDynamic||string.IsNullOrEmpty(assembly.Location)||assembly.Location.StartsWith(Shared,StringComparison.OrdinalIgnoreCase)||!this.seen.Add(assembly.GetName().Name!)){return;}
            this.paths.Add(assembly.Location);
            foreach(var reference in assembly.GetReferencedAssemblies()){
                Assembly loaded;try{loaded=Assembly.Load(reference);}catch(FileNotFoundException){continue;}
                this.Add(loaded);
            }
        }
    }

    private static void WriteIfChanged(string path,string text){
        if(File.Exists(path)&&File.ReadAllText(path,Encoding.UTF8)==text){return;}
        File.WriteAllText(path,text,new UTF8Encoding(false));
    }

    // 失敗したらビルドの出力を返す。成功なら null。
    private static string? Run(string project,CancellationToken cancellationToken){
        var executable=Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"dotnet","dotnet.exe");
        using var process=new Process{StartInfo=new(executable){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.GetDirectoryName(project)!,RedirectStandardOutput=true,RedirectStandardError=true}};
        foreach(var argument in new[]{"build",project,"-nologo","-v:minimal","-m:1","-nodeReuse:false","-p:UseSharedCompilation=false","-p:Configuration=Release"}){process.StartInfo.ArgumentList.Add(argument);}
        if(!process.Start()){throw new ManagementException("Unavailable","SDKを起動できません。");}
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);deadline.CancelAfter(TimeSpan.FromMinutes(10));
        try{
            var output=process.StandardOutput.ReadToEndAsync(deadline.Token);var error=process.StandardError.ReadToEndAsync(deadline.Token);
            process.WaitForExitAsync(deadline.Token).GetAwaiter().GetResult();
            if(process.ExitCode==0){return null;}
            return output.GetAwaiter().GetResult()+error.GetAwaiter().GetResult();
        }finally{
            if(!process.HasExited){process.Kill(entireProcessTree:true);process.WaitForExit();}
        }
    }

    [GeneratedRegex(@"^\s*(?<path>.+?)\((?<line>\d+),(?<column>\d+)\): (?<severity>error|warning) (?<code>[A-Za-z0-9]+): (?<message>.*?)(?: \[[^\]]+\])?\s*$")]
    private static partial Regex DiagnosticLine();

    // MSBuild の出力の「ファイル(行,列): error 番号: 文」を、ソースの位置付きの診断に直す。読めなければ出力の終わりを1件の診断にする。
    private static DiagnosticInfo[] Diagnostics(string log,string directory,Definition definition){
        var result=new List<DiagnosticInfo>();var seen=new HashSet<string>(StringComparer.Ordinal);
        foreach(var line in log.Split('\n')){
            var match=DiagnosticLine().Match(line);if(!match.Success||!seen.Add(line.Trim())){continue;}
            var full=Path.GetFullPath(match.Groups["path"].Value);
            var relative=Path.GetRelativePath(directory,full).Replace('\\','/');
            var file=definition.Source.Files.FirstOrDefault(item=>item.Path.Replace('\\','/')==relative);
            var start=0;
            if(file is not null){start=Offset(file.Text,int.Parse(match.Groups["line"].Value,System.Globalization.CultureInfo.InvariantCulture),int.Parse(match.Groups["column"].Value,System.Globalization.CultureInfo.InvariantCulture));}
            var severity=match.Groups["severity"].Value=="error"?"Error":"Warning";
            result.Add(new(match.Groups["code"].Value,match.Groups["message"].Value,file?.Path,start,0,severity));
        }
        if(!result.Any(item=>item.Severity=="Error")){
            var tail=log.Length>4000?"…"+log[^4000..]:log;
            result.Add(new("BuildFailed","SDKのビルドに失敗しました。\n"+tail));
        }
        return result.ToArray();
    }
    private static int Offset(string text,int line,int column){
        var offset=0;
        for(var current=1;current<line&&offset<text.Length;current++){var next=text.IndexOf('\n',offset);if(next<0){return text.Length;}offset=next+1;}
        return Math.Min(text.Length,offset+Math.Max(0,column-1));
    }
}
