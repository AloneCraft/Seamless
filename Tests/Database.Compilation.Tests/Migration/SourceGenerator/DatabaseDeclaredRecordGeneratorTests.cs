using Microsoft.CodeAnalysis;

namespace Test.SourceGenerator;

public sealed class DatabaseDeclaredRecordGeneratorTests{
    [Theory,Trait("Category","Unit")]
    [InlineData("")]
    [InlineData(".dbo")]
    public void ExplicitKey_IsStoredOnceAndCopyUpdatesRemainConsistent(string schema){
        var source=$$"""
            namespace Sample{
                internal static class DatabaseGenerator;
                public static class Probe{
                    public static bool Run(){
                        var key=Keys{{schema}}.Item.Create(1,2);
                        var original=Tables{{schema}}.Item.Create(key,10,"before");
                        var changed=original with{Id=3,Value=11,Name="after"};
                        var replaced=CopyWithKey(original,Keys{{schema}}.Item.Create(5,6));
                        var bytes=global::MemoryPack.MemoryPackSerializer.Serialize(changed);
                        var restored=global::MemoryPack.MemoryPackSerializer.Deserialize<Tables{{schema}}.Item>(bytes);
                        return original.Id==1&&original.TenantId==2&&original.Value==10&&original.Name=="before"
                            &&changed.Id==3&&changed.Key.Id==3&&changed.TenantId==2&&changed.Value==11&&changed.Name=="after"
                            &&replaced.Id==5&&replaced.TenantId==6&&replaced.Value==10&&replaced.Name=="before"
                            &&restored.Key.Id==3&&restored.Key.TenantId==2&&restored.Value==11&&restored.Name=="after";
                    }
                    private static TRecord CopyWithKey<TKey,TRecord>(TRecord row,TKey key)
                        where TRecord:struct,global::Seamless.Collections.IRecord<TKey>{
                        return row with{Key=key};
                    }
                }
            }
            namespace Sample.Tables{{schema}}{
                public partial struct Item{
                    [global::Seamless.Database.Key]
                    public partial int Id{get;init;}
                    [global::Seamless.Database.Key]
                    public partial long TenantId{get;}
                    public partial int Value{get;init;}
                    public partial string Name{get;init;}
                }
            }
            """;
        var output=Generate(source);
        var row=output.GetTypeByMetadataName("Sample.Tables"+schema+".Item")!;
        var key=output.GetTypeByMetadataName("Sample.Keys"+schema+".Item")!;
        Assert.NotNull(key);
        Assert.Contains(row.AllInterfaces,static type=>type.OriginalDefinition.ToDisplayString()=="Seamless.Collections.IRecord<TKey>");
        Assert.True(((IFieldSymbol)row.GetMembers("_Key").Single()).IsReadOnly);
        Assert.Equal(["Name"],row.GetTypeMembers("C").Single().GetMembers().OfType<IFieldSymbol>().Select(static field=>field.Name));
        Assert.Equal(["Value"],row.GetTypeMembers("S").Single().GetMembers().OfType<IFieldSymbol>().Select(static field=>field.Name));
        Assert.Null(((IPropertySymbol)row.GetMembers("TenantId").Single()).SetMethod);
        Assert.True(((IPropertySymbol)row.GetMembers("Id").Single()).SetMethod!.IsInitOnly);
        AssertRun(output);
    }

    [Fact,Trait("Category","Unit")]
    public void NoKeyAttribute_UsesAllDataPropertiesAndDoesNotDuplicateStorage(){
        const string source="""
            namespace Sample{
                internal static class DatabaseGenerator;
                public static class Probe{
                    public static bool Run(){
                        var original=Tables.Item.Create(Keys.Item.Create(1,"before"));
                        var changed=original with{Name="after"};
                        var first=original.Key;
                        var second=changed.Key;
                        return first.Id==1&&first.Name=="before"&&second.Id==1&&second.Name=="after"
                            &&original.Name=="before"&&changed.Name=="after";
                    }
                }
            }
            namespace Sample.Tables{
                public partial struct Item{
                    public partial int Id{get;init;}
                    public partial string Name{get;init;}
                }
            }
            """;
        var output=Generate(source);
        var row=output.GetTypeByMetadataName("Sample.Tables.Item")!;
        Assert.Empty(row.GetTypeMembers("C"));
        Assert.Empty(row.GetTypeMembers("S"));
        Assert.Equal(["_Key"],row.GetMembers().OfType<IFieldSymbol>().Select(static field=>field.Name));
        AssertRun(output);
    }

