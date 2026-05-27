namespace Seamless.SourceGenerator.Linq.Fragments;

public sealed class InlinedScalarFunc(Fragment scalarFragment) : LambdaFunc {
    public Fragment ScalarFragment => scalarFragment;
    public string[] OuterParameterNames { get; init; } = [];
    public string BodyExpression { get; init; } = string.Empty;
    public string ScalarExpression { get; init; } = string.Empty;
    public string ScalarReturnTypeName { get; init; } = string.Empty;
    public string ScalarStaticMethods { get; init; } = string.Empty;

    public override string SupportingMemberSources {
        get => Fragment.JoinStaticMethods(ScalarStaticMethods, scalarFragment.GetEnumeratorStruct);
        set { }
    }

    public override string RenderInlineAssign(string destination,int number,params string[] arguments) {
        var enumeratorName = $"__scalar_{number}_{SanitizeLocalName(destination)}";
        var enumeratorExpression = RenderScalarEnumeratorExpression(arguments);
        if(!string.IsNullOrWhiteSpace(BodyExpression)
            && !string.IsNullOrWhiteSpace(ScalarExpression)
            && BodyExpression != ScalarExpression) {
            var scalarValueName = $"__scalarValue_{number}_{SanitizeLocalName(destination)}";
            var scalarType = string.IsNullOrWhiteSpace(ScalarReturnTypeName) ? "var" : ScalarReturnTypeName;
            var rewrittenBody = BodyExpression.Replace(ScalarExpression, scalarValueName);
            rewrittenBody = ReplaceParameterReferences(rewrittenBody,OuterParameterNames,arguments);
            return $$"""
                {{scalarType}} {{scalarValueName}} = default!;
                using(var {{enumeratorName}} = {{enumeratorExpression}}) {
                    {{enumeratorName}}.TryGetNext(out {{scalarValueName}});
                }
                {{destination}}={{rewrittenBody}};
                """;
        }

        return $$"""
            using(var {{enumeratorName}} = {{enumeratorExpression}}) {
                {{enumeratorName}}.TryGetNext(out {{destination}});
            }
            """;
    }

    public override string RenderInlineExpression(params string[] arguments) {
        if(!string.IsNullOrWhiteSpace(InlineBody)) {
            return RenderInlineExpressionCore(arguments);
        }

        throw new InvalidOperationException("LINQ scalar lambda must be rendered as statements.");
    }

    private string RenderScalarEnumeratorExpression(params string[] arguments) {
        var expression = scalarFragment.GetEnumeratorExpression;
        return ReplaceParameterReferences(expression,OuterParameterNames,arguments);
    }
}
