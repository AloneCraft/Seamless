using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Seamless.FLinq;

namespace Test.SourceGenerator.LastDemandBoundary;

// 候補。元の BoundaryProbes 入力を変更せず、親の段階受入で実行する。
[LinqIntercept(false)]
public sealed class Last{
    // 原bytesを保持し、raw string終端の改行除去で入力hashが変わることを防ぐ。
    private static readonly string BoundarySource=Encoding.UTF8.GetString(Convert.FromBase64String("Ly8g54us56uL5YWl5Yqb5YCZ6KOc44CC5pei5a2YNDE044GuaW504oaSaW5044Go5YWoUD10cnVl44Gg44GR44Gn44Gv6Kaz5ris44Gn44GN44Gq44GE5LiN5aSJ5p2h5Lu255So44CCCi8vIOimquOBrueUn+aIkGhhcm5lc3Pjgac4IGludGVyY2VwdG9yL2VtaXTjgpLnorroqo3jgZfjgIFSdW4oaWQsMC8xKeOBqFJ1bihpZCwyKeOCkuavlOi8g+OBmeOCi+OAggp1c2luZyBTeXN0ZW07CnVzaW5nIFN5c3RlbS5Db2xsZWN0aW9uczsKdXNpbmcgU3lzdGVtLkNvbGxlY3Rpb25zLkdlbmVyaWM7CnVzaW5nIFN5c3RlbS5HbG9iYWxpemF0aW9uOwp1c2luZyBTeXN0ZW0uTGlucTsKdXNpbmcgU2VhbWxlc3MuRkxpbnE7CnVzaW5nIFNlYW1sZXNzLkxpbnE7CgpuYW1lc3BhY2UgU2FtcGxlOwoKW0xpbnFJbnRlcmNlcHQoZmFsc2UpXQpwdWJsaWMgc2VhbGVkIGNsYXNzIEJvdW5kYXJ5UHJvYmVzewogICAgcHJpdmF0ZSByZWFkb25seSBMaXN0PHN0cmluZz4gVHJhY2U9bmV3KCk7CgogICAgcHVibGljIHN0YXRpYyBvYmplY3RbXSBSdW4oaW50IGlkLGludCBtb2RlKXsKICAgICAgICB2YXIgcHJvYmU9bmV3IEJvdW5kYXJ5UHJvYmVzKCk7CiAgICAgICAgaW50W10gdmFsdWVzOwogICAgICAgIGlmKGlkPT0yfHxpZD09NCl7dmFsdWVzPVstMywtMV07fQogICAgICAgIGVsc2UgaWYoaWQ9PTMpe3ZhbHVlcz1bXTt9CiAgICAgICAgZWxzZXt2YWx1ZXM9Wy0zLC0xLDIsNF07fQogICAgICAgIHVzaW5nIHZhciBzY29wZT1MaW5xQ291bnRlclNjb3BlLlN0YXJ0KCk7CiAgICAgICAgdmFyIHZhbHVlPSIiO3ZhciBlcnJvcj0iIjt2YXIgbWVzc2FnZT0iIjsKICAgICAgICB0cnl7CiAgICAgICAgICAgIGludCByZXN1bHQ7CiAgICAgICAgICAgIGlmKGlkPT0wKXsKICAgICAgICAgICAgICAgIGlmKG1vZGU9PTApe3Jlc3VsdD1wcm9iZS5Qcm9qZWN0aW9uQWxsKHZhbHVlcyk7fQogICAgICAgICAgICAgICAgZWxzZSBpZihtb2RlPT0xKXtyZXN1bHQ9cHJvYmUuUHJvamVjdGlvbk5vbmUodmFsdWVzKTt9CiAgICAgICAgICAgICAgICBlbHNle3Jlc3VsdD1wcm9iZS5Qcm9qZWN0aW9uU3lzdGVtKHZhbHVlcyk7fQogICAgICAgICAgICB9ZWxzZSBpZihpZD09NSl7CiAgICAgICAgICAgICAgICBMaXN0PGludD4gbGlzdD1uZXcgUmVpbXBsZW1lbnRlZExpc3QodmFsdWVzKTsKICAgICAgICAgICAgICAgIGlmKG1vZGU9PTApe3Jlc3VsdD1wcm9iZS5MaXN0QWxsKGxpc3QpO30KICAgICAgICAgICAgICAgIGVsc2UgaWYobW9kZT09MSl7cmVzdWx0PXByb2JlLkxpc3ROb25lKGxpc3QpO30KICAgICAgICAgICAgICAgIGVsc2V7cmVzdWx0PXByb2JlLkxpc3RTeXN0ZW0obGlzdCk7fQogICAgICAgICAgICB9ZWxzZSBpZihpZD09NCl7CiAgICAgICAgICAgICAgICBpZihtb2RlPT0wKXtyZXN1bHQ9cHJvYmUuT3JkZXJlZERlZmF1bHRBbGwodmFsdWVzKTt9CiAgICAgICAgICAgICAgICBlbHNlIGlmKG1vZGU9PTEpe3Jlc3VsdD1wcm9iZS5PcmRlcmVkRGVmYXVsdE5vbmUodmFsdWVzKTt9CiAgICAgICAgICAgICAgICBlbHNle3Jlc3VsdD1wcm9iZS5PcmRlcmVkRGVmYXVsdFN5c3RlbSh2YWx1ZXMpO30KICAgICAgICAgICAgfWVsc2V7CiAgICAgICAgICAgICAgICBpZihtb2RlPT0wKXtyZXN1bHQ9cHJvYmUuT3JkZXJlZEFsbCh2YWx1ZXMpO30KICAgICAgICAgICAgICAgIGVsc2UgaWYobW9kZT09MSl7cmVzdWx0PXByb2JlLk9yZGVyZWROb25lKHZhbHVlcyk7fQogICAgICAgICAgICAgICAgZWxzZXtyZXN1bHQ9cHJvYmUuT3JkZXJlZFN5c3RlbSh2YWx1ZXMpO30KICAgICAgICAgICAgfQogICAgICAgICAgICB2YWx1ZT1yZXN1bHQuVG9TdHJpbmcoQ3VsdHVyZUluZm8uSW52YXJpYW50Q3VsdHVyZSk7CiAgICAgICAgfWNhdGNoKEV4Y2VwdGlvbiBleGNlcHRpb24pe2Vycm9yPWV4Y2VwdGlvbi5HZXRUeXBlKCkuRnVsbE5hbWUhO21lc3NhZ2U9ZXhjZXB0aW9uLk1lc3NhZ2U7fQogICAgICAgIHJldHVybiBuZXcgb2JqZWN0W117dmFsdWUsZXJyb3IsbWVzc2FnZSxwcm9iZS5UcmFjZS5Ub0FycmF5KCksc2NvcGUuQ291bnRlci5QaHlzaWNhbERlbGVnYXRlLHNjb3BlLkNvdW50ZXIuRW51bWVyYWJsZUZhbGxiYWNrfTsKICAgIH0KCiAgICBbTGlucUludGVyY2VwdCh0cnVlLExpbnFJbnN0cnVtZW50YXRpb24uQ291bnRlcildCiAgICBwcml2YXRlIGludCBQcm9qZWN0aW9uQWxsKGludFtdIHZhbHVlcyk9PnZhbHVlcy5TZWxlY3QoeD0+dGhpcy5Qcm9qZWN0KHgpKS5XaGVyZSh4PT50aGlzLktlZXBUZXh0KHgpKS5TZWxlY3QoeD0+dGhpcy5GaW5pc2goeCkpLkxhc3QoKTsKICAgIFtMaW5xSW50ZXJjZXB0KHRydWUsTGlucUluc3RydW1lbnRhdGlvbi5Db3VudGVyLE9wdGltaXphdGlvbnM9TGlucU9wdGltaXphdGlvbnMuTm9uZSldCiAgICBwcml2YXRlIGludCBQcm9qZWN0aW9uTm9uZShpbnRbXSB2YWx1ZXMpPT52YWx1ZXMuU2VsZWN0KHg9PnRoaXMuUHJvamVjdCh4KSkuV2hlcmUoeD0+dGhpcy5LZWVwVGV4dCh4KSkuU2VsZWN0KHg9PnRoaXMuRmluaXNoKHgpKS5MYXN0KCk7CiAgICBwcml2YXRlIGludCBQcm9qZWN0aW9uU3lzdGVtKGludFtdIHZhbHVlcyk9PnZhbHVlcy5TZWxlY3QoeD0+dGhpcy5Qcm9qZWN0KHgpKS5XaGVyZSh4PT50aGlzLktlZXBUZXh0KHgpKS5TZWxlY3QoeD0+dGhpcy5GaW5pc2goeCkpLkxhc3QoKTsKCiAgICBbTGlucUludGVyY2VwdCh0cnVlLExpbnFJbnN0cnVtZW50YXRpb24uQ291bnRlcildCiAgICBwcml2YXRlIGludCBPcmRlcmVkQWxsKGludFtdIHZhbHVlcyk9PnZhbHVlcy5PcmRlckJ5KHg9PnRoaXMuS2V5KHgpKS5MYXN0KHg9PnRoaXMuQWNjZXB0KHgpKTsKICAgIFtMaW5xSW50ZXJjZXB0KHRydWUsTGlucUluc3RydW1lbnRhdGlvbi5Db3VudGVyLE9wdGltaXphdGlvbnM9TGlucU9wdGltaXphdGlvbnMuTm9uZSldCiAgICBwcml2YXRlIGludCBPcmRlcmVkTm9uZShpbnRbXSB2YWx1ZXMpPT52YWx1ZXMuT3JkZXJCeSh4PT50aGlzLktleSh4KSkuTGFzdCh4PT50aGlzLkFjY2VwdCh4KSk7CiAgICBwcml2YXRlIGludCBPcmRlcmVkU3lzdGVtKGludFtdIHZhbHVlcyk9PnZhbHVlcy5PcmRlckJ5KHg9PnRoaXMuS2V5KHgpKS5MYXN0KHg9PnRoaXMuQWNjZXB0KHgpKTsKCiAgICBbTGlucUludGVyY2VwdCh0cnVlLExpbnFJbnN0cnVtZW50YXRpb24uQ291bnRlcildCiAgICBwcml2YXRlIGludCBPcmRlcmVkRGVmYXVsdEFsbChpbnRbXSB2YWx1ZXMpPT52YWx1ZXMuT3JkZXJCeSh4PT50aGlzLktleSh4KSkuTGFzdE9yRGVmYXVsdCh4PT50aGlzLkFjY2VwdCh4KSk7CiAgICBbTGlucUludGVyY2VwdCh0cnVlLExpbnFJbnN0cnVtZW50YXRpb24uQ291bnRlcixPcHRpbWl6YXRpb25zPUxpbnFPcHRpbWl6YXRpb25zLk5vbmUpXQogICAgcHJpdmF0ZSBpbnQgT3JkZXJlZERlZmF1bHROb25lKGludFtdIHZhbHVlcyk9PnZhbHVlcy5PcmRlckJ5KHg9PnRoaXMuS2V5KHgpKS5MYXN0T3JEZWZhdWx0KHg9PnRoaXMuQWNjZXB0KHgpKTsKICAgIHByaXZhdGUgaW50IE9yZGVyZWREZWZhdWx0U3lzdGVtKGludFtdIHZhbHVlcyk9PnZhbHVlcy5PcmRlckJ5KHg9PnRoaXMuS2V5KHgpKS5MYXN0T3JEZWZhdWx0KHg9PnRoaXMuQWNjZXB0KHgpKTsKCiAgICBbTGlucUludGVyY2VwdCh0cnVlLExpbnFJbnN0cnVtZW50YXRpb24uQ291bnRlcildCiAgICBwcml2YXRlIGludCBMaXN0QWxsKExpc3Q8aW50PiB2YWx1ZXMpPT52YWx1ZXMuU2VsZWN0KHg9PnRoaXMuUHJvamVjdCh4KSkuV2hlcmUoeD0+dGhpcy5LZWVwVGV4dCh4KSkuU2VsZWN0KHg9PnRoaXMuRmluaXNoKHgpKS5MYXN0KCk7CiAgICBbTGlucUludGVyY2VwdCh0cnVlLExpbnFJbnN0cnVtZW50YXRpb24uQ291bnRlcixPcHRpbWl6YXRpb25zPUxpbnFPcHRpbWl6YXRpb25zLk5vbmUpXQogICAgcHJpdmF0ZSBpbnQgTGlzdE5vbmUoTGlzdDxpbnQ+IHZhbHVlcyk9PnZhbHVlcy5TZWxlY3QoeD0+dGhpcy5Qcm9qZWN0KHgpKS5XaGVyZSh4PT50aGlzLktlZXBUZXh0KHgpKS5TZWxlY3QoeD0+dGhpcy5GaW5pc2goeCkpLkxhc3QoKTsKICAgIHByaXZhdGUgaW50IExpc3RTeXN0ZW0oTGlzdDxpbnQ+IHZhbHVlcyk9PnZhbHVlcy5TZWxlY3QoeD0+dGhpcy5Qcm9qZWN0KHgpKS5XaGVyZSh4PT50aGlzLktlZXBUZXh0KHgpKS5TZWxlY3QoeD0+dGhpcy5GaW5pc2goeCkpLkxhc3QoKTsKCiAgICBwcml2YXRlIHNlYWxlZCBjbGFzcyBSZWltcGxlbWVudGVkTGlzdChJRW51bWVyYWJsZTxpbnQ+IHZhbHVlcyk6TGlzdDxpbnQ+KHZhbHVlcyksSUVudW1lcmFibGU8aW50PnsKICAgICAgICBJRW51bWVyYXRvcjxpbnQ+IElFbnVtZXJhYmxlPGludD4uR2V0RW51bWVyYXRvcigpPT50aHJvdyBuZXcgSW52YWxpZE9wZXJhdGlvbkV4Y2VwdGlvbigiZGVyaXZlZCBpbnRlcmZhY2UgZW51bWVyYXRvciIpOwogICAgICAgIElFbnVtZXJhdG9yIElFbnVtZXJhYmxlLkdldEVudW1lcmF0b3IoKT0+dGhyb3cgbmV3IEludmFsaWRPcGVyYXRpb25FeGNlcHRpb24oImRlcml2ZWQgaW50ZXJmYWNlIGVudW1lcmF0b3IiKTsKICAgIH0KCiAgICBwdWJsaWMgc3RyaW5nIFByb2plY3QoaW50IHZhbHVlKXt0aGlzLlRyYWNlLkFkZCgiUzE6Iit2YWx1ZSk7cmV0dXJuIHZhbHVlLlRvU3RyaW5nKEN1bHR1cmVJbmZvLkludmFyaWFudEN1bHR1cmUpO30KICAgIHB1YmxpYyBib29sIEtlZXBUZXh0KHN0cmluZyB2YWx1ZSl7dGhpcy5UcmFjZS5BZGQoIlc6Iit2YWx1ZSk7cmV0dXJuIGludC5QYXJzZSh2YWx1ZSxDdWx0dXJlSW5mby5JbnZhcmlhbnRDdWx0dXJlKT4wO30KICAgIHB1YmxpYyBpbnQgRmluaXNoKHN0cmluZyB2YWx1ZSl7dGhpcy5UcmFjZS5BZGQoIlMyOiIrdmFsdWUpO3JldHVybiBpbnQuUGFyc2UodmFsdWUsQ3VsdHVyZUluZm8uSW52YXJpYW50Q3VsdHVyZSk7fQogICAgcHVibGljIGJvb2wgQWNjZXB0KGludCB2YWx1ZSl7dGhpcy5UcmFjZS5BZGQoIlA6Iit2YWx1ZSk7cmV0dXJuIHZhbHVlPjA7fQogICAgcHVibGljIGludCBLZXkoaW50IHZhbHVlKXsKICAgICAgICB0aGlzLlRyYWNlLkFkZCgiSzoiK3ZhbHVlKTsKICAgICAgICBpZih2YWx1ZTwwKXt0aHJvdyBuZXcgSW52YWxpZE9wZXJhdGlvbkV4Y2VwdGlvbigia2V5IG9mIHJlamVjdGVkIGVsZW1lbnQiKTt9CiAgICAgICAgcmV0dXJuIHZhbHVlJjE7CiAgICB9Cn0K"));