    [Theory,Trait("Category","Unit")]
    [InlineData("partial struct")]
    [InlineData("readonly partial struct")]
    [InlineData("partial record struct")]
    [InlineData("readonly partial record struct")]
    public void StructForms_ImplementInitContract(string form){
        var source=$$"""
            namespace Sample{
                internal static class DatabaseGenerator;
                public static class Probe{
                    public static bool Run(){
                        var original=Tables.Item.Create(Keys.Item.Create(1),"before");
                        var changed=original with{Id=2,Name="after"};
                        var normalized=Tables.Item.Strategy.Normalize(original);
                        return original.Id==1&&original.Name=="before"&&changed.Key.Id==2&&changed.Name=="after"
                            &&original.Equals(normalized)&&!original.Equals(changed)
                            &&original.GetHashCode()==normalized.GetHashCode()
                            &&original.GetHashCode()==global::Seamless.Execution.RecordValueStrategy<Tables.Item>.GetHashCode(original)
                            &&Tables.Item.Strategy.ContractId.Length>0;
                    }
                }
            }
            namespace Sample.Tables{
                public {{form}} Item{
                    [global::Seamless.Database.Key]
                    public partial int Id{get;init;}
                    public partial string Name{get;init;}
                }
            }
            """;
        AssertRun(Generate(source));
    }

    [Fact,Trait("Category","Unit")]
    public void NestedGeneratedReferenceFields_ArePlacedInReferenceStorage(){
        const string source="""
            namespace Sample{
                internal static class DatabaseGenerator;
                public static class Probe{
                    public static bool Run(){
                        var nested=Tables.Node.Create(Keys.Node.Create("before"));
                        var original=Tables.Item.Create(Keys.Item.Create(1,nested));
                        var bytes=global::MemoryPack.MemoryPackSerializer.Serialize(original);
                        var restored=global::MemoryPack.MemoryPackSerializer.Deserialize<Tables.Item>(bytes);
                        return restored.Id==1&&restored.Payload.Name=="before";
                    }
                }
            }
            namespace Sample.Tables{
                public partial struct Node{
                    public partial string Name{get;init;}
                }
                public partial struct Item{
                    public partial int Id{get;init;}
                    public partial Node Payload{get;init;}
                }
            }
            """;
        var output=Generate(source);
        var key=output.GetTypeByMetadataName("Sample.Keys.Item")!;
        Assert.Equal(["Payload"],key.GetTypeMembers("C").Single().GetMembers().OfType<IFieldSymbol>().Select(static field=>field.Name));
        Assert.Equal(["Id"],key.GetTypeMembers("S").Single().GetMembers().OfType<IFieldSymbol>().Select(static field=>field.Name));
        AssertRun(output);
    }

    [Theory,Trait("Category","Unit")]
    [InlineData("public int Id{get;init;}")]
    [InlineData("public int Id;")]
    [InlineData("public partial int Key{get;}")]
    [InlineData("public partial int this[int index]{get;}")]
    [InlineData("[global::Seamless.Database.Key] public static int StaticId=>1; public partial int Id{get;}")]
    public void InvalidDataDeclarations_ReportSdb003(string declaration){
        var source=$$"""
            namespace Sample{internal static class DatabaseGenerator;}
            namespace Sample.Tables{
                public partial struct Item{
                    {{declaration}}
                }
            }
            """;
        var compilation=GeneratorTestHelpers.CreateCompilation([("Database.cs",source)]);
        var result=GeneratorTestHelpers.CreateDatabaseDriver().RunGenerators(compilation).GetRunResult();
        Assert.Contains(result.Diagnostics,static diagnostic=>diagnostic.Id=="SDB003"&&diagnostic.Severity==DiagnosticSeverity.Error);
        Assert.DoesNotContain(result.Diagnostics,static diagnostic=>diagnostic.Id=="CS8785");
    }

    [Fact,Trait("Category","Unit")]
    public void ExistingKeyType_ReportsCollision(){
        const string source="""
            namespace Sample{internal static class DatabaseGenerator;}
            namespace Sample.Tables{public partial struct Item{public partial int Id{get;}}}
            namespace Sample.Keys{public struct Item;}
            """;
        var compilation=GeneratorTestHelpers.CreateCompilation([("Database.cs",source)]);
        var result=GeneratorTestHelpers.CreateDatabaseDriver().RunGenerators(compilation).GetRunResult();
        Assert.Contains(result.Diagnostics,static diagnostic=>diagnostic.Id=="SDB003"&&diagnostic.GetMessage().Contains("conflicts",StringComparison.Ordinal));
    }

