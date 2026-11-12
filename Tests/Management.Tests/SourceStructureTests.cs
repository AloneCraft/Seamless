using Seamless.Management;

namespace Management.Tests;

public sealed class SourceStructureTests{
    [Fact]public void NestedAndQualifiedNamespacesDescribeTheSameDatabaseObjects(){
        var nested=Source("""
            namespace Sample{
                internal static partial class DatabaseGenerator;
                namespace Tables{namespace dbo{public record struct Item(int Id,string Title,decimal UnitPrice);}}
                namespace Views{namespace dbo{public partial struct Summary{public partial int Count{get;}}}}
                namespace Work{public partial struct Cache{public partial long Id{get;init;}}}
            }
            """);
        var qualified=Source("""
            namespace Sample{internal static partial class DatabaseGenerator;}
            namespace Sample.Tables.dbo{public record struct Item(int Id,string Title,decimal UnitPrice);}
            namespace Sample.Views.dbo{public partial struct Summary{public partial int Count{get;}}}
            namespace Sample.Work{public partial struct Cache{public partial long Id{get;init;}}}
            """);
        var first=SourceStructure.Read(nested);var second=SourceStructure.Read(qualified);
        Assert.Equal(["Sample"],first.Namespaces);
        Assert.Equal(first.Types,second.Types);
        Assert.Equal(first.Objects.Select(item=>(item.Root,item.Kind,item.Schema,item.Name,item.Type)),second.Objects.Select(item=>(item.Root,item.Kind,item.Schema,item.Name,item.Type)));
        var table=Assert.Single(first.Objects,item=>item.Kind=="Tables");
        Assert.Equal("dbo",table.Schema);
        Assert.Equal(["Id","Title","UnitPrice"],table.Columns.Select(column=>column.Name));
        Assert.Equal(["int","string","decimal"],table.Columns.Select(column=>column.Type));
        Assert.Contains("record struct Item",nested.Files[0].Text.Substring(table.Start,table.Length));
        Assert.Contains("global::Sample.Tables.dbo.Item",first.Types);
    }
    [Fact]public void ConditionsPartialTypesAndNamespaceBoundariesAreRespected(){
        var first="""
            namespace Example{class DatabaseGenerator{}}
            namespace Example.Tables.dbo{public partial struct Row{public int Id{get;init;}}}
            namespace Example.TablesOther{public struct Excluded{}}
            namespace NotARoot{public enum DatabaseGenerator{Value}}
            #if NEVER
            namespace Ignored{class DatabaseGenerator{}}
            namespace Example.Tables.dbo{public struct Hidden{}}
            #endif
            """;
        var source=new SourceSnapshot("1",[new("First.cs",first),new("Second.cs","namespace Example.Tables.dbo;public partial struct Row{public string Name{get;init;}}")],[]);
        var outline=SourceStructure.Read(source);
        Assert.Equal(["Example"],outline.Namespaces);
        var row=Assert.Single(outline.Objects);
        Assert.Equal("Row",row.Name);
        Assert.Equal(["Id","Name"],row.Columns.Select(column=>column.Name));
        Assert.Equal(1,outline.Types.Count(type=>type=="global::Example.Tables.dbo.Row"));
        Assert.Contains("global::Example.TablesOther.Excluded",outline.Types);
        Assert.DoesNotContain(outline.Types,type=>type.Contains("Hidden",StringComparison.Ordinal));
        Assert.Equal(source.Hash(),source.Freeze().Hash());
    }
    [Fact]public void TypeCandidatesIncludeGenericTemplatesAndExcludeInaccessibleTypes(){
        var outline=SourceStructure.Read(Source("""
            class DatabaseGenerator{}
            public static class Utility{}
            public class Generic<T>{public class Nested{}}
            public class Outer{private struct Hidden{} public struct Visible{}}
            public enum Kind{First,Second}
            public delegate int Selector(int value);
            namespace Tables{public struct @event{public double Value{get;}}}
            """));
        Assert.Equal([""],outline.Namespaces);
        Assert.Equal("event",Assert.Single(outline.Objects).Name);
        Assert.Contains("global::Tables.@event",outline.Types);
        Assert.Contains("global::Kind",outline.Types);
        Assert.Contains("global::Selector",outline.Types);
        Assert.Contains("global::Outer.Visible",outline.Types);
        Assert.Contains("global::Generic<T>",outline.Types);
        Assert.Contains("global::Generic<T>.Nested",outline.Types);
        Assert.DoesNotContain(outline.Types,type=>type.Contains("Hidden",StringComparison.Ordinal)||type.Contains("Utility",StringComparison.Ordinal));
    }
    private static SourceSnapshot Source(string text)=>new("1",[new("Database.cs",text)],[]);
    [Fact]public void SourceRangesUseUtf16AndColumnTypeAliasesRemainReadable(){
        const string code="using Money=System.Decimal;\n// 🧭\nnamespace Sample{class DatabaseGenerator{} namespace Tables{public record struct Item(Money Price);}}";
        var item=Assert.Single(SourceStructure.Read(Source(code)).Objects);
        Assert.Equal(code.IndexOf("public record struct Item",StringComparison.Ordinal),item.Start);
        Assert.Equal("Money",Assert.Single(item.Columns).Type);
        var incomplete=SourceStructure.Read(Source("namespace Sample{class DatabaseGenerator{} namespace Tables{public partial struct"));
        Assert.Empty(incomplete.Objects);
        Assert.Equal(["Sample"],incomplete.Namespaces);
    }
}
