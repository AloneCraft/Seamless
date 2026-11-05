using Seamless.Management;

namespace Management.Tests;

public sealed class SourceEditor{
    [Fact]public void Apply(){
        const string text="using System.Linq; // keep\nclass C{int Extra()=>42;int[] Q(int[] xs)=>xs.Where(x=>x>0).Select(x=>x+1).ToArray();}";
        var snapshot=new SourceSnapshot("1",[new("query.cs",text)],[]);
        var editor=new Seamless.Management.SourceEditor();var result=editor.Analyze(snapshot);
        Assert.True(result.Current);var node=Assert.Single(result.Nodes,node=>node.Label=="Where");
        var edited=editor.Apply(snapshot,new(snapshot.Hash(),node.Path,node.Start,node.Length,"xs.Where(x=>x>5)"));
        Assert.Contains("// keep\nclass C{int Extra()=>42;",edited.Files[0].Text);
        Assert.Contains("xs.Where(x=>x>5).Select(x=>x+1).ToArray()",edited.Files[0].Text);
        Assert.Throws<ManagementException>(()=>editor.Apply(edited,new(snapshot.Hash(),node.Path,node.Start,node.Length,"bad")));
        Assert.False(editor.Analyze(snapshot with{Files=[new("query.cs","class C{")]}).Current);
    }
    [Fact]public void Analyze(){
        var editor=new Seamless.Management.SourceEditor();var input=new SourceSnapshot("1",[new("schema.cs","class C{public int Id{get;} public string Name{get;} static int Custom(int n){while(n>1){n/=2;}return n;}}")],[]);
        var result=editor.Analyze(input);Assert.True(result.Current);
        Assert.Contains(result.Nodes,node=>node.Kind=="Property"&&node.Label=="Name");
        Assert.Contains(result.Nodes,node=>node.Kind=="CustomCode");
        Assert.Equal(input,editor.Apply(input,new(input.Hash(),"schema.cs",0,0,"")));
        var staticCall=editor.Analyze(new("1",[new("static.cs","using System.Linq;class C{int[] Q(int[] values)=>Enumerable.Where(values,x=>x>0).ToArray();}")],[]));Assert.False(Assert.Single(staticCall.Nodes,node=>node.Label=="Where").Editable);
    }
}
