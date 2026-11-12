using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Seamless.Execution;
using Seamless.Management;
using Profiler=Seamless.Execution.QueryProfiler;
using PlanDocument=Seamless.Management.ExecutionPlanDocument;

namespace Management.Tests;

public sealed class QueryProfiler{
    [Fact]public void FusedQueryUsesOneEntrySelectionAndMapsItsObservedRegionToTheActualPlanNode(){
        using var program=Generate();Assert.Equal(5,program.Invoke([1,2,3]));
        var budget=new ResourceBudget(1<<20,0);QueryProfileSnapshot snapshot;
        using(var profiler=Profiler.Begin(budget)){
            Assert.Equal(5,program.Invoke([1,2,3]));snapshot=profiler.Complete();
        }
        Assert.Equal(0,budget.MemoryBytes);var region=Assert.Single(snapshot.Regions);var plan=Assert.Single(program.Plans);
        Assert.Equal(plan.MethodKey,region.Target.MethodKey);Assert.Equal(plan.SourceHash,region.Target.SourceHash);Assert.Equal(plan.GeneratedBodyHash,region.Target.GeneratedBodyHash);
        Assert.Equal(plan.CallSite!.Start,region.Target.CallSite.Start);Assert.Equal(plan.CallSite.Path,region.Target.CallSite.Path);
        Assert.Contains(plan.Nodes,node=>node.Id==region.Target.NodeId&&node.Kind=="Fragment");Assert.Equal("FusedPipelineInclusive",region.Scope);
        Assert.True(region.InputCountKnown);Assert.Equal(2,region.InputCount);Assert.True(region.OutputCountKnown);Assert.Equal(1,region.OutputCount);Assert.True(region.Complete);
        Assert.True(region.EndTimestamp>=region.StartTimestamp);Assert.Equal(System.Diagnostics.Stopwatch.Frequency,snapshot.StopwatchFrequency);
        Assert.Contains(QueryProfileDiagnostic.WorkerNotCollected,snapshot.Diagnostics);Assert.Contains(QueryProfileDiagnostic.HelperBodiesNotExpanded,snapshot.Diagnostics);
        var normal=program.Methods.Single(method=>method.Identifier.ValueText.EndsWith("__Normal",StringComparison.Ordinal));
        var entry=program.Methods.Single(method=>method.AttributeLists.SelectMany(list=>list.Attributes).Any(attribute=>attribute.Name.ToString().Contains("InterceptsLocation",StringComparison.Ordinal)));
        Assert.DoesNotContain("QueryProfiler",normal.Body!.ToFullString());Assert.DoesNotContain("QueryProfileRegion",normal.Body.ToFullString());
        Assert.Single(entry.Body!.DescendantNodes().OfType<MemberAccessExpressionSyntax>(),access=>access.Name.Identifier.ValueText=="Current");
        Assert.Contains("__Normal",plan.GeneratedCode!.MethodKey);Assert.Equal("Available",plan.GeneratedCode.Status);
        Assert.Equal("Available",plan.ProfiledGeneratedCode!.Status);Assert.Contains("__Profiled",plan.ProfiledGeneratedCode.MethodKey);Assert.NotEqual(plan.GeneratedCode.BodyHash,plan.ProfiledGeneratedCode.BodyHash);
    }
    [Fact]public void EmptyInputAndFailureKeepTheOriginalResultExceptionAndActualPrefixCounts(){
        using var program=Generate();Assert.Equal(0,program.Invoke([]));
        using(var empty=Profiler.Begin()){
            Assert.Equal(0,program.Invoke([]));var region=Assert.Single(empty.Complete().Regions);Assert.True(region.InputCountKnown);Assert.Equal(0,region.InputCount);Assert.Equal(1,region.OutputCount);
        }
        using var throwing=Generate(enumerable:true);var cause=new InvalidOperationException("expected");var input=new FailingEnumerable(cause);
        var expected=Assert.Throws<TargetInvocationException>(()=>throwing.InvokeEnumerable(input)).InnerException;
        using var failed=Profiler.Begin();var actual=Assert.Throws<TargetInvocationException>(()=>throwing.InvokeEnumerable(input)).InnerException;
        Assert.Same(cause,expected);Assert.Same(cause,actual);Assert.Equal(2,input.Disposals);var observation=Assert.Single(failed.Complete().Regions);
        Assert.Equal("Failed",observation.Outcome);Assert.False(observation.Complete);Assert.Equal(0,observation.OutputCount);
    }
    [Fact]public void ActiveRegionsAtCompleteRemainPartialAndTheFrozenSnapshotCannotGainLateCounts(){
        using var profiler=Profiler.Begin();using var region=profiler.Enter(Target(),true);region.Input();
        var snapshot=profiler.Complete();Assert.False(snapshot.Complete);Assert.False(Assert.Single(snapshot.Regions).Complete);Assert.Contains(QueryProfileDiagnostic.ActiveRegionsAtComplete,snapshot.Diagnostics);
        region.Input();region.Output();region.Succeeded();region.Dispose();Assert.Same(snapshot,profiler.Complete());Assert.Equal(1,snapshot.Regions[0].InputCount);Assert.Equal(0,snapshot.Regions[0].OutputCount);Assert.Null(Profiler.Current);
    }
    [Fact]public void CancellationKeepsTheOriginalExceptionAndTheObservedOutcome(){
        using var program=Generate(enumerable:true);var cause=new OperationCanceledException();var input=new FailingEnumerable(cause);
        Assert.Same(cause,Assert.Throws<TargetInvocationException>(()=>program.InvokeEnumerable(input)).InnerException);
        using var profiler=Profiler.Begin();Assert.Same(cause,Assert.Throws<TargetInvocationException>(()=>program.InvokeEnumerable(input)).InnerException);Assert.Equal(2,input.Disposals);
        var region=Assert.Single(profiler.Complete().Regions);Assert.Equal("Cancelled",region.Outcome);Assert.False(region.Complete);Assert.Equal(0,region.OutputCount);
    }
    [Fact]public void UnsupportedProceduralShapeReportsTheMissingMeasurementWithoutChangingItsResult(){
        using var program=Generate(procedural:true);using var profiler=Profiler.Begin();Assert.Equal(5,program.Invoke([1,2,3]));
        var snapshot=profiler.Complete();Assert.Empty(snapshot.Regions);Assert.Contains(QueryProfileDiagnostic.UnsupportedQueryShape,snapshot.Diagnostics);
    }
    [Fact]public async Task NestedScopesRestoreTheirParentAndEscapedChildContextsCannotRecordAfterClosure(){
        var budget=new ResourceBudget(1<<20,0);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);Task<Profiler?> child;
        using(var outer=Profiler.Begin(budget,maximumRegions:2)){
            using(var inner=Profiler.Begin(budget,maximumRegions:2)){Assert.Same(inner,Profiler.Current);Assert.Empty(inner.Complete().Regions);}
            Assert.Same(outer,Profiler.Current);child=Task.Run(async()=>{await release.Task;return Profiler.Current;});_=outer.Complete();
        }
        release.SetResult();Assert.Null(await child);Assert.Null(Profiler.Current);Assert.Equal(0,budget.MemoryBytes);
    }
    [Fact]public void RegionLimitAndReservationFailureStayBoundedAndReleaseTheirBudget(){
        var budget=new ResourceBudget(1<<20,0);
        using(var profiler=Profiler.Begin(budget,maximumRegions:1)){
            using(var region=profiler.Enter(Target(),true)){region.Output();region.Succeeded();}
            using(var dropped=profiler.Enter(Target(),true)){dropped.Input();dropped.Succeeded();}
            var snapshot=profiler.Complete();Assert.Single(snapshot.Regions);Assert.Equal(1,snapshot.DroppedRegions);Assert.False(snapshot.Complete);Assert.Contains(QueryProfileDiagnostic.RegionLimit,snapshot.Diagnostics);
        }
        Assert.Equal(0,budget.MemoryBytes);var small=new ResourceBudget(1,0);Assert.Throws<ResourceLimitException>(()=>Profiler.Begin(small));Assert.Equal(0,small.MemoryBytes);Assert.Null(Profiler.Current);
    }
    [Theory][InlineData("Array")][InlineData("List")][InlineData("Opaque")]
    public void NullableSumCountsEachConsumerValueAcrossSpanAndEnumeratorBranches(string storage){
        const string source="using System.Linq;using Seamless.FLinq;public sealed class ProfileQuery{[LinqIntercept(true)] private static decimal? Compute(System.Collections.Generic.IEnumerable<decimal?> values)=>values.Sum();public static decimal? Run(System.Collections.Generic.IEnumerable<decimal?> values)=>Compute(values);}";
        using var program=Generate(sourceOverride:source);AssertOptionalSpanBranches(program);decimal?[] values=[1m,null,4m];IEnumerable<decimal?> input=values;
        if(storage=="List"){input=values.ToList();}else if(storage=="Opaque"){input=Opaque(values);}
        var expected=program.InvokeArgument(input);
        using var profiler=Profiler.Begin();Assert.Equal(expected,program.InvokeArgument(input));var region=Assert.Single(profiler.Complete().Regions);
        Assert.True(region.InputCountKnown,ProfileBodies(program));Assert.Equal(3,region.InputCount);Assert.Equal(1,region.OutputCount);Assert.True(region.Complete);
    }
    [Theory][InlineData("Array")][InlineData("List")][InlineData("Opaque")]
    public void FloatSumKeepsItsNormalResultAndConsumerCountAcrossTheOptionalSpanBranch(string storage){
        const string source="using System.Linq;using Seamless.FLinq;public sealed class ProfileQuery{[LinqIntercept(true)] private static float Compute(System.Collections.Generic.IEnumerable<float> values)=>values.Sum();public static float Run(System.Collections.Generic.IEnumerable<float> values)=>Compute(values);}";
        using var program=Generate(sourceOverride:source);AssertOptionalSpanBranches(program);float[] values=[1,2,4];IEnumerable<float> input=values;
        if(storage=="List"){input=values.ToList();}else if(storage=="Opaque"){input=OpaqueFloats(values);}
        var expected=input.Sum();Assert.Equal(expected,program.InvokeArgument(input));using var profiler=Profiler.Begin();Assert.Equal(expected,program.InvokeArgument(input));var region=Assert.Single(profiler.Complete().Regions);
        Assert.True(region.InputCountKnown,ProfileBodies(program));Assert.Equal(3,region.InputCount);Assert.Equal(1,region.OutputCount);Assert.True(region.Complete);
    }
    [Fact]public void UninstrumentedBulkSumNeverReportsAKnownZeroInputCount(){
        const string source="using System.Linq;using Seamless.FLinq;public sealed class ProfileQuery{[LinqIntercept(true)] private static int Compute(int[] values)=>values.Sum();public static int Run(int[] values)=>Compute(values);}";
        using var program=Generate(sourceOverride:source);Assert.Equal(6,program.Invoke([1,2,3]));using var profiler=Profiler.Begin();Assert.Equal(6,program.Invoke([1,2,3]));var snapshot=profiler.Complete();var region=Assert.Single(snapshot.Regions);Assert.False(region.InputCountKnown);Assert.Contains(QueryProfileDiagnostic.InputCountNotCollected,snapshot.Diagnostics);Assert.Equal(1,region.OutputCount);
    }
    [Fact]public void SimilarlyNamedUserDelegateRetainsItsInvocationAndOnlyTheOuterConsumerIsCounted(){
        const string source="using System;using System.Linq;using Seamless.FLinq;public sealed class ProfileQuery{[LinqIntercept(true)] private static int Compute(int[] values,Func<int,int> __computeUser)=>values.Where(value=>__computeUser(value)>1).Sum();public static int Run(int[] values)=>Compute(values,static value=>value);}";
        using var program=Generate(sourceOverride:source);Assert.Equal(5,program.Invoke([1,2,3]));using var profiler=Profiler.Begin();Assert.Equal(5,program.Invoke([1,2,3]));var region=Assert.Single(profiler.Complete().Regions);Assert.True(region.InputCountKnown);Assert.Equal(2,region.InputCount);Assert.Equal(1,region.OutputCount);
    }
    [Fact]public void AFailingCurrentGetterDoesNotCountAValueBeforeItWasReceived(){
        const string source="using System.Linq;using Seamless.FLinq;public sealed class ProfileQuery{[LinqIntercept(true)] private static float Compute(System.Collections.Generic.IEnumerable<float> values)=>values.Sum();public static float Run(System.Collections.Generic.IEnumerable<float> values)=>Compute(values);}";
        using var program=Generate(sourceOverride:source);AssertOptionalSpanBranches(program);var cause=new InvalidOperationException("Current failed");var input=new FailingCurrentEnumerable(cause);
        Assert.Same(cause,Assert.Throws<TargetInvocationException>(()=>program.InvokeArgument(input)).InnerException);
        using var profiler=Profiler.Begin();Assert.Same(cause,Assert.Throws<TargetInvocationException>(()=>program.InvokeArgument(input)).InnerException);Assert.Equal(2,input.Disposals);
        var region=Assert.Single(profiler.Complete().Regions);Assert.True(region.InputCountKnown);Assert.Equal(0,region.InputCount);Assert.Equal(0,region.OutputCount);Assert.Equal("Failed",region.Outcome);Assert.False(region.Complete);
    }
    [Fact]public void ActualRenamedRootComputeAndAccumulatorStayBoundWhenAUserParameterOccupiesTheirPreferredName(){
        const string source="using System.Linq;using Seamless.FLinq;public sealed class ProfileQuery{[LinqIntercept(true)] private static float Compute(System.Collections.Generic.IEnumerable<float> values,int compute)=>values.Sum()+compute;public static float Run(System.Collections.Generic.IEnumerable<float> values)=>Compute(values,0);}";
        using var program=Generate(sourceOverride:source);float[] input=[1,2,4];Assert.Equal(7f,program.InvokeArgument(input));using var profiler=Profiler.Begin();Assert.Equal(7f,program.InvokeArgument(input));var region=Assert.Single(profiler.Complete().Regions);Assert.True(region.InputCountKnown,ProfileBodies(program));Assert.Equal(3,region.InputCount);Assert.Equal(1,region.OutputCount);
    }
    private static IEnumerable<decimal?> Opaque(IEnumerable<decimal?> values){foreach(var value in values){yield return value;}}
    private static IEnumerable<float> OpaqueFloats(IEnumerable<float> values){foreach(var value in values){yield return value;}}
    private static void AssertOptionalSpanBranches(Program program){var normal=program.Methods.Single(method=>method.Identifier.ValueText.EndsWith("__Normal",StringComparison.Ordinal));Assert.Contains("TryGetSpan",normal.Body!.ToFullString());Assert.NotEmpty(normal.Body.DescendantNodes().OfType<ForStatementSyntax>());Assert.NotEmpty(normal.Body.DescendantNodes().OfType<WhileStatementSyntax>());}
    private static string ProfileBodies(Program program)=>string.Join("\n",program.Methods.Where(method=>method.Identifier.ValueText.EndsWith("__Normal",StringComparison.Ordinal)||method.Identifier.ValueText.EndsWith("__Profiled",StringComparison.Ordinal)).Select(method=>method.Identifier.ValueText+"\n"+method.Body!.ToFullString()));
    private static QueryProfileTarget Target()=>new("Query.Compute(int[])",new('B',64),new('C',64),new("Query.cs",0,1,"Query.Run()"),"fragment0");
    private static Program Generate(string predicate="value=>value>1",bool procedural=false,bool enumerable=false,string? sourceOverride=null){
        var options=new CSharpParseOptions(LanguageVersion.CSharp14).WithFeatures([new("InterceptorsNamespaces","Intercept"),new("InterceptorsPreviewNamespaces","Intercept")]);
        var body="=>values.Where("+predicate+").Sum();";
        if(procedural){body="{if(values.Length==0){return 0;}return values.Where("+predicate+").Sum();}";}
        var inputType="int[]";if(enumerable){inputType="System.Collections.Generic.IEnumerable<int>";}
        var source="using System.Linq;using Seamless.FLinq;public sealed class ProfileQuery{[LinqIntercept(true)] private static int Compute("+inputType+" values)"+body+" public static int Run("+inputType+" values)=>Compute(values);}";
        if(sourceOverride is not null){source=sourceOverride;}
        var references=global::Seamless.Management.SourceEditor.PlatformReferences().ToList();
        foreach(var assembly in new[]{typeof(Profiler).Assembly,typeof(global::Seamless.Linq.Enumerable).Assembly}){if(!references.Any(reference=>reference.Display==assembly.Location)){references.Add(MetadataReference.CreateFromFile(assembly.Location));}}
        var compilation=CSharpCompilation.Create("ProfileQuery_"+Guid.NewGuid().ToString("N"),[CSharpSyntaxTree.ParseText(source,options,"profile-query.cs")],references,new(OutputKind.DynamicallyLinkedLibrary,allowUnsafe:true,optimizationLevel:OptimizationLevel.Release));
        GeneratorDriver driver=CSharpGeneratorDriver.Create([new global::Seamless.FLinq.Generator.FLinqIncrementalGenerator().AsSourceGenerator()],parseOptions:options);
        driver.RunGeneratorsAndUpdateCompilation(compilation,out var output,out var diagnostics);Assert.DoesNotContain(diagnostics,item=>item.Severity==DiagnosticSeverity.Error||item.Id=="FLINQ002");
        using var image=new MemoryStream();var result=output.Emit(image);Assert.True(result.Success,string.Join("\n",result.Diagnostics));image.Position=0;
        return new(image.ToArray(),output,PlanDocument.Read(image));
    }
    private sealed class Program:IDisposable{
        private readonly AssemblyLoadContext context=new("ProfileQuery",true);
        private readonly Assembly assembly;
        internal MethodDeclarationSyntax[] Methods{get;}
        internal PlanDocument[] Plans{get;}
        internal Program(byte[] image,Compilation compilation,PlanDocument[] plans){using var stream=new MemoryStream(image);this.assembly=this.context.LoadFromStream(stream);this.Plans=plans;this.Methods=compilation.SyntaxTrees.SelectMany(tree=>tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()).ToArray();}
        internal object? Invoke(int[] input)=>this.assembly.GetType("ProfileQuery",true)!.GetMethod("Run")!.Invoke(null,[input]);
        internal object? InvokeEnumerable(IEnumerable<int> input)=>this.assembly.GetType("ProfileQuery",true)!.GetMethod("Run")!.Invoke(null,[input]);
        internal object? InvokeArgument(object input)=>this.assembly.GetType("ProfileQuery",true)!.GetMethod("Run")!.Invoke(null,[input]);
        public void Dispose()=>this.context.Unload();
    }
    private sealed class FailingEnumerable(Exception error):IEnumerable<int>{
        internal int Disposals{get;private set;}
        public IEnumerator<int> GetEnumerator(){try{yield return 1;throw error;}finally{this.Disposals++;}}
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>this.GetEnumerator();
    }
    private sealed class FailingCurrentEnumerable(Exception error):IEnumerable<float>{
        internal int Disposals{get;private set;}
        public IEnumerator<float> GetEnumerator()=>new Cursor(this,error);
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>this.GetEnumerator();
        private sealed class Cursor(FailingCurrentEnumerable owner,Exception error):IEnumerator<float>{
            public float Current=>throw error;
            object System.Collections.IEnumerator.Current=>this.Current;
            public bool MoveNext()=>true;
            public void Reset()=>throw new NotSupportedException();
            public void Dispose()=>owner.Disposals++;
        }
    }
}
