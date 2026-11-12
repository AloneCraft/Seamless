using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Seamless.Database.Client;

namespace Test.SourceGenerator;

public sealed class DatabaseNativeClientGenerator{
    [Fact]
    public void Initialize_GeneratesOwnedCollectionsAndPreservesScalarAndWireContracts(){
        var (run,output)=Generate("public sealed class Queries:IQueries{public Queries(Db database){}public int Run(int input)=>input;public System.Collections.Generic.IEnumerable<int> List(int input)=>new[]{input};}public static class Usage{public static async Task<int> Read(QueriesClient client){await using var rows=client.ListAsync(1);var sum=0;await foreach(var row in rows.ConfigureAwait(false)){sum+=row;}return sum;}}",
            "int Run(int input);System.Collections.Generic.IEnumerable<int> List(int input);");
        Assert.DoesNotContain(run.Diagnostics,static diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error);
        Assert.DoesNotContain(output.GetDiagnostics(),static diagnostic=>diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning);
        var text=Assert.Single(Assert.Single(run.Results).GeneratedSources).SourceText.ToString();
        Assert.Contains("global::Seamless.Database.Client.NativeResultStream<int> @ListAsync",text,StringComparison.Ordinal);
        Assert.Contains(".QueryRecordsStream<int,int,global::Sample.IQueries>",text,StringComparison.Ordinal);
        Assert.Contains("global::System.Threading.Tasks.Task<int> @RunAsync",text,StringComparison.Ordinal);
        Assert.Contains(".ExecuteRecordScalarAsync<int,int,global::Sample.IQueries>",text,StringComparison.Ordinal);
        Assert.Contains("NativeRecordQueryContract.For<int,int,global::Sample.IQueries>()",text,StringComparison.Ordinal);
    }
    [Theory,InlineData(false),InlineData(true)]
    public void Initialize_GeneratesAValidatedParameterlessEntry(bool contextConstructor){
        var constructor="public Queries(Db database){}";
        var expected="static context=>new global::Sample.Queries((global::Sample.Db)context.Database)";
        if(contextConstructor){constructor="public Queries(global::Seamless.Database.Client.INativeQueryContext context){}";expected="static context=>new global::Sample.Queries(context)";}
        var (run,output)=Generate("public sealed class Queries:IQueries{"+constructor+"public int Run(int input)=>input;}");
        Assert.DoesNotContain(run.Diagnostics,static diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error);
        Assert.DoesNotContain(output.GetDiagnostics(),static diagnostic=>diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning);
        var text=Assert.Single(Assert.Single(run.Results).GeneratedSources).SourceText.ToString();
        Assert.Contains("public QueriesModule():this("+expected+")",text,StringComparison.Ordinal);
        Assert.Contains("INativeGeneratedQueryModule",text,StringComparison.Ordinal);
        Assert.Contains(".Define(\"Run\",\"db\",schemaFingerprint,false)",text,StringComparison.Ordinal);
        Assert.Contains("DeserializeOwned(arguments.Span",text,StringComparison.Ordinal);
        Assert.True(text.IndexOf("DeserializeOwned(arguments.Span",StringComparison.Ordinal)<text.IndexOf("this.factory(context)",StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("public sealed class Queries:IQueries{private Queries(Db db){}public int Run(int input)=>input;}")]
    [InlineData("public sealed class Queries:IQueries{public Queries(string name){}public int Run(int input)=>input;}")]
    [InlineData("public sealed class Queries:IQueries{public Queries(Db db){}public Queries(global::Seamless.Database.Client.INativeQueryContext context){}public int Run(int input)=>input;}")]
    [InlineData("public abstract class Queries:IQueries{public Queries(Db db){}public abstract int Run(int input);}")]
    [InlineData("public sealed class Queries{public Queries(Db db){}public int Run(int input)=>input;}")]
    [InlineData("public sealed class Other{}")]
    public void Initialize_RejectsInvalidOrAmbiguousServerBindings(string implementation){
        var (run,_)=Generate(implementation);
        Assert.Contains(run.Diagnostics,static diagnostic=>diagnostic.Id=="DBGEN005"&&diagnostic.Severity==DiagnosticSeverity.Error);
        Assert.Empty(Assert.Single(run.Results).GeneratedSources);
    }

    private static (GeneratorDriverRunResult Run,Compilation Output) Generate(string implementation,string methods="int Run(int input);"){
        var source="""
            using System.Threading.Tasks;
            namespace Sample{
                internal static class DatabaseGenerator{
                    public const string ClientContract="Sample.IQueries";
                    public const string ClientModule="module";
                    public const string ClientDatabase="db";
                    public const string ServerImplementation="Sample.Queries";
                }
                public interface IQueries{int Run(int input);}
                public sealed class Db:global::Seamless.Database.DatabaseContainer{}
            """+implementation+"}";
        source=source.Replace("public interface IQueries{int Run(int input);}","public interface IQueries{"+methods+"}",StringComparison.Ordinal);
        var compilation=GeneratorTestHelpers.CreateCompilation([("Native.cs",source)]).AddReferences(MetadataReference.CreateFromFile(typeof(NativeQueryClient).Assembly.Location));
        GeneratorDriver driver=CSharpGeneratorDriver.Create(generators:[new global::Seamless.Database.Generator.DatabaseNativeClientGenerator().AsSourceGenerator()],parseOptions:GeneratorTestHelpers.ParseOptions);
        driver=driver.RunGeneratorsAndUpdateCompilation(compilation,out var output,out _);
        return(driver.GetRunResult(),output);
    }
}
