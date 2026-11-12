using System.Text.Json;
using Seamless.Management;
using Designer=Seamless.Management.DatabaseDesigner;

namespace Management.Tests;

public sealed class DeclaredViewTemplates{
    [Fact]
    public void SameNamedViewsUseDeclaredDefinitionsAndKeepTheirOwnScopeWhenEdited(){
        var definition=Designer.Empty("declared-view-sdk","Declared view SDK");
        definition=Designer.AddSchema(definition,"dbo0");
        definition=Designer.AddSchema(definition,"dbo1");
        var scopes=new[]{"","dbo0","dbo1"};
        var helpers=new[]{"RootRows","Dbo0Rows","Dbo1Rows"};
        for(var index=0;index<scopes.Length;index++){
            definition=Designer.ApplyQuery(definition,new("Shared",scopes[index],DatabaseQueryKind.View,
                [new("Id","int",false)],[],"","// original body "+helpers[index]+"\nreturn global::UserCode.ViewRows."+helpers[index]+"();"));
        }
        const string helpersSource="""
            using System.Collections.Generic;
            using System.Linq;
            namespace UserCode;
            // Unrelated declaration and these helper bodies must survive form editing.
            [global::Seamless.FLinq.LinqIntercept(true)]
            public sealed class ViewRows{
                public static IEnumerable<global::UserCode.Views.Shared> RootRows()=>new[]{11,11}.Select(value=>new global::UserCode.Views.Shared(value));
                public static IEnumerable<global::UserCode.Views.dbo0.Shared> Dbo0Rows()=>new[]{22,22}.Select(value=>new global::UserCode.Views.dbo0.Shared(value));
                public static IEnumerable<global::UserCode.Views.dbo1.Shared> Dbo1Rows()=>new[]{33,33}.Select(value=>new global::UserCode.Views.dbo1.Shared(value));
                public const string Kept="other declaration";
            }
            [global::Seamless.FLinq.LinqIntercept(true)]
            public sealed class ViewCalls{
                public long[] Root(ManagedDb db)=>db.Shared.Select(row=>row.Id).ToArray();
                public long[] Dbo0(ManagedDb db)=>db.dbo0.Shared.Select(row=>row.Id).ToArray();
                public long[] Dbo1(ManagedDb db)=>db.dbo1.Shared.Select(row=>row.Id).ToArray();
            }
            """;
        definition=definition with{Source=definition.Source with{Files=definition.Source.Files.Select(file=>{
            if(!file.Path.StartsWith("Views/",StringComparison.Ordinal)){return file;}
            return file with{Text="// declaration comment "+file.Path+"\n"+file.Text
                .Replace("public readonly partial record struct","[global::System.Serializable] public readonly partial record struct",StringComparison.Ordinal)
                .Replace("public static global::System.Collections.Generic.IEnumerable<","[global::System.Diagnostics.DebuggerStepThrough] public static global::System.Collections.Generic.IEnumerable<",StringComparison.Ordinal)};
        }).Append(new("ViewHelpers.cs",helpersSource)).ToArray()}};
        for(var index=0;index<scopes.Length;index++){
            var path=string.Join(".",new[]{scopes[index],"Shared"}.Where(part=>part.Length>0));
            var query=Designer.ReadQuery(definition,path);
            definition=Designer.ApplyQuery(definition,query with{
                Columns=[new("Id","long",false)],Body="// edited body "+helpers[index]+"\n"+query.Body
            });
            var edited=Designer.ReadQuery(definition,path);
            Assert.Equal(scopes[index],edited.Schema);
            Assert.Equal("long",Assert.Single(edited.Columns).Type);
            Assert.Contains("// original body "+helpers[index],edited.Body);
            Assert.Contains("// edited body "+helpers[index],edited.Body);
            var filePath="Views/"+(scopes[index].Length>0?scopes[index]+"/":"")+"Shared.cs";
            var text=definition.Source.Files.Single(file=>file.Path==filePath).Text;
            //本体はQueryへ直接書き、列を手書きした形では行型を返す。ラッパーと名前の文字列は書かない
            var rowNamespace="global::UserCode.Views.";
            if(scopes[index].Length>0){rowNamespace+=scopes[index]+".";}
            Assert.Contains("IEnumerable<"+rowNamespace+"@Shared> Query(",text);
            Assert.DoesNotContain("CreateView",text);
            Assert.DoesNotContain("\""+path+"\"",text);
            Assert.Contains("// declaration comment "+filePath,text);
            Assert.Contains("System.Serializable",text);
            Assert.Contains("DebuggerStepThrough",text);
        }
        Assert.Equal(helpersSource,definition.Source.Files.Single(file=>file.Path=="ViewHelpers.cs").Text);
        var objects=Designer.Objects(definition).Where(item=>item.Kind=="View").ToArray();
        Assert.Equal(new[]{"Shared","dbo0.Shared","dbo1.Shared"},objects.Select(item=>item.Id).Order(StringComparer.Ordinal));
        var selections=objects.Select(item=>Designer.SelectQueryPlans(definition,item.Id,[])).ToArray();
        for(var index=0;index<selections.Length;index++){
            Assert.NotEmpty(selections[index].Methods);
            Assert.Contains(selections[index].Methods,method=>method.Contains("ViewRows."+helpers[index]+"(",StringComparison.Ordinal));
            foreach(var other in helpers.Where(helper=>helper!=helpers[index])){
                Assert.DoesNotContain(selections[index].Methods,method=>method.Contains("ViewRows."+other+"(",StringComparison.Ordinal));
            }
        }
        if(Environment.GetEnvironmentVariable("SEAMLESS_VIEW_SDK_ROOT") is{} output){
            var directory=Path.GetFullPath(output);
            Assert.StartsWith("F:\\Seamless\\",directory,StringComparison.OrdinalIgnoreCase);
            Directory.CreateDirectory(directory);
            foreach(var file in definition.Source.Files){
                var path=Path.GetFullPath(Path.Combine(directory,file.Path));
                Assert.StartsWith(directory+Path.DirectorySeparatorChar,path,StringComparison.OrdinalIgnoreCase);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path,file.Text);
            }
            File.WriteAllText(Path.Combine(directory,"definition.json"),JsonSerializer.Serialize(definition,new JsonSerializerOptions{WriteIndented=true}));
        }
    }
}