    [Fact,Trait("Category","Unit")]
    public void UnrelatedKeyAttribute_DoesNotChangeTheAllColumnsDefault(){
        const string source="""
            namespace Other{public sealed class KeyAttribute:global::System.Attribute;}
            namespace Sample{
                internal static class DatabaseGenerator;
                public static class Probe{
                    public static bool Run(){
                        var row=Tables.Item.Create(Keys.Item.Create(1,2));
                        return row.Key.Id==1&&row.Key.Value==2;
                    }
                }
            }
            namespace Sample.Tables{
                public partial struct Item{
                    [global::Other.Key]
                    public partial int Id{get;}
                    public partial int Value{get;}
                }
            }
            """;
        AssertRun(Generate(source));
    }

    [Theory,Trait("Category","Unit")]
    [InlineData("Tables","Tables")]
    [InlineData("Tables","Views")]
    [InlineData("Tables","Work")]
    [InlineData("Views","Work")]
    [InlineData("Work","Views")]
    public void MixedSchemaModes_AreAllowed(string first,string second){
        var source=$$"""
            namespace Sample{internal static class DatabaseGenerator;}
            namespace Sample.{{first}}{
                public partial struct Item{
                    public partial int Id{get;}
                }
            }
            namespace Sample.{{second}}.dbo{
                public partial struct Other{
                    public partial int Id{get;}
                }
            }
            """;
        var compilation=GeneratorTestHelpers.CreateCompilation([("Database.cs",source)]);
        var result=GeneratorTestHelpers.CreateDatabaseDriver().RunGenerators(compilation).GetRunResult();
        Assert.DoesNotContain(result.Diagnostics,static diagnostic=>diagnostic.Id=="SDB003"
            &&diagnostic.GetMessage().Contains("schema",StringComparison.OrdinalIgnoreCase));
    }

    [Fact,Trait("Category","Unit")]
    public void MixedSchemas_KeepRootAndNamedMembersSeparate(){
        const string source="""
            namespace Sample{
                internal static class DatabaseGenerator;
                public sealed partial class Catalog:global::Seamless.Database.DatabaseContainer;
                public static class Probe{
                    public static bool Run(){
                        var root=Tables.Item.Create(Keys.Item.Create(1));
                        var first=Tables.dbo0.Item.Create(Keys.dbo0.Item.Create(2));
                        var second=Tables.dbo1.Item.Create(Keys.dbo1.Item.Create(3));
                        var namedDbo=Tables.dbo.Item.Create(Keys.dbo.Item.Create(4));
                        return root.Id==1&&first.Id==2&&second.Id==3&&namedDbo.Id==4
                            &&typeof(Catalog).GetProperty("Item") is not null
                            &&typeof(Catalog).GetProperty("dbo0")!.PropertyType.GetProperty("Item") is not null
                            &&typeof(Catalog).GetProperty("dbo1")!.PropertyType.GetProperty("Item") is not null
                            &&typeof(Catalog).GetProperty("dbo")!.PropertyType.GetProperty("Item") is not null;
                    }
                }
            }
            namespace Sample.Tables{public partial struct Item{public partial int Id{get;}}}
            namespace Sample.Tables.dbo0{public partial struct Item{public partial int Id{get;}}}
            namespace Sample.Tables.dbo1{public partial struct Item{public partial int Id{get;}}}
            namespace Sample.Tables.dbo{public partial struct Item{public partial int Id{get;}}}
            """;
        AssertRun(Generate(source));
    }

    [Theory,Trait("Category","Unit")][InlineData(false)][InlineData(true)]
    public void DeclaredViewsCoexistWithRootAndExplicitEmptySchemas(bool explicitSchema){
        var members="";
        if(explicitSchema){members="public ExistingSchema dbo0=>new(this);";}
        var source=$$"""
            namespace Sample{
                internal static class DatabaseGenerator;
                public sealed partial class Catalog:global::Seamless.Database.DatabaseContainer{
                    {{members}}
                }
                public sealed partial class ExistingSchema(Catalog database):global::Seamless.Database.DatabaseSchema(database,"dbo0");
                public static class Probe{
                    public static bool Run()=>typeof(Catalog).GetProperty("Summary")!.PropertyType.GetGenericTypeDefinition()==typeof(global::Seamless.Collections.ISet<>)
                        &&typeof(Catalog).GetProperty("dbo0")!.PropertyType.GetProperty("Summary")!.PropertyType.GetGenericTypeDefinition()==typeof(global::Seamless.Collections.ISet<>);
                }
            }
            namespace Sample.Views{
                public readonly record struct Summary(int Id){
                    public static global::Seamless.Collections.ISet<Summary> Query(Sample.Catalog container)=>null!;
                }
            }
            namespace Sample.Views.dbo0{
                public readonly record struct Summary(int Id){
                    public static global::Seamless.Collections.ISet<Summary> Query(Sample.Catalog container)=>null!;
                }
            }
            """;
        AssertRun(Generate(source));
    }

