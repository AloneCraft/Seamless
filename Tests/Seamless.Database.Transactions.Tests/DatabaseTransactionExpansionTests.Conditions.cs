using System.Runtime.Loader;

namespace Seamless.Database.Transactions.Tests;

public sealed partial class DatabaseTransactionExpansionTests{
    [Fact]
    public void ConditionTerminalsKeepShortCircuitCallbackAndExceptionOrder(){
        const string methods="""
            public int And(int[] source,List<string> trace,bool enabled,bool fail){
                if(Effects.Gate(trace,enabled)&&source.Sum(value=>Effects.Item(trace,value,fail))>0){trace.Add("then");return 1;}
                else{trace.Add("else");return 0;}
            }
            public int Or(int[] source,List<string> trace,bool enabled,bool fail){
                if(Effects.Gate(trace,enabled)||source.Sum(value=>Effects.Item(trace,value,fail))>0){trace.Add("then");return 1;}
                else{trace.Add("else");return 0;}
            }
            public int Conditional(int[] source,List<string> trace,bool enabled,bool fail){
                if(enabled?source.Sum(value=>Effects.Item(trace,value,fail))>0:Effects.Gate(trace,false)){trace.Add("then");return 1;}
                else{trace.Add("else");return 0;}
            }
            public int Duplicate(int[] source,List<string> trace,bool enabled,bool fail){
                if(source.Sum(value=>Effects.Item(trace,value,fail))>0&&source.Sum(value=>Effects.Item(trace,value,fail))>0){trace.Add("then");return 1;}
                else{trace.Add("else");return 0;}
            }
            public int Before(int[] source,List<string> trace,bool enabled,bool fail){
                if(Effects.Before(trace,fail)>source.Sum(value=>Effects.Item(trace,value,false))){trace.Add("then");return 1;}
                else{trace.Add("else");return 0;}
            }
            public int After(int[] source,List<string> trace,bool enabled,bool fail){
                if(source.Sum(value=>Effects.Item(trace,value,false))>Effects.Before(trace,fail)){trace.Add("then");return 1;}
                else{trace.Add("else");return 0;}
            }
            public int ElseIf(int[] source,List<string> trace,bool enabled,bool fail){
                if(Effects.Gate(trace,enabled)){trace.Add("first");return 2;}
                else if(source.Sum(value=>Effects.Item(trace,value,fail))>0){trace.Add("second");return 1;}
                else{trace.Add("last");return 0;}
            }
            public int Count(int[] source,List<string> trace,bool enabled,bool fail){
                if(source.Count(value=>Effects.Item(trace,value,fail)>0)>0){trace.Add("then");return 1;}
                else{trace.Add("else");return 0;}
            }
            public int Nullable(IEnumerable<int>? source,List<string> trace,bool enabled,bool fail){
                if(source!=null&&source.Sum(value=>Effects.Item(trace,value,fail))>0){trace.Add("then");return 1;}
                else{trace.Add("else");return 0;}
            }
            """;
        var source="""
            #nullable enable
            using System;
            using System.Linq;
            using System.Collections.Generic;
            using Seamless.FLinq;
            namespace ConditionEvaluationCase;
            public static class Effects{
                public static int Item(List<string> trace,int value,bool fail){
                    trace.Add("item"+value);if(fail&&value==2){throw new InvalidOperationException("item2");}return value;
                }
                public static bool Gate(List<string> trace,bool enabled){trace.Add("gate"+enabled);return enabled;}
                public static int Before(List<string> trace,bool fail){
                    trace.Add("before");if(fail){throw new InvalidOperationException("before");}return 0;
                }
            }
            [LinqIntercept(true)]public sealed class Reader{
            """+methods+"}"+"[LinqIntercept(false)]public sealed class Reference{"+methods+"}"+"""
            public static class Entry{
                private static string Observe(Func<int> action,List<string> trace){
                    try{var value=action();return "value"+value+"/"+string.Join(",",trace);}
                    catch(InvalidOperationException error){return "error"+error.Message+"/"+string.Join(",",trace);}
                }
                private static void Check(Func<List<string>,int> actual,Func<List<string>,int> expected){
                    var a=new List<string>();var b=new List<string>();
                    var observed=Observe(()=>actual(a),a);var reference=Observe(()=>expected(b),b);
                    if(observed!=reference){throw new InvalidOperationException(observed+" != "+reference);}
                }
                public static int Run(){
                    var reader=new Reader();var reference=new Reference();var cases=0;
                    foreach(var values in new[]{new[]{1,2,3},Array.Empty<int>()}){
                        foreach(var enabled in new[]{false,true}){
                            foreach(var fail in new[]{false,true}){
                                Check(trace=>reader.And(values,trace,enabled,fail),trace=>reference.And(values,trace,enabled,fail));
                                Check(trace=>reader.Or(values,trace,enabled,fail),trace=>reference.Or(values,trace,enabled,fail));
                                Check(trace=>reader.Conditional(values,trace,enabled,fail),trace=>reference.Conditional(values,trace,enabled,fail));
                                Check(trace=>reader.Duplicate(values,trace,enabled,fail),trace=>reference.Duplicate(values,trace,enabled,fail));
                                Check(trace=>reader.Before(values,trace,enabled,fail),trace=>reference.Before(values,trace,enabled,fail));
                                Check(trace=>reader.After(values,trace,enabled,fail),trace=>reference.After(values,trace,enabled,fail));
                                Check(trace=>reader.ElseIf(values,trace,enabled,fail),trace=>reference.ElseIf(values,trace,enabled,fail));
                                Check(trace=>reader.Count(values,trace,enabled,fail),trace=>reference.Count(values,trace,enabled,fail));
                                Check(trace=>reader.Nullable(values,trace,enabled,fail),trace=>reference.Nullable(values,trace,enabled,fail));
                                cases+=9;
                            }
                        }
                    }
                    Check(trace=>reader.Nullable(null,trace,true,true),trace=>reference.Nullable(null,trace,true,true));
                    return cases+1;
                }
            }
            """;
        var(output,generated)=Generate(source,"ConditionEvaluationCase");
        Assert.DoesNotContain(generated,static text=>text.Contains("UnsupportedLinqExpansion",StringComparison.Ordinal));
        Assert.DoesNotContain(output.GetDiagnostics(),static diagnostic=>diagnostic.Severity==Microsoft.CodeAnalysis.DiagnosticSeverity.Warning&&diagnostic.Id.StartsWith("CS86",StringComparison.Ordinal));
        using var image=new MemoryStream();
        var emitted=output.Emit(image);
        Assert.True(emitted.Success,string.Join(Environment.NewLine,emitted.Diagnostics));
        image.Position=0;
        var load=new AssemblyLoadContext("condition-evaluation-"+Guid.NewGuid(),isCollectible:true);
        load.Resolving+=static(_,name)=>AssemblyLoadContext.Default.LoadFromAssemblyName(name);
        try{
            var run=load.LoadFromStream(image).GetType("ConditionEvaluationCase.Entry",true)!.GetMethod("Run")!;
            Assert.Equal(73,run.Invoke(null,null));
        }finally{load.Unload();}
    }

