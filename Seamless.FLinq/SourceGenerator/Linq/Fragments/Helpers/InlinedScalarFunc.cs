namespace Seamless.FLinq.Linq.Fragments;

internal sealed class InlinedScalarFunc(Fragment scalarFragment):LambdaFunc{
    public Fragment ScalarFragment=>scalarFragment;
    public string[] OuterParameterNames{get;init;}=[];
    public string BodyExpression{get;init;}=string.Empty;
    public string ScalarExpression{get;init;}=string.Empty;
    public string ScalarReturnTypeName{get;init;}=string.Empty;
    public string ScalarStaticMethods{get;init;}=string.Empty;

    public override string SupportingMemberSources{
        get=>Fragment.JoinStaticMethods(this.ScalarStaticMethods,scalarFragment.GetEnumeratorStruct);
        set{}
    }

    public override string RenderInlineAssign(string destination,int number,params string[] arguments){
        var enumeratorName=$"__scalar_{number}_{SanitizeLocalName(destination)}";
        var enumeratorExpression= this.RenderScalarEnumeratorExpression(arguments);
        if(!string.IsNullOrWhiteSpace(this.BodyExpression)
            && !string.IsNullOrWhiteSpace(this.ScalarExpression)
            &&this.BodyExpression!=this.ScalarExpression){
            var scalarValueName=$"__scalarValue_{number}_{SanitizeLocalName(destination)}";
            var scalarType=string.IsNullOrWhiteSpace(this.ScalarReturnTypeName) ? "var" : this.ScalarReturnTypeName;
            var rewrittenBody= this.BodyExpression.Replace(this.ScalarExpression,scalarValueName);
            rewrittenBody=ReplaceParameterReferences(rewrittenBody,this.OuterParameterNames,arguments);
            return $$"""
                {{scalarType}} {{scalarValueName}}=default!;
                using(var {{enumeratorName}}={{enumeratorExpression}}){
                    {{enumeratorName}}.TryGetNext(out {{scalarValueName}});
                }
                {{destination}}={{rewrittenBody}};
                """;
        }

        var tempName=$"__scalarTmp_{number}";
        return $$"""
            using(var {{enumeratorName}}={{enumeratorExpression}}){
                {{enumeratorName}}.TryGetNext(out var {{tempName}});
                {{destination}}={{tempName}};
            }
            """;
    }

    public override string RenderInlineExpression(params string[] arguments){
        if(!string.IsNullOrWhiteSpace(this.InlineBody)){
            return this.RenderInlineExpressionCore(arguments);
        }

        throw new InvalidOperationException("LINQ scalar lambda must be rendered as statements.");
    }

    private string RenderScalarEnumeratorExpression(params string[] arguments){
        var expression=scalarFragment.GetEnumeratorExpression;
        return ReplaceParameterReferences(expression,this.OuterParameterNames,arguments);
    }
}