    [Theory,Trait("Category","Unit")]
    [InlineData("Tables")][InlineData("Views")][InlineData("Work")]
    public void RootObjectAndSchemaNameCollision_IsDiagnosed(string kind){
        var source=$$"""
            namespace Sample{
                internal static class DatabaseGenerator;
                public sealed partial class Catalog:global::Seamless.Database.DatabaseContainer;
            }
            namespace Sample.{{kind}} {
                public partial struct sales { public partial int Id { get; } }
            }
            namespace Sample.Tables.sales {
                public partial struct Item { public partial int Id { get; } }
            }
            """;
        var result=GeneratorTestHelpers.CreateDatabaseDriver().RunGenerators(GeneratorTestHelpers.CreateCompilation([("Database.cs",source)])).GetRunResult();
        Assert.Contains(result.Diagnostics,static diagnostic=>diagnostic.Id=="SDB001");
    }

    [Fact,Trait("Category","Unit")]
    public void MultipleSchemas_KeepIdenticallyNamedKeysSeparate(){
        const string source="""
            namespace Sample{
                internal static class DatabaseGenerator;
                public static class Probe{
                    public static bool Run(){
                        var first=Tables.dbo.Item.Create(Keys.dbo.Item.Create(1));
                        var second=Tables.sales.Item.Create(Keys.sales.Item.Create(2));
                        return first.Id==1&&second.Id==2;
                    }
                }
            }
            namespace Sample.Tables.dbo{public partial struct Item{public partial int Id{get;}}}
            namespace Sample.Tables.sales{public partial struct Item{public partial int Id{get;}}}
            """;
        AssertRun(Generate(source));
    }

    [Fact,Trait("Category","Unit")]
    public void DifferentDatabaseRoots_CanChooseDifferentSchemaModes(){
        const string source="""
            namespace First{internal static class DatabaseGenerator;}
            namespace Second{internal static class DatabaseGenerator;}
            namespace First.Tables{public partial struct Item{public partial int Id{get;}}}
            namespace Second.Tables.dbo{public partial struct Item{public partial int Id{get;}}}
            namespace Sample{
                public static class Probe{
                    public static bool Run(){
                        var first=global::First.Tables.Item.Create(global::First.Keys.Item.Create(1));
                        var second=global::Second.Tables.dbo.Item.Create(global::Second.Keys.dbo.Item.Create(2));
                        return first.Id==1&&second.Id==2;
                    }
                }
            }
            """;
        AssertRun(Generate(source));
    }

    [Fact,Trait("Category","Unit")]
    public void LegacyOptimizedLayout_PreservesExistingNestedValueOrder(){
        const string source="""
            namespace Sample{
                internal static class DatabaseGenerator{public const bool OptimizeFieldLayout=true;}
                public static class Probe{public static bool Run()=>Records.Outer.LayoutIsCompatible();}
            }
            namespace Sample.Records{
                public partial struct Inner{
                    public partial long First{get;}
                    public partial int Second{get;}
                }
                public partial struct Outer{
                    public partial byte Head{get;}
                    public partial long Tail{get;}
                    public partial Inner Value{get;}
                    public static bool LayoutIsCompatible()=>
                        global::System.Runtime.InteropServices.Marshal.OffsetOf<F>("Tail").ToInt64()==0
                        &&global::System.Runtime.InteropServices.Marshal.OffsetOf<F>("Value").ToInt64()==8;
                }
            }
            """;
        AssertRun(Generate(source));
    }

