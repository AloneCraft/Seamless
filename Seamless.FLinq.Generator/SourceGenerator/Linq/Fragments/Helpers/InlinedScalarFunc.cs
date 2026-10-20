namespace Seamless.FLinq.Generator;

internal sealed class InlinedScalarFunc(Fragment scalarFragment):LambdaFunc {
    public Fragment ScalarFragment => scalarFragment;
    public string[] OuterParameterNames { get; init; } =Array.Empty<string>();
    public string BodyExpression { get; init; } = string.Empty;
    public string ScalarExpression { get; init; } = string.Empty;
    public string ScalarReturnTypeName { get; init; } = string.Empty;
    public string ScalarStaticMethods { get; init; } = string.Empty;

    public override string SupportingMemberSources {
        get => Fragment.JoinStaticMethods(this.ScalarStaticMethods,scalarFragment.GetEnumeratorStruct);
        set { }
    }

    private string? TryRenderInlineScalarStatement(string targetVar,string targetType,int number,params string[] arguments) {
        string? statement = null;
        if(scalarFragment is Scalar scalar) {
            statement=scalar.RenderInlineComputation(targetVar,targetType);
        } else if(scalarFragment is DirectExecutionFragment directExec) {
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
        var enumeratorExpression = this.RenderScalarEnumeratorExpression(arguments);
        if(!string.IsNullOrWhiteSpace(this.BodyExpression)
            &&!string.IsNullOrWhiteSpace(this.ScalarExpression)
            &&this.BodyExpression!=this.ScalarExpression) {
            var scalarValueName = $"__scalarValue_{number}_{SanitizeLocalName(destination)}";
            string scalarType;
            if(string.IsNullOrWhiteSpace(this.ScalarReturnTypeName)) {
                scalarType="var";
            } else {
                scalarType=this.ScalarReturnTypeName;
            }
            var rewrittenBody = this.BodyExpression.Replace(this.ScalarExpression,scalarValueName);
            rewrittenBody=ReplaceParameterReferences(rewrittenBody,this.OuterParameterNames,arguments);
            if(this.TryRenderInlineScalarStatement(scalarValueName,scalarType,number,arguments) is { } inlinedScalar) {
                return $$"""
                    {{inlinedScalar}}
                    {{destination}}={{rewrittenBody}};
                    """;
            }
            return $$"""
                {{scalarType}} {{scalarValueName}}=default!;
                using(var {{enumeratorName}}={{enumeratorExpression}}){
                    {{enumeratorName}}.TryGetNext(out {{scalarValueName}});
                }
                {{destination}}={{rewrittenBody}};
                """;
        }

        if(this.TryRenderInlineScalarStatement(destination,this.ReturnTypeName,number,arguments) is { } directScalar) {
            return directScalar;
        }

        var tempName = $"__scalarTmp_{number}";
        return $$"""
            using(var {{enumeratorName}}={{enumeratorExpression}}){
                {{enumeratorName}}.TryGetNext(out var {{tempName}});
                {{destination}}={{tempName}};
            }
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
