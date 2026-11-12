namespace Seamless.FLinq.Generator;

internal sealed class ViewDefinitionRoot(int number,string source,string elementType,string ownerType,string ownerParameter,Enumerable definition,string supportingMethods)
    :Root(number,source,elementType,$"global::System.Collections.Generic.IEnumerator<{elementType}>",true){
    private Enumerable currentDefinition=definition;
    internal ViewProjectionMap? Projection{get;init;}
    internal string ProjectionReason{get;init;}="DirectColumnProjectionNotProven";
    public string ExpansionStatus{
        get{
            if(this.FusionStatus=="Applied"){
                return "InlinedIntoCaller";
            }else{
                return "ExpandedDefinitionWithSetBoundary";
            }
        }
    }
    internal string ViewElementType=>elementType;
    public string PredicatePushdownStatus{get;private set;}="NotRequested";
    public string PredicatePushdownReason{get;private set;}="NoLeadingWhere";
    public string PushedKeyMember{get;private set;}=string.Empty;
    private string DefinitionMethod=>$"{this.Struct}_ViewDefinition";
    private string ExpandedSource=>$"((global::Seamless.Database.View<{elementType}>)({source})).ExpandDefinition<{ownerType}>({this.DefinitionMethod})";
    public override string? DirectEnumerableExpression=>this.ExpandedSource;
    public override string StructMethod=>JoinStaticMethods(JoinStaticMethods(this.currentDefinition.StructMethod,supportingMethods),
        $"private static global::System.Collections.Generic.IEnumerable<{elementType}> {this.DefinitionMethod}({ownerType} {ownerParameter})=>{this.currentDefinition.GetEnumerableExpression};");
    public override string GetEnumeratorStruct=>this.currentDefinition.GetEnumeratorStruct+"\n"+base.GetEnumeratorStruct;
    public override string GetEnumeratorExpression=>RenderEnumeratorExpression(this.EnumeratorTypeName,this.ExpandedSource);
    protected override IEnumerable<Fragment> GetChildFragments(){yield return this.currentDefinition;}
    /// <summary>定義生成時に、factoryの名前などの追加引数が定数だと確認できたか。省いても利用者コードを落とさない。</summary>
    internal bool FactoryArgumentsConstant{get;init;}
    /// <summary>Viewの集合比較が、生成コードの通常のDistinctと同じ比較になると証明できたか。</summary>
    internal bool SetEqualityIsDefault{get;init;}
    /// <summary>匿名型の本体＋生成投影の形か。</summary>
    internal bool AnonymousIntermediate{get;init;}
    public string FusionStatus{get;private set;}="NotAttempted";
    public string FusionReason{get;private set;}=string.Empty;

    /// <summary>
    /// 定義の本体を呼出元のパイプラインへ融合する。成功すると、定義の根(所有DBを呼出元の受け手へ置換)と、
    /// 定義の段＋集合境界のDistinctを返す。View実体・getter・factory・delegateは生成コードに残らない。
    /// </summary>
    internal bool TryFuse(ref int nextNumber,out Enumerable root,out List<PipelineStage> stages,out string methods){
        root=null!;stages=null!;methods=string.Empty;
        bool Reject(string reason){this.FusionStatus="NotApplied";this.FusionReason=reason;return false;}
        if(!this.FactoryArgumentsConstant){return Reject("ViewFactoryArgumentsNotConstant");}
        if(!this.SetEqualityIsDefault){return Reject("SetEqualityNotProvenDefault");}
        // 匿名型の中間行(登録型への置換)を外側のパイプラインへ移すと生成が失敗する(2026-11-11観測、原因未特定)。融合しない。
        if(this.AnonymousIntermediate){return Reject("AnonymousIntermediateNotFusedYet");}
        if(this.currentDefinition is not PipelineEnumerable pipeline||pipeline.Stages.Count==0){return Reject("DefinitionIsNotAPipeline");}
        foreach(var stage in pipeline.Stages){
            if(stage.SecondSource is not null){return Reject("DefinitionJoinSourceNotFused");}
            foreach(var callback in new[]{stage.Func,stage.SecondFunc,stage.ThirdFunc}){
                if(callback is null){
                    continue;
                }else{
                }
                if(callback.CapturedLocals.Length>0||callback.HoistedLocals.Length>0){return Reject("DefinitionStageCapturesValues");}
            }
        }
        var receiver=System.Text.RegularExpressions.Regex.Match(source.Trim(),@"^\(*(?<receiver>this|@?[A-Za-z_][A-Za-z0-9_]*)\)*\.@?[A-Za-z_][A-Za-z0-9_]*$");
        if(!receiver.Success){return Reject("ViewReceiverIsNotASimpleName");}
        var prefix=new List<PipelineStage>();
        var innerMethods=string.Empty;
        SourceRootBase innerRoot;
        if(pipeline.Source is ViewDefinitionRoot nested){
            if(!nested.TryFuse(ref nextNumber,out var nestedRoot,out var nestedStages,out innerMethods)){return Reject("NestedView:"+nested.FusionReason);}
            innerRoot=(SourceRootBase)nestedRoot;
            prefix.AddRange(nestedStages);
        }else if(pipeline.Source is TransactionRowsRoot rows){
            innerRoot=rows;
        }else if(pipeline.Source is Root plain&&plain.GetType()==typeof(Root)&&plain.CanTryProvideSpan){
            innerRoot=plain;
        }else{
            return Reject("DefinitionSourceShapeNotFused");
        }
        var owner=ownerParameter.TrimStart('@');
        var ownerToken=new System.Text.RegularExpressions.Regex(@"(?<![A-Za-z0-9_@.])@?"+System.Text.RegularExpressions.Regex.Escape(owner)+@"(?![A-Za-z0-9_])");
        if(ownerToken.Matches(innerRoot.SourceExpression).Count!=1){return Reject("OwnerReferenceIsNotTheSingleRootReceiver");}
        var rootSource=ownerToken.Replace(innerRoot.SourceExpression,receiver.Groups["receiver"].Value);
        if(innerRoot is TransactionRowsRoot rowsRoot){
            var fusedRows=rowsRoot.WithSource(nextNumber++,rootSource,rowsRoot.ElementType,$"global::System.Collections.Generic.IEnumerator<{rowsRoot.ElementType}>");
            fusedRows.FusedView=this.Decision();
            root=fusedRows;
        }else{
            root=new FusedViewRoot(nextNumber++,rootSource,innerRoot.ElementType,this.Decision());
        }
        if(pipeline.Stages[0].Func is{} first){first.ExecutionNote="[View/融合] "+elementType+" の定義を呼出元へ展開。集合境界はDistinct段で維持。";}
        stages=[..prefix,..pipeline.Stages,new PipelineStage(PipelineStageKind.Distinct,null,"null",elementType,elementType,keyType:elementType)];
        methods=JoinStaticMethods(innerMethods,supportingMethods);
        this.FusionStatus="Applied";
        this.FusionReason="DefinitionInlinedWithDistinctSetBoundary";
        return true;
    }
    // 融合後の根はViewDefinitionRootを子に持たない(計画の反射走査で重複ノードになるため)。判断だけを値として渡す。
    private FusedViewDecision Decision()=>new("InlinedIntoCaller",this.ViewElementType,"Applied","DefinitionInlinedWithDistinctSetBoundary",this.PredicatePushdownStatus,this.PredicatePushdownReason,this.PushedKeyMember);
    internal void RejectPredicate(string reason){this.PredicatePushdownStatus="NotApplied";this.PredicatePushdownReason=reason;}
    internal bool PushPredicate(FuncFragment predicate,ref int nextNumber,ISymbol member){
        if(this.currentDefinition is not PipelineEnumerable pipeline||pipeline.Stages.Count==0||pipeline.Stages[pipeline.Stages.Count-1].Kind!=PipelineStageKind.Select
            ||pipeline.Stages.Take(pipeline.Stages.Count-1).Any(static stage=>stage.Kind!=PipelineStageKind.Where||stage.ContainsAnchor||stage.Func is{RequiresStatementInlining:true})){
            this.RejectPredicate("OnlyWhereThenDirectSelectIsSupported");return false;
        }
        var stages=pipeline.Stages.ToList();
        stages.Insert(stages.Count-1,new(PipelineStageKind.Where,predicate,null,this.Projection!.InputType,this.Projection.InputType));
        var replacement=new PipelineEnumerable(nextNumber++,pipeline.Source,stages,pipeline.ElementType);
        replacement.ApplyRenderContext(this.RenderContext);
        this.currentDefinition=replacement;
        this.PredicatePushdownStatus="Applied";
        this.PredicatePushdownReason="DirectKeyFilterBeforeProjection;PointLookupNotSelected";
        this.PushedKeyMember=member.ToDisplayString();
        return true;
    }
    public override string GetConsumeExecutionStatement(string consumerName)=>$$"""
        {
            var __viewSource{{number}}={{this.ExpandedSource}};
            using var __viewEnumerator{{number}}=__viewSource{{number}}.GetEnumerator();
            while(__viewEnumerator{{number}}.MoveNext()){
                var __viewItem{{number}}=__viewEnumerator{{number}}.Current;
                {{consumerName}}.Consume(__viewItem{{number}});
            }
        }
        """;
}

/// <summary>融合時点のビューの判断。計画に記録する。</summary>
internal sealed record FusedViewDecision(string ExpansionStatus,string ViewElementType,string FusionStatus,string FusionReason,string PredicatePushdownStatus,string PredicatePushdownReason,string PushedKeyMember);

/// <summary>呼出元へ融合したビュー定義の根。所有DBは呼出元の受け手へ置換済みで、集合境界は後続のDistinct段が担う。</summary>
internal sealed class FusedViewRoot(int number,string source,string elementType,FusedViewDecision view)
    :Root(number,source,elementType,$"global::System.Collections.Generic.IEnumerator<{elementType}>",true){
    internal FusedViewDecision View=>view;
}

internal sealed class OpaqueViewRoot(int number,string source,string elementType,string reason)
    :Root(number,source,elementType,$"global::System.Collections.Generic.IEnumerator<{elementType}>",true){
    public string ExpansionStatus=>"NotExpanded";
    public string ExpansionReason=>reason;
}