    [Fact,Trait("Category","Unit")]
    public void ScalarColumns_NormalizeConstructionAndInitIncludingAssignedKeys(){
        const string source="""
            namespace Sample{
                internal static class DatabaseGenerator;
                public static class Probe{
                    public static bool Run(){
                        var negativeZero=global::System.BitConverter.Int64BitsToDouble(long.MinValue);
                        var nan=global::System.BitConverter.Int32BitsToSingle(0x7f800001);
                        var unspecified=new global::System.DateTime(2026,11,7,12,34,56);
                        var offset=new global::System.DateTimeOffset(2026,11,7,12,34,56,global::System.TimeSpan.FromHours(9));
                        var key=Keys.Item.Create(negativeZero,unspecified);
                        var original=Tables.Item.Create(key,nan,1.2300m,offset,negativeZero,(global::System.Half)(-0.0));
                        var changed=original with{
                            Value=global::System.BitConverter.Int32BitsToSingle(int.MinValue),
                            Amount=new decimal(0,0,0,true,28),
                            Maybe=null,
                            Key=default
                        };
                        var assigned=Tables.Item.Create(default,0,0,offset,null,(global::System.Half)0);
                        var bytes=global::MemoryPack.MemoryPackSerializer.Serialize(changed);
                        var restored=global::MemoryPack.MemoryPackSerializer.Deserialize<Tables.Item>(bytes);
                        return global::System.BitConverter.DoubleToInt64Bits(original.Id)==0
                            &&original.Stamp.Kind==global::System.DateTimeKind.Utc&&original.Stamp.Ticks==unspecified.Ticks
                            &&global::System.BitConverter.SingleToInt32Bits(original.Value)==0x7fc00000
                            &&decimal.GetBits(original.Amount)[3]==0x00020000
                            &&original.Offset.Offset==global::System.TimeSpan.Zero&&original.Offset.UtcTicks==offset.UtcTicks
                            &&global::System.BitConverter.DoubleToInt64Bits(original.Maybe!.Value)==0
                            &&global::System.BitConverter.HalfToInt16Bits(original.Small)==0
                            &&global::System.BitConverter.SingleToInt32Bits(changed.Value)==0
                            &&decimal.GetBits(changed.Amount)[3]==0&&changed.Maybe is null
                            &&changed.Stamp.Kind==global::System.DateTimeKind.Utc&&changed.Stamp.Ticks==0
                            &&assigned.Stamp.Kind==global::System.DateTimeKind.Utc
                            &&restored.Stamp.Kind==global::System.DateTimeKind.Utc&&restored.Amount==0;
                    }
                }
            }
            namespace Sample.Tables{
                public partial struct Item{
                    [global::Seamless.Database.Key]
                    public partial double Id{get;init;}
                    [global::Seamless.Database.Key]
                    public partial global::System.DateTime Stamp{get;}
                    public partial float Value{get;init;}
                    public partial decimal Amount{get;init;}
                    public partial global::System.DateTimeOffset Offset{get;init;}
                    public partial double? Maybe{get;init;}
                    public partial global::System.Half Small{get;init;}
                }
            }
            """;
        AssertRun(Generate(source));
    }

    [Fact,Trait("Category","Unit")]
    public void GeneratedStrategy_NormalizesWholeRecordAndPreservesSharedReferences(){
        const string source="""
            namespace Sample{
                internal static class DatabaseGenerator;
                public sealed class Node(double number){
                    private readonly double number=number;
                    private Node? next;
                    public double Number=>this.number;
                    public Node? Next=>this.next;
                    public void Link(Node node)=>this.next=node;
                }
                public static class Probe{
                    public static bool Run(){
                        var node=new Node(global::System.BitConverter.Int64BitsToDouble(long.MinValue));
                        node.Link(node);
                        var row=Tables.Item.Create(Keys.Item.Create(node),node,node);
                        var copied=row with{First=node,Second=node};
                        var normalized=Tables.Item.Strategy.Normalize(copied);
                        var other=new Node(0);
                        other.Link(other);
                        var distinct=Tables.Item.Strategy.Normalize(copied with{Second=other});
                        var repeated=Tables.Item.Strategy.Normalize(copied);
                        return global::System.Object.ReferenceEquals(row.First,node)
                            &&global::System.Object.ReferenceEquals(row.Id,node)
                            &&global::System.Object.ReferenceEquals(copied.First,copied.Second)
                            &&!global::System.Object.ReferenceEquals(normalized.First,node)
                            &&global::System.Object.ReferenceEquals(normalized.Id,normalized.First)
                            &&global::System.Object.ReferenceEquals(normalized.First,normalized.Second)
                            &&global::System.Object.ReferenceEquals(normalized.First,normalized.First.Next)
                            &&global::System.Object.ReferenceEquals(distinct.Id,distinct.First)
                            &&!global::System.Object.ReferenceEquals(distinct.First,distinct.Second)
                            &&global::System.Object.ReferenceEquals(distinct.Second,distinct.Second.Next)
                            &&!global::System.Object.ReferenceEquals(repeated.First,normalized.First)
                            &&global::System.BitConverter.DoubleToInt64Bits(normalized.First.Number)==0
                            &&global::System.BitConverter.DoubleToInt64Bits(node.Number)==long.MinValue
                            &&row.Equals(normalized)&&row.GetHashCode()==normalized.GetHashCode();
                    }
                }
            }
            namespace Sample.Tables{
                public partial struct Item{
                    [global::Seamless.Database.Key]
                    public partial global::Sample.Node Id{get;}
                    public partial global::Sample.Node First{get;init;}
                    public partial global::Sample.Node Second{get;init;}
                }
            }
            """;
        AssertRun(Generate(source));
    }