    [Fact]
    public void BoundaryProbesMatchSystem(){
        var failures=new List<string>();
        var observations=new List<object>();
        var options=new CSharpParseOptions(LanguageVersion.Preview).WithFeatures([
            new("InterceptorsPreview","true"),new("InterceptorsNamespaces","Intercept"),new("InterceptorsPreviewNamespaces","Intercept")
        ]);
        var compilation=GeneratorTestHelpers.CreateCompilation([("BoundaryProbes.cs",BoundarySource)]).WithAssemblyName("LastDemandBoundary");
        compilation=compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.Single(),CSharpSyntaxTree.ParseText(BoundarySource,options,path:"BoundaryProbes.cs"));
        GeneratorDriver driver=CSharpGeneratorDriver.Create([new global::Seamless.FLinq.Generator.FLinqIncrementalGenerator().AsSourceGenerator()],parseOptions:options);
        driver=driver.RunGeneratorsAndUpdateCompilation(compilation,out var output,out var generatorDiagnostics);
        var generated=string.Concat(output.SyntaxTrees.Skip(1).Select(tree=>tree.ToString()));
        var diagnostics=generatorDiagnostics.Concat(output.GetDiagnostics()).ToList();
        var errors=diagnostics.Where(diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error).Select(diagnostic=>diagnostic.ToString()).ToList();
        var interceptionNames=new[]{"ProjectionAll","ProjectionNone","OrderedAll","OrderedNone","OrderedDefaultAll","OrderedDefaultNone","ListAll","ListNone"};
        var interceptorCount=generated.Split("InterceptsLocationAttribute(1,",StringSplitOptions.None).Length-1;
        if(interceptorCount!=8){errors.Add("InterceptionCount: expected=8, actual="+interceptorCount);}
        foreach(var name in interceptionNames){
            if(!generated.Contains("OptimizedLinq_"+name+"_",StringComparison.Ordinal)){errors.Add("InterceptionSourceMissing: "+name);}
        }
        var emitSucceeded=false;
        var dllSha256="";
        var executed=false;
        if(errors.Count==0){
            using var pe=new MemoryStream();
            var emit=output.Emit(pe);
            diagnostics.AddRange(emit.Diagnostics);
            emitSucceeded=emit.Success;
            if(!emit.Success){errors.AddRange(emit.Diagnostics.Where(diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error).Select(diagnostic=>diagnostic.ToString()));}
            else{
                var assemblyBytes=pe.ToArray();
                dllSha256=Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(assemblyBytes));
                var context=new global::System.Runtime.Loader.AssemblyLoadContext("LastDemandBoundary",isCollectible:true);
                try{
                    using var assemblyStream=new MemoryStream(assemblyBytes);
                    var type=context.LoadFromStream(assemblyStream).GetType("Sample.BoundaryProbes")!;
                    var run=type.GetMethod("Run")!;
                    for(var scenario=0;scenario<6;scenario++){
                        var optimized=(object[])run.Invoke(null,[scenario,0])!;
                        var noOptimizations=(object[])run.Invoke(null,[scenario,1])!;
                        var system=(object[])run.Invoke(null,[scenario,2])!;
                        Compare(scenario,"All",optimized,system,failures,observations);
                        Compare(scenario,"None",noOptimizations,system,failures,observations);
                    }
                    executed=true;
                }catch(Exception exception){failures.Add("Execution: "+exception);}
                finally{context.Unload();}
            }
        }
        Save(BoundarySource,generated,dllSha256,new{ScenarioCount=6,InterceptionExpected=8,InterceptionActual=interceptorCount,ExpectedSystemComparisons=12,ActualSystemComparisons=observations.Count,Errors=errors,EmitSucceeded=emitSucceeded,Executed=executed,DllSHA256=dllSha256,Diagnostics=diagnostics.Select(diagnostic=>diagnostic.ToString()).ToArray(),Failures=failures,Observations=observations});
        Assert.Equal(12,observations.Count);
        Assert.True(errors.Count==0&&failures.Count==0,string.Join(Environment.NewLine,errors.Concat(failures)));
    }

    private static void Compare(int scenario,string mode,object[] actual,object[] expected,List<string> failures,List<object> observations){
        var differences=new List<string>();
        for(var index=0;index<3;index++){
            if(!Equals(actual[index],expected[index])){differences.Add("Field["+index+"]: actual="+Format(actual[index])+", System="+Format(expected[index]));}
        }
        if(!((string[])actual[3]).SequenceEqual((string[])expected[3])){differences.Add("Trace: actual="+Format(actual[3])+", System="+Format(expected[3]));}
        if((long)actual[4]!=0){differences.Add("PhysicalDelegate: "+actual[4]);}
        if((long)actual[5]!=0){differences.Add("EnumerableFallback: "+actual[5]);}
        var label="Scenario="+scenario+", Mode="+mode;
        failures.AddRange(differences.Select(difference=>label+": "+difference));
        observations.Add(new{Scenario=scenario,Mode=mode,Actual=Snapshot(actual),System=Snapshot(expected),Differences=differences});
    }

    private static string Format(object? value){
        if(value is string[] items){return string.Join(",",items);}
        return value?.ToString()??"<null>";
    }

    private static object Snapshot(object[] value)=>new{Value=value[0],ExceptionType=value[1],ExceptionMessage=value[2],Trace=value[3],PhysicalDelegate=value[4],EnumerableFallback=value[5]};

    private static void Save(string source,string generated,string dllSha256,object observations){
        var root=Environment.GetEnvironmentVariable("SEAMLESS_LAST_BOUNDARY_ARTIFACT_DIR");
        if(string.IsNullOrWhiteSpace(root)){return;}
        var directory=Path.GetFullPath(root);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory,"input.cs"),source);
        File.WriteAllText(Path.Combine(directory,"generated.cs"),generated);
        File.WriteAllText(Path.Combine(directory,"observations.json"),JsonSerializer.Serialize(observations,new JsonSerializerOptions{WriteIndented=true}));
        static string Hash(string text)=>Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        File.WriteAllText(Path.Combine(directory,"hashes.json"),JsonSerializer.Serialize(new{SourceSHA256=Hash(source),GeneratedSHA256=Hash(generated),DllSHA256=dllSha256,GeneratorSHA256=Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(global::Seamless.FLinq.Generator.FLinqIncrementalGenerator).Assembly.Location))),Runtime=Environment.Version.ToString()},new JsonSerializerOptions{WriteIndented=true}));
    }
}