    [Theory]
    [InlineData("checked","IfConditionExplicitOverflowContextNotSupported")]
    [InlineData("ref-like-generic","IfConditionRefLikeTypeParameterCaptureNotSupported")]
    public void UnprovenConditionContextsExplainWhyTheyCannotMove(string shape,string reason){
        var members="""
            public static bool Call(int value)=>Query(value);
            [LinqIntercept(true)]public static bool Query(int value){if(checked(new[]{int.MaxValue+value}.Sum())>0){return true;}return false;}
            """;
        if(shape=="ref-like-generic"){
            members="""
                public static bool Call<T>(T source)where T:IValues,allows ref struct=>Query(source);
                [LinqIntercept(true)]public static bool Query<T>(T source)where T:IValues,allows ref struct{if(source.Values().Sum()>0){return true;}return false;}
                """;
        }
        var source="""
            #nullable enable
            using System;
            using System.Linq;
            using System.Collections.Generic;
            using Seamless.FLinq;
            namespace ConditionGuardCase;
            public interface IValues{int[] Values();}
            public sealed class Entry{
            """+members+"}";
        var(output,generated)=Generate(source,"ConditionGuardCase");
        Assert.Contains(generated,text=>text.Contains(reason,StringComparison.Ordinal));
        using var image=new MemoryStream();
        var emitted=output.Emit(image);
        Assert.True(emitted.Success,string.Join(Environment.NewLine,emitted.Diagnostics));
    }

    [Fact]
    public void MixedCheckedConditionKeepsItsExistingStatementExpansion(){
        const string body="""
            public int Query(int[] values,int[] other,int adjustment,List<int> trace){
                if(checked(values.Sum(value=>Effects.Note(trace,value))+adjustment)>0){trace.Add(-1);}
                return other.Count();
            }
            """;
        var source="""
            #nullable enable
            using System;
            using System.Linq;
            using System.Collections.Generic;
            using Seamless.FLinq;
            namespace MixedConditionCase;
            public static class Effects{public static int Note(List<int> trace,int value){trace.Add(value);return value;}}
            [LinqIntercept(true)]public sealed class Reader{
            """+body+"}[LinqIntercept(false)]public sealed class Reference{"+body+"}"+"""
            public static class Entry{
                private static string Observe(Func<int> action,List<int> trace){
                    try{var value=action();return "value"+value+"/"+string.Join(",",trace);}
                    catch(OverflowException){return "overflow/"+string.Join(",",trace);}
                }
                public static int Run(){
                    var reader=new Reader();var reference=new Reference();var cases=0;
                    foreach(var values in new[]{new[]{1,2},new[]{int.MaxValue},Array.Empty<int>()}){
                        foreach(var adjustment in new[]{0,1}){
                            var a=new List<int>();var b=new List<int>();var other=new[]{7,8};
                            var actual=Observe(()=>reader.Query(values,other,adjustment,a),a);
                            var expected=Observe(()=>reference.Query(values,other,adjustment,b),b);
                            if(actual!=expected){throw new InvalidOperationException(actual+" != "+expected);}
                            cases++;
                        }
                    }
                    return cases;
                }
            }
            """;
        var evidence=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,"mixed-condition.g.cs");
        var(output,generated)=Generate(source,"MixedConditionCase",generatedEvidencePath:evidence);
        Assert.DoesNotContain(generated,static text=>text.Contains("UnsupportedLinqExpansion",StringComparison.Ordinal));
        using var image=new MemoryStream();
        var emitted=output.Emit(image);
        Assert.True(emitted.Success,string.Join(Environment.NewLine,emitted.Diagnostics));
        image.Position=0;
        var load=new AssemblyLoadContext("mixed-condition-"+Guid.NewGuid(),isCollectible:true);
        load.Resolving+=static(_,name)=>AssemblyLoadContext.Default.LoadFromAssemblyName(name);
        try{
            var run=load.LoadFromStream(image).GetType("MixedConditionCase.Entry",true)!.GetMethod("Run")!;
            Assert.Equal(6,run.Invoke(null,null));
        }finally{load.Unload();}
    }
}