    [Fact,Trait("Category","Unit")]
    public void ScalarStrategy_HotPathDoesNotAllocateGraphWork(){
        const string source="""
            namespace Sample{
                internal static class DatabaseGenerator;
                public static class Probe{
                    public static bool Run(){
                        var row=Tables.Item.Create(Keys.Item.Create(1),2.500m,"same");
                        for(var index=0;index<64;index++){
                            _=global::Seamless.Execution.RecordValueStrategy<Tables.Item>.Normalize(row);
                            _=global::Seamless.Execution.RecordValueStrategy<Tables.Item>.Equals(row,row);
                            _=global::Seamless.Execution.RecordValueStrategy<Tables.Item>.GetHashCode(row);
                        }
                        var before=global::System.GC.GetAllocatedBytesForCurrentThread();
                        var total=0;
                        for(var index=0;index<256;index++){
                            var copy=global::Seamless.Execution.RecordValueStrategy<Tables.Item>.Normalize(row);
                            if(!copy.Equals(row)){return false;}
                            total=unchecked(total+global::Seamless.Execution.RecordValueStrategy<Tables.Item>.GetHashCode(copy));
                        }
                        var allocated=global::System.GC.GetAllocatedBytesForCurrentThread()-before;
                        return allocated==0&&total==unchecked(row.GetHashCode()*256);
                    }
                }
            }
            namespace Sample.Tables{
                public partial struct Item{
                    [global::Seamless.Database.Key]public partial int Id{get;}
                    public partial decimal Amount{get;init;}
                    public partial string Name{get;init;}
                }
            }
            """;
        AssertRun(Generate(source));
    }

    private static Compilation Generate(string source){
        var compilation=GeneratorTestHelpers.CreateCompilation([("Database.cs",source)]);
        GeneratorDriver driver=GeneratorTestHelpers.CreateDatabaseDriver();
        driver=driver.RunGeneratorsAndUpdateCompilation(compilation,out var output,out var diagnostics);
        Assert.DoesNotContain(diagnostics,static diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error);
        var errors=output.GetDiagnostics().Where(static diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error).ToArray();
        Assert.True(errors.Length==0,string.Join(Environment.NewLine,errors.Select(static diagnostic=>diagnostic.ToString())));
        var warnings=output.GetDiagnostics().Where(static diagnostic=>diagnostic.Severity==DiagnosticSeverity.Warning
            &&diagnostic.Location.SourceTree?.FilePath.EndsWith(".Record.g.cs",StringComparison.Ordinal)==true).ToArray();
        Assert.True(warnings.Length==0,string.Join(Environment.NewLine,warnings.Select(static diagnostic=>diagnostic.ToString())));
        driver.RunGeneratorsAndUpdateCompilation(compilation,out var repeated,out var repeatedDiagnostics);
        Assert.DoesNotContain(repeatedDiagnostics,static diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error);
        Assert.DoesNotContain(repeated.GetDiagnostics(),static diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error);
        return output;
    }

    private static void AssertRun(Compilation compilation){
        using var stream=new MemoryStream();
        var emitted=compilation.Emit(stream);
        Assert.True(emitted.Success,string.Join(Environment.NewLine,emitted.Diagnostics));
        stream.Position=0;
        var context=new global::System.Runtime.Loader.AssemblyLoadContext(null,isCollectible:true);
        try{
            var assembly=context.LoadFromStream(stream);
            Assert.True((bool)assembly.GetType("Sample.Probe")!.GetMethod("Run")!.Invoke(null,null)!);
        }finally{
            context.Unload();
        }
    }
}
