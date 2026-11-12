using Seamless.Management;

namespace Management.Tests;

public sealed class CSharpTypeCompletionTests{
    [Fact]public void GlobalRootShowsNamespacesAndRealGlobalTypes(){
        var completion=new CSharpTypeCompletion(["int","string","Guid","global::RootType","global::Sample.Tables.Row"]);
        var items=completion.Complete("global::",8);
        Assert.Contains(items,item=>item.Label=="System"&&item.IsNamespace&&item.InsertText=="System.");
        Assert.Contains(items,item=>item.Label=="RootType"&&!item.IsNamespace);
        Assert.DoesNotContain(items,item=>item.Label is "int" or "string" or "Guid");
    }
    [Fact]public void QualifiedGenericTypesAreShallowAndKeepTypeParameters(){
        var completion=new CSharpTypeCompletion([]);
        const string source="global::System.Collections.Generic.";
        var items=completion.Complete(source,source.Length);
        Assert.Contains(items,item=>item.Label=="List<T>"&&item.InsertText=="List<T>"&&!item.IsNamespace);
        Assert.Contains(items,item=>item.Label=="HashSet<T>");
        Assert.Single(items,item=>item.Label=="Dictionary<TKey,TValue>");
        Assert.DoesNotContain(items,item=>item.Label.Contains("System.Collections.Generic.",StringComparison.Ordinal));
    }
    [Fact]public void ATypeArgumentAndTheIdentifierRightOfTheCaretAreReplacedIndependently(){
        var completion=new CSharpTypeCompletion([]);
        const string source="global::System.Collections.Generic.List<global::System.Str>";
        var caret=source.LastIndexOf('>');
        var item=Assert.Single(completion.Complete(source,caret),item=>item.Label=="String");
        Assert.Equal("global::System.Collections.Generic.List<global::System.String>",source[..item.Start]+item.InsertText+source[(item.Start+item.Length)..]);
        const string middle="global::System.Colllections.Generic.List<int>";
        var position=middle.IndexOf("Coll",StringComparison.Ordinal)+4;
        var space=Assert.Single(completion.Complete(middle,position),item=>item.Label=="Collections");
        Assert.Equal("global::System.Collections.Generic.List<int>",middle[..space.Start]+space.InsertText+middle[(space.Start+space.Length)..]);
    }
    [Fact]public void SourceAndNestedGenericTypesParticipateInCompletion(){
        var completion=new CSharpTypeCompletion(["int","global::Sample.Box<T>","global::Sample.Box<T>.Nested<U>"]);
        const string source="global::Sample.Box<int>.";
        Assert.Contains(completion.Complete(source,source.Length),item=>item.Label=="Nested<U>"&&!item.IsNamespace);
        var bare=Assert.Single(completion.Complete("Nest",4),item=>item.Label.StartsWith("Nested<U>",StringComparison.Ordinal));
        Assert.Equal("global::Sample.Box<T>.Nested<U>",bare.InsertText);
    }
    [Fact]public void ReferenceMetadataHonorsTheFriendAssemblyWithoutExecutingIt(){
        var path=typeof(Seamless.Management.Components.コマンド登録<>).Assembly.Location;
        var publicTypes=CSharpTypeCompletion.ReadMetadataTypes([path]);
        Assert.DoesNotContain("Seamless.Management.Components.CSharpHighlighting",publicTypes);
        var accessible=CSharpTypeCompletion.ReadMetadataTypes([path],typeof(CSharpTypeCompletionTests).Assembly.GetName());
        Assert.Contains("Seamless.Management.Components.CSharpHighlighting",accessible);
    }
}
