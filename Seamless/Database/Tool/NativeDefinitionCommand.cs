using Seamless.Database.Rdbms;
using Seamless.Database.SqlServer;
using System.Security.Cryptography;
using Seamless.Database.Compilation;

namespace Seamless.Database.Tool;

/// <summary>変換専用CLI。DB接続・データ転送・比較実行を開始しない。</summary>
internal static class NativeDefinitionCommand{
    public static async Task<int>RunAsync(string[]arguments,CancellationToken cancellationToken=default){
        var options=ParseOptions(arguments);
        string Required(string name){if(options.TryGetValue(name,out var values)&&values.Count==1){return values[0];}throw new ArgumentException("Exactly one --"+name+" is required.");}
        var input=Path.GetFullPath(Required("snapshot"));var directory=Path.GetDirectoryName(input)!;
        var output=Path.GetFullPath(Required("output"));
        if(string.Equals(Path.GetPathRoot(output),@"C:\",StringComparison.OrdinalIgnoreCase)){throw new ArgumentException("Conversion output must not use C drive.");}
        if(Directory.Exists(output)||File.Exists(output)){throw new IOException("Output already exists; select a new export directory.");}
        var vendor=Required("vendor") switch{"sqlserver"=>SqlVendor.SqlServer,"mysql"=>SqlVendor.MySql,_=>throw new ArgumentException("Vendor must be sqlserver or mysql.")};
        var bytes=await File.ReadAllBytesAsync(input,cancellationToken).ConfigureAwait(false);
        using var document=JsonDocument.Parse(bytes);var root=document.RootElement;
        string Read(string name,string fallback){if(root.TryGetProperty(name,out var value)){return value.GetString()??fallback;}return fallback;}
        if(Read("TargetFramework","net10.0")!="net10.0"||Read("LanguageVersion","14")!="14"){throw new NotSupportedException("The source snapshot must target net10.0 and C# 14.");}
        var sources=new List<NativeSourceFile>();
        if(!root.TryGetProperty("Sources",out var files)){files=root.GetProperty("Files");}
        foreach(var file in files.EnumerateArray()){
            var path=file.GetProperty("Path").GetString()??throw new InvalidDataException("Source path is missing.");
            string?text=null;
            if(file.TryGetProperty("Content",out var content)){text=content.GetString();}
            else if(file.TryGetProperty("Text",out content)){text=content.GetString();}
            text??=await File.ReadAllTextAsync(Path.GetFullPath(path,directory),cancellationToken).ConfigureAwait(false);
            sources.Add(new(path,text));
        }
        string[]references=[];
        if(options.TryGetValue("reference",out var supplied)){references=supplied.Select(p=>Path.GetFullPath(p,directory)).ToArray();}
        var platform=((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?.Split(Path.PathSeparator)??[];
        if(root.TryGetProperty("References",out var expectedReferences)){
            foreach(var expected in expectedReferences.EnumerateArray()){
                var name=expected.GetProperty("Name").GetString();var expectedHash=expected.GetProperty("Sha256").GetString();
                var candidates=references.Concat(platform).Where(p=>Path.GetFileNameWithoutExtension(p)==name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                if(candidates.Length!=1||!Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(candidates[0],cancellationToken).ConfigureAwait(false))).Equals(expectedHash,StringComparison.OrdinalIgnoreCase)){throw new InvalidDataException("Source reference mismatch: "+name);}
            }
        }
        string[]defines=[];
        if(root.TryGetProperty("Defines",out var symbols)&&symbols.ValueKind==JsonValueKind.Array){defines=symbols.EnumerateArray().Select(s=>s.GetString()!).ToArray();}
        var checkOverflow=root.TryGetProperty("CheckOverflow",out var overflow)&&overflow.GetBoolean();
        var snapshot=NativeCompilationSnapshot.Create(Read("Revision","file-export"),sources,references,defines,checkOverflow,originSnapshotHash:Read("SourceHash",Convert.ToHexString(SHA256.HashData(bytes))));
        var analysis=new NativeDatabaseCompiler().Analyze(snapshot,Required("type"),cancellationToken);
        var generated=new VendorSqlGenerator().Generate(analysis.Database,vendor);
        var diagnostics=analysis.Diagnostics.Concat(generated.Diagnostics).ToArray();
        var failed=diagnostics.Any(d=>d.Severity==NativeDiagnosticSeverity.CompilationError);
        var partial=diagnostics.Any(d=>d.Severity==NativeDiagnosticSeverity.Unsupported);
        var stage=output+".staging."+Guid.NewGuid().ToString("N");Directory.CreateDirectory(stage);
        var json=new JsonSerializerOptions{WriteIndented=true};
        await File.WriteAllTextAsync(Path.Combine(stage,"model.json"),JsonSerializer.Serialize(analysis.Database,json),cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(stage,"diagnostics.json"),JsonSerializer.Serialize(diagnostics,json),cancellationToken).ConfigureAwait(false);
        var script=new StringBuilder();
        foreach(var statement in generated.Statements){
            if(vendor==SqlVendor.SqlServer){script.AppendLine(statement).AppendLine("GO");}
            else if(statement.StartsWith("CREATE PROCEDURE",StringComparison.Ordinal)){script.AppendLine("DELIMITER $$").AppendLine(statement+"$$").AppendLine("DELIMITER ;");}
            else{script.AppendLine(statement);}
        }
        await File.WriteAllTextAsync(Path.Combine(stage,"schema.sql"),script.ToString(),cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(stage,"queries.json"),JsonSerializer.Serialize(generated.Queries,json),cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(stage,"statements.json"),JsonSerializer.Serialize(generated.Statements,json),cancellationToken).ConfigureAwait(false);
        var status="Converted";if(failed){status="Failed";}else if(partial){status="Partial";}
        await File.WriteAllTextAsync(Path.Combine(stage,"manifest.json"),JsonSerializer.Serialize(new{Format="SeamlessDefinitionExportV1",Status=status,Vendor=vendor.ToString(),snapshot.Revision,snapshot.Fingerprint,snapshot.OriginSnapshotHash,Database=analysis.Database.Name,Tables=analysis.Database.Tables.Length,Queries=generated.Queries.Length,Diagnostics=diagnostics.Length},json),cancellationToken).ConfigureAwait(false);
        Directory.Move(stage,output);
        Console.WriteLine($"{status}: {output}; tables={analysis.Database.Tables.Length}; queries={generated.Queries.Length}; diagnostics={diagnostics.Length}");
        if(failed){return 1;}if(partial){return 3;}return 0;
    }

    public static async Task<int>RunSqlAsync(string[]arguments,CancellationToken cancellationToken=default){
        var options=ParseOptions(arguments);
        string Required(string name){if(!options.TryGetValue(name,out var values)||values.Count!=1){throw new ArgumentException("Exactly one --"+name+" is required.");}return values[0];}
        var input=Path.GetFullPath(Required("sql"));var output=Path.GetFullPath(Required("output"));
        if(string.Equals(Path.GetPathRoot(output),@"C:\",StringComparison.OrdinalIgnoreCase)){throw new ArgumentException("Conversion output must not use C drive.");}
        if(Directory.Exists(output)||File.Exists(output)){throw new IOException("Output already exists; select a new export directory.");}
        var vendor=Required("vendor") switch{"sqlserver"=>SqlVendor.SqlServer,"mysql"=>SqlVendor.MySql,_=>throw new ArgumentException("Vendor must be sqlserver or mysql.")};
        var sql=await File.ReadAllTextAsync(input,cancellationToken).ConfigureAwait(false);
        var result=new SqlDefinitionConverter().Convert(sql,vendor,Required("name"));
        var stage=output+".staging."+Guid.NewGuid().ToString("N");Directory.CreateDirectory(stage);
        var json=new JsonSerializerOptions{WriteIndented=true};
        await File.WriteAllTextAsync(Path.Combine(stage,"source.sql"),result.SourceSql,cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(stage,"diagnostics.json"),JsonSerializer.Serialize(result.Diagnostics,json),cancellationToken).ConfigureAwait(false);
        if(result.CSharp is{}generated){
            var files=Path.Combine(stage,"sources");Directory.CreateDirectory(files);
            foreach(var file in generated.Files){
                var path=Path.GetFullPath(file.RelativePath,files);
                if(!path.StartsWith(files+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)){throw new InvalidDataException("Generated file is outside the output directory.");}
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(path,file.Content,cancellationToken).ConfigureAwait(false);
            }
            await File.WriteAllTextAsync(Path.Combine(stage,"snapshot.json"),JsonSerializer.Serialize(new{Revision="sql-import-"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql))),TargetFramework="net10.0",LanguageVersion="14",Sources=generated.Files.Select(f=>new{Path=f.RelativePath,Content=f.Content})},json),cancellationToken).ConfigureAwait(false);
        }
        var failed=result.Diagnostics.Any(d=>d.Severity==NativeDiagnosticSeverity.CompilationError);
        var partial=result.Diagnostics.Any(d=>d.Severity==NativeDiagnosticSeverity.Unsupported)||result.CSharp is null;
        var status="Converted";if(failed){status="Failed";}else if(partial){status="Partial";}
        await File.WriteAllTextAsync(Path.Combine(stage,"manifest.json"),JsonSerializer.Serialize(new{Format="SeamlessSqlDefinitionImportV1",Status=status,Vendor=vendor.ToString(),SourceSha256=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql))),TypeName=result.CSharp?.TypeNamespace+"."+result.CSharp?.DatabaseName,Diagnostics=result.Diagnostics.Length},json),cancellationToken).ConfigureAwait(false);
        Directory.Move(stage,output);Console.WriteLine($"{status}: {output}; diagnostics={result.Diagnostics.Length}");
        if(failed){return 1;}if(partial){return 3;}return 0;
    }

    private static Dictionary<string,List<string>>ParseOptions(string[]arguments){
        var options=new Dictionary<string,List<string>>(StringComparer.Ordinal);
        for(var index=0;index<arguments.Length;index+=2){
            if(index+1>=arguments.Length||!arguments[index].StartsWith("--",StringComparison.Ordinal)){throw new ArgumentException("Expected --option value.");}
            var key=arguments[index][2..];if(!options.TryGetValue(key,out var values)){values=[];options.Add(key,values);}values.Add(arguments[index+1]);
        }
        return options;
    }
}
