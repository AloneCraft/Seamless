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
    // 参照の根：DB の部品・Seamless.Linq と、取り込んだ DB が使う SQL Server の組み込み関数と bit の型(使わない DB の成果物には入らない)。
    private static readonly Assembly[] ReferenceRoots=[typeof(DatabaseContainer).Assembly,typeof(global::Seamless.Linq.Enumerable).Assembly,typeof(global::Seamless.Database.SqlServer.Types.Bit).Assembly];
    private readonly Lock gate=new();

    internal CompiledSource Build(Definition definition,CancellationToken cancellationToken=default){
        if(definition.Kind!=DefinitionKind.Database){throw new InvalidOperationException("SDK のビルドは DB 定義だけに使います。");}
        SourceEditor.Validate(definition.Source);
        lock(this.gate){
            var draft=this.Prepare(definition.Source);
            if(!File.Exists(draft.Assembly)){
                var failure=Run(draft.Project,cancellationToken);
                if(failure is not null){return new([],new(definition.Source.Hash(),"",Diagnostics(failure,draft.Directory,definition.Source),[]));}
            }
            var context=new SourceBuildInspectionContext("server-draft",new(definition,0,0),new(draft.Project,draft.Files));
            SourceProjectBuild? inspected=null;
            SourceBuildArtifactInspector.Inspect(context,draft.Assembly,cancellationToken,(build,captured)=>{artifacts.Store(build,captured,cancellationToken);inspected=build;});
            return NativeRuntime.FromImported(artifacts.Read(inspected!,cancellationToken));
        }
    }

    /// <summary>
    /// 登録クエリの下書きを、公開済みの DB の成果物(DLL)を参照して SDK でビルドする。入口 QueryEntry は DB 生成器がプロジェクトの設定から作る(ManagedQueryEntryGenerator)。
    /// 監査の情報(入口の識別と問い合わせの形)は、ビルドしたソースと生成ファイルを Roslyn で解析して作る(コンパイルはしない)。
    /// </summary>
    internal CompiledSource BuildQuery(Definition definition,byte[] database,CancellationToken cancellationToken=default){
        if(definition.Kind!=DefinitionKind.Query||definition.EntryType is null||definition.EntryMember is null){throw new InvalidOperationException("登録クエリの定義と入口が必要です。");}
        SourceEditor.Validate(definition.Source);
        lock(this.gate){
            var query=new QueryBinding(database,DatabaseName(database),definition.EntryType,definition.EntryMember);
            var draft=this.Prepare(definition.Source,query);
            if(!File.Exists(draft.Assembly)){
                var failure=Run(draft.Project,cancellationToken);
                if(failure is not null){return new([],new(definition.Source.Hash(),"",Diagnostics(failure,draft.Directory,definition.Source),[]));}
            }
            var context=new SourceBuildInspectionContext("server-draft",new(definition,0,0),new(draft.Project,draft.Files));
            SourceProjectBuild? inspected=null;
            SourceBuildArtifactInspector.Inspect(context,draft.Assembly,cancellationToken,(build,captured)=>{artifacts.Store(build,captured,cancellationToken);inspected=build;});
            var compiled=NativeRuntime.FromImported(artifacts.Read(inspected!,cancellationToken));
            try{return compiled with{AuditMetadata=QueryAudit(definition,draft,query,compiled)};}
            catch{compiled.Dispose();throw;}
        }
    }

    /// <summary>
    /// 生成コードの表示：SDK のビルドが書き出した生成ファイル(DB 生成器・FLinq 生成器)と利用者のファイルを返す(11/15 の決定：表示とビルドの結果を一致させる)。
    /// 同じソースのビルドがあれば使い回す。ビルドが誤りで終わっても、生成器が書き出したファイルは返す。
    /// </summary>
    internal GeneratedSourceFile[] GenerateSources(SourceSnapshot source,CancellationToken cancellationToken=default){
        SourceEditor.Validate(source);
        lock(this.gate){
            var draft=this.Prepare(source);
            var generated=Path.Combine(draft.Directory,"generated");
            if(!File.Exists(draft.Assembly)||!Directory.Exists(generated)){_=Run(draft.Project,cancellationToken);}
            var files=source.Files.Select(static file=>new GeneratedSourceFile("User",file.Path,file.Text)).ToList();
            if(!Directory.Exists(generated)){return files.ToArray();}
            foreach(var path in Directory.EnumerateFiles(generated,"*.cs",SearchOption.AllDirectories).Order(StringComparer.Ordinal)){
                var parts=Path.GetRelativePath(generated,path).Split(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
                var kind=parts[0] switch{"Seamless.Database.Generator"=>"Database","Seamless.FLinq.Generator"=>"FLinq",_=>null};
                if(kind is null){continue;}
                files.Add(new(kind,Path.GetFileName(path),File.ReadAllText(path,Encoding.UTF8)));
            }
            return files.ToArray();
        }
    }

    // 参照する DB の成果物の名前(プロジェクトの参照名と出力の依存の名前)。
    private static string DatabaseName(byte[] database){
        using var pe=new System.Reflection.PortableExecutable.PEReader(new MemoryStream(database,false));
        var reader=System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(pe);
        if(!reader.IsAssembly){throw new ManagementException("Validation","登録クエリが参照するDBの成果物が不正です。");}
        return reader.GetString(reader.GetAssemblyDefinition().Name);
    }

    // 監査の情報は、ビルドしたソースと DB 生成器の生成ファイルを解析して作る(内蔵のコンパイルが DB 生成器の後の状態で作っていたのと同じ材料)。
    private static QueryAuditMetadata QueryAudit(Definition definition,DraftDirectory draft,QueryBinding query,CompiledSource compiled){
        var parse=new Microsoft.CodeAnalysis.CSharp.CSharpParseOptions(Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp14,preprocessorSymbols:definition.Source.Defines??[]);
        var trees=definition.Source.Files.Select(file=>Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(file.Text,parse,file.Path)).ToList();
        var generated=Path.Combine(draft.Directory,"generated","Seamless.Database.Generator");
        if(Directory.Exists(generated)){
            foreach(var path in Directory.EnumerateFiles(generated,"*.cs",SearchOption.AllDirectories).Order(StringComparer.Ordinal)){
                trees.Add(Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(File.ReadAllText(path,Encoding.UTF8),parse,path));
            }
        }
        var references=SourceEditor.PlatformReferences().Cast<Microsoft.CodeAnalysis.MetadataReference>().ToList();
        references.AddRange(new ItemsFor().Closure(ReferenceRoots)
            .Select(path=>Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(path)));
        references.Add(Microsoft.CodeAnalysis.MetadataReference.CreateFromImage(query.Database));
        var compilation=Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create("audit",trees,references,new(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary,allowUnsafe:true,nullableContextOptions:Microsoft.CodeAnalysis.NullableContextOptions.Enable));
        var methods=compilation.GetTypeByMetadataName(query.EntryType)?.GetMembers(query.EntryMember).OfType<Microsoft.CodeAnalysis.IMethodSymbol>()
            .Where(method=>method.IsStatic&&method.DeclaredAccessibility==Microsoft.CodeAnalysis.Accessibility.Public&&!method.IsGenericMethod&&method.Parameters.All(parameter=>parameter.RefKind==Microsoft.CodeAnalysis.RefKind.None)).ToArray()??[];
        if(methods.Length!=1){throw new ManagementException("Validation","登録クエリの入口を解析できません。");}
        return QueryAuditMetadataBuilder.Create(methods[0],compilation,compiled.AssemblyBytes,definition.Source.Hash(),compiled.Result.ArtifactHash);
    }

    private sealed record DraftDirectory(string Directory,string Project,string Assembly,SourceProjectFile[] Files);
    // 登録クエリが参照する DB の成果物と入口。
    private sealed record QueryBinding(byte[] Database,string DatabaseName,string EntryType,string EntryMember){
        internal string Key=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(this.Database))+"\n"+this.EntryType+"\n"+this.EntryMember;
    }
    // 作業用のフォルダー(ソースのハッシュごと。登録クエリは参照する DB と入口も含める)へソースとプロジェクトを書く。同じなら同じフォルダー・同じビルドを使い回す。
    private DraftDirectory Prepare(SourceSnapshot source,QueryBinding? query=null){
        var key=source.Hash()[..32];
        if(query is not null){key="q"+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(source.Hash()+"\n"+query.Key)))[..31];}
        var directory=Path.Combine(root,key);
        var name="Seamless.Managed_"+source.Hash()[..24];
        var project=Path.Combine(directory,name+".csproj");
        Directory.CreateDirectory(directory);SourceWorkspace.CheckLinks(directory);
        var files=new List<SourceProjectFile>();
        foreach(var file in source.Files){
            var path=Path.GetFullPath(Path.Combine(directory,file.Path));
            if(!path.StartsWith(Path.GetFullPath(directory)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)){throw new ManagementException("InvalidRequest","ソースの場所が作業用のフォルダーを外れています: "+file.Path);}
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            WriteIfChanged(path,file.Text);
            files.Add(new(file.Path,path));
        }
        string? databasePath=null;
        if(query is not null){
            databasePath=Path.Combine(directory,"database",query.DatabaseName+".dll");
            Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
            if(!File.Exists(databasePath)||!File.ReadAllBytes(databasePath).AsSpan().SequenceEqual(query.Database)){File.WriteAllBytes(databasePath,query.Database);}
        }
        WriteIfChanged(project,Project(source,name,query,databasePath));
        return new(directory,project,Path.Combine(directory,"bin",name+".dll"),files.ToArray());
    }

    // 内蔵のコンパイルと同じ参照(Seamless.Database と Seamless.Linq と、それらが参照する部品)をサーバーの部品から直接参照し、出力へ写す(共有の部品のバイトがサーバーと一致する)。
    private static string Project(SourceSnapshot source,string name,QueryBinding? query=null,string? databasePath=null){
        // 登録クエリの実行の文脈(ManagementExecutionContext)も Seamless.Database にあるので、参照は DB の部品だけで済む(サーバーの部品を成果物に入れない)。
        var references=new ItemsFor().Closure(ReferenceRoots);
        var analyzers=AnalyzerAssemblies.Select(item=>Path.Combine(AppContext.BaseDirectory,item+".dll")).Where(File.Exists).ToArray();
        var project=new XElement("Project",new XAttribute("Sdk","Microsoft.NET.Sdk"),
            new XElement("PropertyGroup",
                new XElement("TargetFramework","net10.0"),new XElement("LangVersion","14"),new XElement("Nullable","enable"),new XElement("AllowUnsafeBlocks","true"),
                new XElement("AssemblyName",name),new XElement("RootNamespace",name),new XElement("OutDir","$(MSBuildProjectDirectory)\\bin\\"),
                new XElement("EnableDefaultCompileItems","false"),new XElement("DebugType","portable"),new XElement("Deterministic","true"),
                new XElement("EnableNETAnalyzers","false"),new XElement("NuGetAudit","false"),new XElement("GenerateDocumentationFile","false"),
                new XElement("InterceptorsNamespaces","$(InterceptorsNamespaces);Intercept"),new XElement("SeamlessManagedDatabase","true"),
                new XElement("EmitCompilerGeneratedFiles","true"),new XElement("CompilerGeneratedFilesOutputPath","generated"),
                new XElement("DefineConstants",string.Join(";",source.Defines??[]))),
            new XElement("ItemGroup",
                source.Files.Select(file=>new XElement("Compile",new XAttribute("Include",file.Path))),
                references.Select(path=>new XElement("Reference",new XAttribute("Include",Path.GetFileNameWithoutExtension(path)),new XElement("HintPath",path),new XElement("Private","true"))),
                analyzers.Select(path=>new XElement("Analyzer",new XAttribute("Include",path))),
                new XElement("CompilerVisibleProperty",new XAttribute("Include","SeamlessManagedDatabase")),
                query is null?null:new XElement("Reference",new XAttribute("Include",query.DatabaseName),new XElement("HintPath",databasePath),new XElement("Private","true")),
                query is null?null:new XElement("CompilerVisibleProperty",new XAttribute("Include","SeamlessQueryEntryType")),
                query is null?null:new XElement("CompilerVisibleProperty",new XAttribute("Include","SeamlessQueryEntryMember"))),
            query is null?null:new XElement("PropertyGroup",new XElement("SeamlessQueryEntryType",query.EntryType),new XElement("SeamlessQueryEntryMember",query.EntryMember)),
            // SDK が自動で足す条件定義(TRACE・RELEASE・NET など)を外し、下書きの条件定義だけにする(内蔵のコンパイルと同じ。検査は PDB の定義と下書きの一致を求める)。
            new XElement("Target",new XAttribute("Name","SeamlessDraftDefines"),new XAttribute("BeforeTargets","CoreCompile"),
                new XElement("PropertyGroup",new XElement("DefineConstants",string.Join(";",source.Defines??[])))));
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

    // 位置の無い診断(生成器が Location.None で出すもの)は「CSC : error 番号: 文」の形になる。
    [GeneratedRegex(@"^\s*(?<path>.+?)(?:\((?<line>\d+),(?<column>\d+)\)| ): (?<severity>error|warning) (?<code>[A-Za-z0-9]+): (?<message>.*?)(?: \[[^\]]+\])?\s*$")]
    private static partial Regex DiagnosticLine();

    // MSBuild の出力の「ファイル(行,列): error 番号: 文」を、ソースの位置付きの診断に直す(位置の無い診断はファイル無しにする)。読めなければ出力の終わりを1件の診断にする。
    private static DiagnosticInfo[] Diagnostics(string log,string directory,SourceSnapshot source){
        var result=new List<DiagnosticInfo>();var seen=new HashSet<string>(StringComparer.Ordinal);
        foreach(var line in log.Split('\n')){
            var match=DiagnosticLine().Match(line);if(!match.Success||!seen.Add(line.Trim())){continue;}
            SourceFile? file=null;
            if(match.Groups["line"].Success){
                var relative=Path.GetRelativePath(directory,Path.GetFullPath(match.Groups["path"].Value)).Replace('\\','/');
                file=source.Files.FirstOrDefault(item=>item.Path.Replace('\\','/')==relative);
            }
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
