using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Seamless.Management;
using Designer=Seamless.Management.DatabaseDesigner;

namespace Management.Tests;

public sealed partial class DatabaseDesigner{
    [Fact]
    public void ImplicitReturnConversionKeepsDeclaredTypeAndOriginalEffects(){
        var definition=FunctionBoundaryDefinition("global::UserCode.Converted","return new global::UserCode.Input(id);","""
            namespace UserCode{
             public static class Effects{public static int Count;public static bool Fail;}
             public readonly record struct Converted(int Value);
             public readonly record struct Input(int Value){
              public static implicit operator Converted(Input value){Effects.Count++;if(Effects.Fail){throw new System.InvalidOperationException("conversion");}return new(value.Value);}
             }
            }
            """);
        var target=Designer.Objects(definition).Single(item=>item.Name=="Amount");
        var baseline=Designer.ReadObjectDefinition(definition,target);
        var part=baseline.Parts.Single(item=>item.Role=="Member");
        var replacement=part.Text.Replace("new global::UserCode.Input(id)","new global::UserCode.Input(id+1)",StringComparison.Ordinal);
        Assert.NotEqual(part.Text,replacement);
        var edit=Designer.ApplyObjectSource(definition,baseline,part.Id,replacement);
        Assert.True(edit.Succeeded,string.Join("\n",edit.Diagnostics.Select(item=>item.Message)));
        var changed=edit.Definition!;
        Assert.Equal("global::UserCode.Converted",Designer.ReadQuery(changed,"Amount").ReturnType);
        Assert.False(string.IsNullOrWhiteSpace(Designer.ReadObjectDefinition(changed,target).ResultTypeInferenceReason));
        using var result=RunFunctionBoundary(changed,"""
            namespace UserCode{public static class BoundaryRun{
             public static string Run(){using var db=new ManagedDb(global::Seamless.Database.DatabaseContainerOptions.InMemory);
              var value=db.Amount(4);var immediate=Effects.Count;var number=value.Value.Value;Effects.Fail=true;string? error=null;
              try{_=db.Amount(4);}catch(System.InvalidOperationException exception){error=exception.Message;}
              return System.Text.Json.JsonSerializer.Serialize(new{number,immediate,total=Effects.Count,error});
             }
            }}
            """);
        Assert.Equal(5,result.RootElement.GetProperty("number").GetInt32());
        Assert.Equal(1,result.RootElement.GetProperty("immediate").GetInt32());
        Assert.Equal(2,result.RootElement.GetProperty("total").GetInt32());
        Assert.Equal("conversion",result.RootElement.GetProperty("error").GetString());
    }

