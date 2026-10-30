namespace Seamless.FLinq.Generator;

internal sealed class InlinedScalarFunc(Fragment scalarFragment):LambdaFunc {
    public Fragment ScalarFragment => scalarFragment;
    public string[] OuterParameterNames { get; init; } =Array.Empty<string>();
    public string BodyExpression { get; init; } = string.Empty;
    public string ScalarExpression { get; init; } = string.Empty;
    public string ScalarReturnTypeName { get; init; } = string.Empty;
    public string ScalarStaticMethods { get; init; } = string.Empty;

    public override string SupportingMemberSources {
        get {
            if(scalarFragment is Scalar scalar){
                return Fragment.JoinStaticMethods(this.ScalarStaticMethods,scalar.InlineSupportingMembers);
            }
            return Fragment.JoinStaticMethods(this.ScalarStaticMethods,scalarFragment.GetEnumeratorStruct);
        }
        set { }
    }

    private string? TryRenderInlineScalarStatement(string targetVar,string targetType,int number,params string[] arguments) {
        string? statement = null;
        if(scalarFragment is Scalar scalar) {
            statement=scalar.RenderInlineComputation(targetVar,targetType);
        }
        if(string.IsNullOrWhiteSpace(statement)&&scalarFragment is DirectExecutionFragment directExec) {
            statement=directExec.GetDirectExecutionStatement(targetVar,targetType);
        } else {
        }
        if(!string.IsNullOrWhiteSpace(statement)) {
            return ReplaceParameterReferences(statement!,this.OuterParameterNames,arguments);
        } else {
            return null;
        }
    }

    public override string RenderInlineAssign(string destination,int number,params string[] arguments) {
        var enumeratorName = $"__scalar_{number}_{SanitizeLocalName(destination)}";
        if(!string.IsNullOrWhiteSpace(this.BodyExpression)
            &&!string.IsNullOrWhiteSpace(this.ScalarExpression)
            &&this.BodyExpression!=this.ScalarExpression) {
            return this.RenderCompositeAssign(destination,number,arguments);
        }

        // RenderInlineComputation は出力先の変数を自分で宣言する。destination は呼び出し側が宣言済みなので、
        // 宣言し直すと同じスコープで二重定義になる。別名の変数へ計算させてから代入する。
        var directValueName = $"__scalarValue_{number}_{SanitizeLocalName(destination)}";
        if(this.TryRenderInlineScalarStatement(directValueName,this.ReturnTypeName,number,arguments) is { } directScalar) {
            // 一回のscalar計算のlocalとusing寿命をcallerへ漏らさない。
            return $$"""
                {
                    {{directScalar}}
                    {{destination}}={{directValueName}};
                }
                """;
        }

        var enumeratorExpression = this.RenderScalarEnumeratorExpression(arguments);
        var tempName = $"__scalarTmp_{number}";
        return $$"""
            using(var {{enumeratorName}}={{enumeratorExpression}}){
                {{enumeratorName}}.TryGetNext(out var {{tempName}});
                {{destination}}={{tempName}};
            }
            """;
    }

    // スカラーを式の前へ巻き上げると、条件分岐・短絡・左からの評価順が変わる。
    // 明示引数を取る static ローカル関数を元の式位置で呼び、closure の割当ても避ける。
    private string RenderCompositeAssign(string destination,int number,string[] arguments){
        var suffix=$"{number}_{SanitizeLocalName(destination)}";
        var function=$"__scalarCompute_{suffix}";
        var result=$"__scalarResult_{suffix}";
        var aliases=this.OuterParameterNames.Select((_,index)=>$"__scalarInput_{suffix}_{index}").ToArray();
        var parameters=aliases.Select((name,index)=>$"{this.ParameterTypeNames[index]} {name}").ToList();
        var calls=this.OuterParameterNames.ToList();
        var scalarType=this.ScalarReturnTypeName;
        var computation=this.TryRenderInlineScalarStatement(result,scalarType,number,aliases);
        if(computation is null){
            var enumerator=$"__scalarEnumerator_{suffix}";
            computation=$"{scalarType} {result}=default!;\nusing(var {enumerator}={this.RenderScalarEnumeratorExpression(aliases)}){{{enumerator}.TryGetNext(out {result});}}";
        }
        for(var index=0;index<this.CapturedLocals.Length;index++){
            var capture=this.CapturedLocals[index];
            var alias=$"__scalarCapture_{suffix}_{index}";
            parameters.Add($"{capture.TypeName} {alias}");
            calls.Add(capture.Name);
            computation=computation.Replace("this."+capture.Name,alias);
            computation=System.Text.RegularExpressions.Regex.Replace(computation,$@"(?<![\w.]){System.Text.RegularExpressions.Regex.Escape(capture.Name)}(?!\w)",alias);
        }
        var invocation=function+"("+string.Join(",",calls)+")";
        var body=this.BodyExpression.Replace(this.ScalarExpression,invocation);
        body=ReplaceParameterReferences(body,this.OuterParameterNames,arguments);
        return $$"""
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            static {{scalarType}} {{function}}({{string.Join(",",parameters)}}){
                {{computation}}
                return {{result}};
            }
            {{destination}}={{body}};
            """;
    }

    public override string RenderInlineExpression(params string[] arguments) {
        if(!string.IsNullOrWhiteSpace(this.InlineBody)) {
            return this.RenderInlineExpressionCore(arguments);
        }

        throw new InvalidOperationException("LINQ scalar lambda must be rendered as statements.");
    }

    private string RenderScalarEnumeratorExpression(params string[] arguments) {
        var expression = scalarFragment.GetEnumeratorExpression;
        return ReplaceParameterReferences(expression,this.OuterParameterNames,arguments);
    }
}
