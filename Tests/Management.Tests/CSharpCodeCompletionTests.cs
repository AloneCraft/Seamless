using Seamless.Management;

namespace Management.Tests;

public sealed class CSharpCodeCompletionTests{
    [Theory]
    [InlineData("class C { void M(){global::System.Collections.Generic.Li$$ value;} }","List")]
    [InlineData("class C { void M(){global::Sys$$ value;} }","System")]
    [InlineData("class C { void M(){string text=\"x\";text.Sub$$;} }","Substring")]
    [InlineData("class C { void M(){string? text=null;text?.Sub$$;} }","Substring")]
    [InlineData("class C { void M(){System.Math.Sq$$;} }","Sqrt")]
    [InlineData("class C { void M(int amount){int total=1; var x=amo$$;} }","amount")]
    [InlineData("class C { void M(){int total=1; var x=tot$$;} }","total")]
    [InlineData("using System.Linq; class C { void M(){int[] values=[];values.Whe$$;} }","Where")]
    public void SemanticSymbolsAreOfferedInIncompleteSource(string marked,string expected){
        var (source,position)=Input(marked);
        var result=new CSharpCodeCompletion().Complete(source,"Code.cs",position);
        Assert.Null(result.UnavailableReason);
        Assert.Contains(result.Items,item=>item.InsertText==expected);
        Assert.All(result.Items,item=>Assert.NotEmpty(item.DisplayText));
    }

    [Fact]
    public void ReceiverAccessibilityAndStaticRulesComeFromSemanticBinding(){
        var (source,position)=Input("class Other {private int Secret;public int Visible;public static int Shared;} class C {void M(){Other value=new();value.$$;}}");
        var result=new CSharpCodeCompletion().Complete(source,"Code.cs",position);
        Assert.Contains(result.Items,item=>item.InsertText=="Visible");
        Assert.DoesNotContain(result.Items,item=>item.InsertText is "Secret" or "Shared");
    }

    [Theory]
    [InlineData("class C {void M(){var x=\"System.$$\";}}")]
    [InlineData("class C {void M(){// System.$$\n}}")]
    [InlineData("class C {void M(){unknown.$$;}}")]
    public void UnavailableContextsDoNotOfferUnrelatedGlobalCandidates(string marked){
        var (source,position)=Input(marked);
        var result=new CSharpCodeCompletion().Complete(source,"Code.cs",position);
        Assert.Empty(result.Items);
        Assert.NotNull(result.UnavailableReason);
    }

    [Fact]
    public void ApplyReplacesTheWholeIdentifierAndRejectsChangesInAnySourceFile(){
        var (source,position)=Input("class C {void M(){string text=\"\";text.Sub$$wrong;}}");
        source=source with{Files=[..source.Files,new("Other.cs","class Other {}") ]};
        var completion=new CSharpCodeCompletion();
        var result=completion.Complete(source,"Code.cs",position);
        var index=Array.FindIndex(result.Items,item=>item.InsertText=="Substring");
        Assert.True(index>=0);
        var applied=completion.Apply(source,result,index);
        Assert.Contains("text.Substring;",applied.Files[0].Text);
        Assert.DoesNotContain("wrong",applied.Files[0].Text);
        var changed=source with{Files=[source.Files[0],new("Other.cs","class Changed {}") ]};
        var error=Assert.Throws<ManagementException>(()=>completion.Apply(changed,result,index));
        Assert.Equal("Conflict",error.Code);
    }

    [Fact]
    public void DeclaredTypesAndDefinesAreResolvedAcrossFilesWithoutRunningInitializers(){
        var (source,position)=Input("class C {void M(){Dangerous.$$;}}");
        source=source with{Defines=["FEATURE"],Files=[..source.Files,new("Types.cs","#if FEATURE\nclass Dangerous {static Dangerous()=>throw new System.Exception();public static int Count=>throw new System.Exception();}\n#endif")]};
        var result=new CSharpCodeCompletion().Complete(source,"Code.cs",position);
        Assert.Contains(result.Items,item=>item.InsertText=="Count");
    }

    [Fact]
    public void CandidateCountIsBoundedAndCancellationIsHonored(){
        var members=string.Join(" ",Enumerable.Range(0,125).Select(index=>$"public int Value{index};"));
        var (source,position)=Input("class Many {"+members+"} class C {void M(){Many value=new();value.Val$$;}}");
        var completion=new CSharpCodeCompletion();
        var result=completion.Complete(source,"Code.cs",position);
        Assert.Equal(CSharpCodeCompletion.MaximumItems,result.Items.Length);
        Assert.True(result.Limited);
        using var cancelled=new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(()=>completion.Complete(source,"Code.cs",position,cancelled.Token));
    }

    private static (SourceSnapshot Source,int Position) Input(string marked){
        var position=marked.IndexOf("$$",StringComparison.Ordinal);
        Assert.True(position>=0);
        return(new("completion",[new("Code.cs",marked.Replace("$$","",StringComparison.Ordinal))],[]),position);
    }
}
