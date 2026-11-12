using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Seamless.Management;
using Plan=Seamless.Management.ExecutionPlanDocument;

namespace Management.Tests;

public sealed class GeneratedControlFlow{
    [Fact]public void FinalEmittedBodyHasOneAnnotationPassTypedLoopAndRelativeRanges(){
        using var program=Generate("private static int Compute(int[] values)=>values.Where(x=>x>1).Select(x=>x*2).Sum();");
        Assert.Equal(10,program.Invoke());var plan=Assert.Single(program.Plans);var code=Assert.IsType<GeneratedExecutionCode>(plan.GeneratedCode);
        Assert.Equal("Available",code.Status);Assert.Contains("[手順 ",code.BodyText!);Assert.NotEqual(plan.GeneratedBodyHash,code.BodyHash);
        var method=program.Interceptor(plan);var actual=method.SyntaxTree.GetText().ToString(method.Body!.Span);
        Assert.Equal(actual,code.BodyText);Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(actual))),code.BodyHash);
        var annotations=Regex.Matches(actual,@"\[手順 (\d+)\]").Select(match=>match.Groups[1].Value).ToArray();Assert.NotEmpty(annotations);Assert.Equal(annotations.Length,annotations.Distinct().Count());
        Assert.Contains(code.Edges,edge=>edge.To is{} to&&to<=edge.From);Assert.Contains(code.Blocks,block=>block.ConditionKind is "WhenTrue" or "WhenFalse");
        var operations=code.Blocks.SelectMany(block=>block.Operations).Concat(code.Blocks.Where(block=>block.BranchValue is not null).Select(block=>block.BranchValue!)).ToArray();Assert.Contains(operations,operation=>operation.OutputType=="int");
        Assert.All(operations,operation=>{if(operation.Start>=0){Assert.InRange(operation.Start,0,actual.Length);Assert.InRange(operation.Length,0,actual.Length-operation.Start);}else{Assert.Equal(0,operation.Length);}});
        var attribute=SyntaxFactory.Attribute(SyntaxFactory.ParseName("System.Obsolete")).WithArgumentList(SyntaxFactory.AttributeArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.AttributeArgument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression,SyntaxFactory.Literal(new string('x',12000)))))));
        var enlarged=method.AddAttributeLists(SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute)));
        var rewritten=method.SyntaxTree.GetRoot().ReplaceNode(method,enlarged);var reparsed=CSharpSyntaxTree.ParseText(rewritten.ToFullString(),(CSharpParseOptions)method.SyntaxTree.Options).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(node=>node.Identifier.ValueText==method.Identifier.ValueText);
        Assert.NotEqual(method.Body.SpanStart,reparsed.Body!.SpanStart);Assert.Equal(actual,reparsed.Body.ToString());
    }
    [Fact]public void GotoAndLabelRemainInTheExecutedBodyAndCfgUsesActualSuccessors(){
        using var program=Generate("private static int Compute(int[] values){var total=values.Where(x=>x>0).Sum();if(total>0){goto Finished;}total=-1;Finished:return total;}");
        Assert.Equal(6,program.Invoke());var code=Assert.Single(program.Plans).GeneratedCode!;Assert.Equal("Available",code.Status);Assert.Contains("goto ",code.BodyText!);Assert.Contains("Finished",code.BodyText!);
        Assert.Contains(code.Edges,edge=>edge.Kind=="Conditional");Assert.Contains(code.Edges,edge=>edge.Semantics=="Return");
        Assert.All(code.Edges.Where(edge=>edge.To is not null),edge=>Assert.Contains(code.Blocks,block=>block.Id==edge.To));
    }
    [Fact]public void SpanLocalsAreResolvedUsingTheRealGeneratedMethodSignature(){
        using var program=Generate("private static int Compute(int[] values){System.ReadOnlySpan<int> span=values;return span[0]+values.Where(x=>x>1).Sum();}");
        Assert.Equal(6,program.Invoke());var code=Assert.Single(program.Plans).GeneratedCode!;Assert.Equal("Available",code.Status);
        Assert.Contains(code.Blocks.SelectMany(block=>block.Operations),operation=>operation.OutputType?.Contains("ReadOnlySpan<int>",StringComparison.Ordinal)==true);
    }
    [Fact]public void JoinGroupByAndStaticHelperCallsRemainBoundAndExplicitlyLeaveHelperBodiesUnexpanded(){
        using var program=Generate("private static int Compute(int[] values)=>values.Join(values,x=>x,x=>x,(x,y)=>Add(x,y)).GroupBy(x=>x%2).Select(group=>group.Sum()).Sum();private static int Add(int left,int right)=>left+right;");
        Assert.Equal(12,program.Invoke());var code=Assert.Single(program.Plans).GeneratedCode!;Assert.Equal("Available",code.Status);
        Assert.Contains(code.Blocks.SelectMany(block=>block.Operations),operation=>operation.Symbol?.Contains("Add",StringComparison.Ordinal)==true);
        Assert.Contains("GeneratedCode.HelperBodiesNotExpanded",code.Diagnostics);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void LocalFunctionsAndCastDelegatesKeepTheirSeparateBodiesExplicit(bool cast){
        var method="private static int Compute(int[] values){int Adjust(int value)=>value+1;return values.Select(value=>Adjust(value)).Sum();}";var diagnostic="GeneratedCode.LocalFunctionBodiesNotExpanded";var expected=9;
        if(cast){method="private static int Compute(int[] values){var predicate=(System.Func<int,bool>)(value=>value>1);return values.Where(predicate).Sum();}";diagnostic="GeneratedCode.AnonymousFunctionBodiesNotExpanded";expected=5;}
        using var program=Generate(method);Assert.Equal(expected,program.Invoke());var code=Assert.Single(program.Plans).GeneratedCode!;Assert.Equal("Available",code.Status);Assert.Contains(diagnostic,code.Diagnostics);
    }
    [Theory]
    [InlineData("private static int Compute(int[] values){var total=0;int Observe(int value){total+=value;return total;}var rows=values.Select(value=>Observe(value)).ToArray();return total*100+rows.Sum();}",610)]
    [InlineData("private static int Compute(int[] values){T Id<T>(T value)=>value;return values.Select(value=>Id<int>(value)).Sum();}",6)]
    [InlineData("private static int Compute(int[] values)=>values.Select(value=>{int Adjust(int input)=>input+1;return Adjust(value);}).Sum();",9)]
    public void LocalFunctionCallbacksPreserveCapturedCellsGenericBindingAndNestedScope(string method,int expected){
        using var program=Generate(method);Assert.Equal(expected,program.Invoke());Assert.Equal("Available",Assert.Single(program.Plans).GeneratedCode!.Status);
    }
    [Fact]public void BindingErrorsKeepActualBodyButNeverClaimAnAvailableCfg(){
        var code=Capture("class Query{static int Run(){return Missing;}}");Assert.Equal("Unknown",code.GetProperty("status").GetString());Assert.Empty(code.GetProperty("blocks").EnumerateArray());Assert.Contains(code.GetProperty("diagnostics").EnumerateArray(),item=>item.GetString()=="GeneratedCode.BindingError=CS0103");Assert.NotNull(code.GetProperty("bodyHash").GetString());
    }
    [Fact]public void BodyLimitHasAnExplicitStatusAndDoesNotKeepAnUnusableHash(){
        var code=Capture("class Query{static int Run(){var text=\""+new string('x',65536)+"\";return text.Length;}}");Assert.Equal("Limited",code.GetProperty("status").GetString());Assert.Equal(JsonValueKind.Null,code.GetProperty("bodyText").ValueKind);Assert.Equal(JsonValueKind.Null,code.GetProperty("bodyHash").ValueKind);Assert.Empty(code.GetProperty("blocks").EnumerateArray());
    }
    private static JsonElement Capture(string text){
        var tree=CSharpSyntaxTree.ParseText(text,new(LanguageVersion.CSharp14));var compilation=CSharpCompilation.Create("CaptureFixture",[tree],global::Seamless.Management.SourceEditor.PlatformReferences(),new(OutputKind.DynamicallyLinkedLibrary));
        var type=typeof(global::Seamless.FLinq.Generator.FLinqIncrementalGenerator).Assembly.GetType("Seamless.FLinq.Generator.GeneratedControlFlowCapture",throwOnError:true)!;
        var method=tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single();var value=type.GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[method,compilation.GetSemanticModel(tree),tree]);return JsonSerializer.SerializeToElement(value,value!.GetType(),ManagementJson.Options);
    }
    private static GeneratedProgram Generate(string method){
        var parse=new CSharpParseOptions(LanguageVersion.CSharp14).WithFeatures([new("InterceptorsNamespaces","Intercept"),new("InterceptorsPreviewNamespaces","Intercept")]);
        var text="using System.Linq;using Seamless.FLinq;public sealed class Query{[LinqIntercept(true)] "+method+" public static int Run()=>Compute(new[]{1,2,3});}";
        var references=global::Seamless.Management.SourceEditor.PlatformReferences().ToList();references.Add(MetadataReference.CreateFromFile(typeof(global::Seamless.Linq.Enumerable).Assembly.Location));
        var compilation=CSharpCompilation.Create("GeneratedFlow_"+Guid.NewGuid().ToString("N"),[CSharpSyntaxTree.ParseText(text,parse,"generated-flow.cs")],references,new(OutputKind.DynamicallyLinkedLibrary,allowUnsafe:true,optimizationLevel:OptimizationLevel.Release));
        GeneratorDriver driver=CSharpGeneratorDriver.Create([new global::Seamless.FLinq.Generator.FLinqIncrementalGenerator().AsSourceGenerator()],parseOptions:parse);
        driver=driver.RunGeneratorsAndUpdateCompilation(compilation,out var output,out var diagnostics);Assert.DoesNotContain(diagnostics,item=>item.Severity==DiagnosticSeverity.Error||item.Id=="FLINQ002");
        using var image=new MemoryStream();var emitted=output.Emit(image);Assert.True(emitted.Success,string.Join("\n",emitted.Diagnostics));image.Position=0;var plans=Plan.Read(image);Assert.NotEmpty(plans);
        return new(output,image.ToArray(),plans);
    }
    private sealed class GeneratedProgram:IDisposable{
        private readonly AssemblyLoadContext context=new("GeneratedFlow",isCollectible:true);
        private readonly Assembly assembly;
        private readonly Compilation output;
        internal Plan[] Plans{get;}
        internal GeneratedProgram(Compilation output,byte[] image,Plan[] plans){this.output=output;this.Plans=plans;using var stream=new MemoryStream(image);this.assembly=this.context.LoadFromStream(stream);}
        internal object? Invoke()=>this.assembly.GetType("Query",throwOnError:true)!.GetMethod("Run")!.Invoke(null,null);
        internal MethodDeclarationSyntax Interceptor(Plan plan)=>this.output.SyntaxTrees.SelectMany(tree=>tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()).Single(method=>method.AttributeLists.SelectMany(list=>list.Attributes).Any(attribute=>attribute.Name.ToString().EndsWith(".ExecutionPlanAttribute",StringComparison.Ordinal)&&attribute.ArgumentList!.Arguments[1].Expression is LiteralExpressionSyntax literal&&literal.Token.ValueText.Contains(plan.GeneratedCode!.BodyHash!,StringComparison.Ordinal)));
        public void Dispose()=>this.context.Unload();
    }
}