    [Theory]
    [InlineData("int[]")]
    [InlineData("object")]
    public void UnchangedScalarSequenceKeepsImmediateEvaluation(string declaredType){
        var definition=FunctionBoundaryDefinition(declaredType,"return global::UserCode.Effects.Sequence(id);","""
            namespace UserCode{public static class Effects{
             public static int Count;public static bool Fail;
             public static int[] Sequence(int id){Count++;if(Fail){throw new System.InvalidOperationException("sequence");}return new[]{id,id+1};}
            }}
            """);
        var target=Designer.Objects(definition).Single(item=>item.Name=="Amount");
        var baseline=Designer.ReadObjectDefinition(definition,target);
        var part=baseline.Parts.Single(item=>item.Role=="Member");
        var unchanged=Designer.ApplyObjectSource(definition,baseline,part.Id,part.Text);
        Assert.True(unchanged.Succeeded,string.Join("\n",unchanged.Diagnostics.Select(item=>item.Message)));
        Assert.Equal(definition.Source.Files,unchanged.Definition!.Source.Files);
        var replacement=part.Text.Replace("return global::UserCode.Effects.Sequence(id);","/* keep immediate execution */return global::UserCode.Effects.Sequence(id);",StringComparison.Ordinal);
        Assert.NotEqual(part.Text,replacement);
        var edit=Designer.ApplyObjectSource(definition,baseline,part.Id,replacement);
        Assert.True(edit.Succeeded,string.Join("\n",edit.Diagnostics.Select(item=>item.Message)));
        var changed=edit.Definition!;
        var query=Designer.ReadQuery(changed,"Amount");
        Assert.Equal(DatabaseQueryKind.ScalarFunction,query.Kind);
        Assert.Equal(declaredType,query.ReturnType);
        using var result=RunFunctionBoundary(changed,"""
            namespace UserCode{public static class BoundaryRun{
             public static string Run(){using var db=new ManagedDb(global::Seamless.Database.DatabaseContainerOptions.InMemory);
              var wrapped=db.Amount(7);var immediate=Effects.Count;var values=(int[])wrapped.Value;Effects.Fail=true;string? error=null;
              try{_=db.Amount(7);}catch(System.InvalidOperationException exception){error=exception.Message;}
              return System.Text.Json.JsonSerializer.Serialize(new{values,immediate,total=Effects.Count,error});
             }
            }}
            """);
        Assert.Equal(new[]{7,8},result.RootElement.GetProperty("values").EnumerateArray().Select(item=>item.GetInt32()));
        Assert.Equal(1,result.RootElement.GetProperty("immediate").GetInt32());
        Assert.Equal(2,result.RootElement.GetProperty("total").GetInt32());
        Assert.Equal("sequence",result.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public void FactorySynchronizationDoesNotBindToCustomShadow(){
        var definition=FunctionBoundaryDefinition("int","return id;","""
            namespace UserCode{public sealed partial class ManagedDb{
             protected new global::Seamless.Database.TableFunction<T> TableFunction<T>(System.Func<System.Collections.Generic.IEnumerable<T>> definition,string name=""){
              throw new System.InvalidOperationException("custom shadow");
             }
            }}
            """);
        var target=Designer.Objects(definition).Single(item=>item.Name=="Amount");
        var baseline=Designer.ReadObjectDefinition(definition,target);
        var part=baseline.Parts.Single(item=>item.Role=="Member");
        var replacement=part.Text.Replace("return id;","return new[]{id,id+1};",StringComparison.Ordinal);
        var edit=Designer.ApplyObjectSource(definition,baseline,part.Id,replacement);
        if(!edit.Succeeded){
            Assert.NotEmpty(edit.Diagnostics);
            Assert.Contains("return new[]{id,id+1};",replacement,StringComparison.Ordinal);
            return;
        }
        var changed=edit.Definition!;
        var current=Designer.ReadObjectDefinition(changed,target);
        Assert.False(string.IsNullOrWhiteSpace(current.ResultTypeInferenceReason));
        var member=current.Parts.Single(item=>item.Role=="Member").Text;
        Assert.Contains("ScalarFunction<int>",member,StringComparison.Ordinal);
        Assert.Contains("return new[]{id,id+1};",member,StringComparison.Ordinal);
        Assert.DoesNotContain("this.TableFunction",member,StringComparison.Ordinal);
    }

    [Fact]
    public void NestedFunctionWrapperResultDoesNotConfuseOuterType(){
        var definition=FunctionBoundaryDefinition("global::Seamless.Database.ScalarFunction<int>","return this.ScalarFunction<int>(()=>id);","");
        var target=Designer.Objects(definition).Single(item=>item.Name=="Amount");
        var baseline=Designer.ReadObjectDefinition(definition,target);
        var part=baseline.Parts.Single(item=>item.Role=="Member");
        var replacement=part.Text.Replace("()=>id)","()=>id+1)",StringComparison.Ordinal);
        Assert.NotEqual(part.Text,replacement);
        var edit=Designer.ApplyObjectSource(definition,baseline,part.Id,replacement);
        Assert.True(edit.Succeeded,string.Join("\n",edit.Diagnostics.Select(item=>item.Message)));
        var changed=edit.Definition!;
        Assert.Equal("global::Seamless.Database.ScalarFunction<int>",Designer.ReadQuery(changed,"Amount").ReturnType);
        using var result=RunFunctionBoundary(changed,"""
            namespace UserCode{public static class BoundaryRun{
             public static string Run(){using var db=new ManagedDb(global::Seamless.Database.DatabaseContainerOptions.InMemory);return System.Text.Json.JsonSerializer.Serialize(db.Amount(41).Value.Value);}
            }}
            """);
        Assert.Equal(42,result.RootElement.GetInt32());
    }

    private static Definition FunctionBoundaryDefinition(string type,string body,string support){
        var definition=FunctionResultFixture(false);
        var files=definition.Source.Files.Select(file=>{
            if(file.Path=="Database.cs"){
                return file with{Text=file.Text.Replace("ScalarFunction<int>","ScalarFunction<"+type+">",StringComparison.Ordinal).Replace("return id;",body,StringComparison.Ordinal)};
            }
            return file;
        }).ToList();
        if(support.Length>0){files.Add(new("FunctionBoundarySupport.cs",support));}
        var result=definition with{Source=definition.Source with{Files=files.ToArray()}};
        AssertFunctionResultCompilation(result);
        return result;
    }

    private static JsonDocument RunFunctionBoundary(Definition definition,string runner){
        var source=definition.Source with{Files=[..definition.Source.Files,new("FunctionBoundaryRun.cs",runner)]};
        var options=new CSharpParseOptions(LanguageVersion.CSharp14,preprocessorSymbols:source.Defines??[]);
        var compilation=CSharpCompilation.Create("FunctionBoundary_"+Guid.NewGuid().ToString("N"),source.Files.Select(file=>CSharpSyntaxTree.ParseText(file.Text,options,file.Path)),global::Seamless.Management.SourceEditor.PlatformReferences(),new(OutputKind.DynamicallyLinkedLibrary));
        using var image=new MemoryStream();
        var emitted=compilation.Emit(image);
        Assert.True(emitted.Success,string.Join("\n",emitted.Diagnostics));
        var load=new AssemblyLoadContext("FunctionBoundary_"+Guid.NewGuid().ToString("N"),isCollectible:true);
        load.Resolving+=static(_,name)=>AssemblyLoadContext.Default.LoadFromAssemblyName(name);
        try{
            image.Position=0;
            var assembly=load.LoadFromStream(image);
            var method=assembly.GetType("UserCode.BoundaryRun",throwOnError:true)!.GetMethod("Run",BindingFlags.Public|BindingFlags.Static)!;
            return JsonDocument.Parse(Assert.IsType<string>(method.Invoke(null,null)));
        }finally{load.Unload();}
    }
}
