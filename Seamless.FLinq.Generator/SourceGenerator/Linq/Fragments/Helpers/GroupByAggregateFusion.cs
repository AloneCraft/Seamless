using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Seamless.FLinq.Generator;

internal enum GroupAggregateKind {
    Count,
    LongCount,
    Sum,
    Min,
    Max,
    Average,
}

internal sealed class GroupAggregateItem {
    public GroupAggregateKind Kind { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string AccumulatorFieldType { get; set; } = string.Empty;
    public string ResultExpressionType { get; set; } = string.Empty;
    public FuncFragment? InnerFunc { get; set; }
    public string OriginalInvocationText { get; set; } = string.Empty;
    public string RawInvocationText { get; set; } = string.Empty;
}

internal sealed class GroupByAggregatePlan {
    public string KeyType { get; set; } = string.Empty;
    public string AccumulatorType { get; set; } = string.Empty;
    public List<GroupAggregateItem> Items { get; } = new();
    public string ProjectedResultExpression { get; set; } = string.Empty;
    public string ResultType { get; set; } = string.Empty;
    public bool IsSingleField => this.Items.Count==1;

    // 後ろの Take(n)・First・Any などが先頭の n グループしか使わないとき、その n。null なら全グループを集約する。
    // System.Linq は集約のラムダを、使うグループの分しか評価しないので、それを超えるグループの要素は集約しない(キー選択子と要素選択子は全要素で評価する)。
    public int? GroupLimit { get; set; }

    // グループのアキュムレータを探して(必要なら作って)、要素を集約する文。GroupLimit があるときは、上限に達した後の新しいグループを作らず、その要素の集約を飛ばす。
    public string RenderAccumulate(string mapExpression,string keyName,string elementVarName,string indent) {
        if(this.GroupLimit is not { } limit) {
            return $"{indent}ref var __acc=ref {mapExpression}.GetValueRefOrAddDefault({keyName},out var __exists);\n"+this.RenderAccumulatorUpdate("__acc",elementVarName,indent);
        }
        var sb = new StringBuilder();
        sb.AppendLine($"{indent}ref var __acc=ref {mapExpression}.GetValueRefOrNullRef({keyName});");
        sb.AppendLine($"{indent}var __exists=!global::System.Runtime.CompilerServices.Unsafe.IsNullRef(ref __acc);");
        sb.AppendLine($"{indent}if(!__exists&&{mapExpression}.Count<{limit}) {{");
        sb.AppendLine($"{indent}    __acc=ref {mapExpression}.GetValueRefOrAddDefault({keyName},out _);");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine($"{indent}if(!global::System.Runtime.CompilerServices.Unsafe.IsNullRef(ref __acc)) {{");
        sb.AppendLine(this.RenderAccumulatorUpdate("__acc",elementVarName,indent+"    "));
        sb.Append($"{indent}}}");
        return sb.ToString();
    }

    public string RenderAccumulatorUpdate(string accRefName,string elementVarName,string indent) {
        var sb = new StringBuilder();
        for(var i = 0;i<this.Items.Count;i++) {
            var item = this.Items[i];
            string fieldAccess;
            if(this.IsSingleField){
                fieldAccess=accRefName;
            }else{
                fieldAccess=$"{accRefName}.{item.FieldName}";
            }
            var valueExpr = elementVarName;
            if(item.InnerFunc is not null) {
                var innerVar = $"__aggVal_{i}";
                sb.AppendLine($"{indent}{item.InnerFunc.ReturnTypeName} {innerVar}=default!;");
                sb.AppendLine(PipelineEnumerable.IndentBlock(item.InnerFunc.RenderInlineAssign(innerVar,i,elementVarName),indent));
                valueExpr=innerVar;
            }
            switch(item.Kind) {
                case GroupAggregateKind.Count:
                    if(item.InnerFunc is not null) {
                        sb.AppendLine($"{indent}if({valueExpr}) {fieldAccess}++;");
                    } else {
                        sb.AppendLine($"{indent}{fieldAccess}++;");
                    }
                    break;
                case GroupAggregateKind.LongCount:
                    if(item.InnerFunc is not null) {
                        sb.AppendLine($"{indent}if({valueExpr}) {fieldAccess}++;");
                    } else {
                        sb.AppendLine($"{indent}{fieldAccess}++;");
                    }
                    break;
                case GroupAggregateKind.Sum:
                    sb.AppendLine($"{indent}{fieldAccess}+={valueExpr};");
                    break;
                case GroupAggregateKind.Min:
                    sb.AppendLine($"{indent}if(!__exists || {valueExpr} < {fieldAccess}) {fieldAccess}={valueExpr};");
                    break;
                case GroupAggregateKind.Max:
                    sb.AppendLine($"{indent}if(!__exists || {valueExpr} > {fieldAccess}) {fieldAccess}={valueExpr};");
                    break;
                case GroupAggregateKind.Average:
                    sb.AppendLine($"{indent}{fieldAccess}.Sum+={valueExpr};");
                    sb.AppendLine($"{indent}{fieldAccess}.Count++;");
                    break;
            }
        }
        return sb.ToString().TrimEnd();
    }
}

internal static class GroupByAggregateFusion {
    public static bool TryAnalyzeSelect(
        LambdaExpressionSyntax selectLambda,
        FuncFragment selectFunc,
        SemanticModel semanticModel,
        string keyType,
        string groupElementType,
        PipelineBuildServices services,
        ref int uniqueNumber,
        out GroupByAggregatePlan plan) {
        plan=null!;
        var groupParamName = selectLambda switch {
            SimpleLambdaExpressionSyntax simple => simple.Parameter.Identifier.ValueText,
            ParenthesizedLambdaExpressionSyntax parenthesized when parenthesized.ParameterList.Parameters.Count==1 => parenthesized.ParameterList.Parameters[0].Identifier.ValueText,
            _ => null,
        };
        if(groupParamName is null) {
            return false;
        }

        ExpressionSyntax? bodyExpr = selectLambda.Body as ExpressionSyntax;
        if(bodyExpr is null) {
            return false;
        }

        return TryAnalyzeBody(bodyExpr,selectFunc,groupParamName,null,semanticModel,keyType,groupElementType,services,ref uniqueNumber,out plan);
    }

    public static bool TryAnalyzeResultSelector(
        LambdaExpressionSyntax resultLambda,
        FuncFragment resultFunc,
        SemanticModel semanticModel,
        string keyType,
        string groupElementType,
        PipelineBuildServices services,
        ref int uniqueNumber,
        out GroupByAggregatePlan plan) {
        plan=null!;
        if(resultLambda is not ParenthesizedLambdaExpressionSyntax parenthesized
            ||parenthesized.ParameterList.Parameters.Count!=2) {
            return false;
        }

        var keyParamName = parenthesized.ParameterList.Parameters[0].Identifier.ValueText;
        var groupParamName = parenthesized.ParameterList.Parameters[1].Identifier.ValueText;

        ExpressionSyntax? bodyExpr = resultLambda.Body as ExpressionSyntax;
        if(bodyExpr is null) {
            return false;
        }

        return TryAnalyzeBody(bodyExpr,resultFunc,groupParamName,keyParamName,semanticModel,keyType,groupElementType,services,ref uniqueNumber,out plan);
    }

    private static bool TryAnalyzeBody(
        ExpressionSyntax bodyExpr,
        FuncFragment funcFragment,
        string groupParamName,
        string? keyParamName,
        SemanticModel semanticModel,
        string keyType,
        string groupElementType,
        PipelineBuildServices services,
        ref int uniqueNumber,
        out GroupByAggregatePlan plan) {
        plan=null!;

        // Check all references to groupParamName
        var idNodes = bodyExpr.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
            .Where(id => id.Identifier.ValueText==groupParamName)
            .ToList();
        if(idNodes.Count==0) {
            return false; // ResultIgnoresGroup handles this case via distinct
        }

        var resultTypeSymbol = semanticModel.GetTypeInfo(bodyExpr).ConvertedType
            ??semanticModel.GetTypeInfo(bodyExpr).Type;
        if(resultTypeSymbol is not null&&services.ContainsAnonymousType(resultTypeSymbol)) {
            return false;
        }

        var aggPlan = new GroupByAggregatePlan {
            KeyType=keyType,
            ResultType=resultTypeSymbol?.ToDisplayString(services.FullyQualifiedFormat)??"object",
        };

        var invocationReplacements = new Dictionary<string,string>(StringComparer.Ordinal);

        foreach(var idNode in idNodes) {
            // Case 1: g.Key
            if(idNode.Parent is MemberAccessExpressionSyntax memberAccess
                &&memberAccess.Expression==idNode
                &&memberAccess.Name.Identifier.ValueText=="Key") {
                continue;
            }

            // Case 2: invocation of Count, Sum, Min, Max, Average on g
            // Can be member access g.Sum(...) or Enumerable.Sum(g, ...)
            InvocationExpressionSyntax? invocation = null;
            if(idNode.Parent is MemberAccessExpressionSyntax ma && ma.Expression==idNode && ma.Parent is InvocationExpressionSyntax inv1) {
                invocation=inv1;
            } else if(idNode.Parent is ArgumentSyntax arg && arg.Expression==idNode && arg.Parent is ArgumentListSyntax argList && argList.Parent is InvocationExpressionSyntax inv2) {
                invocation=inv2;
            }

            if(invocation is null) {
                return false; // Group parameter used in unsupported way
            }

            if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method
                ||(method.ReducedFrom??method).ContainingType?.ToDisplayString()!="System.Linq.Enumerable") {
                return false;
            }

            var methodName = (method.ReducedFrom??method).Name;
            if(!TryMapAggregateKind(methodName,out var aggKind)) {
                return false;
            }

            var item = new GroupAggregateItem {
                Kind=aggKind,
                FieldName=$"Agg{aggPlan.Items.Count}",
                OriginalInvocationText=services.RenderExpression(invocation),
                RawInvocationText=invocation.ToString(),
            };

            // Check arguments of aggregation method
            var regularArgs = invocation.ArgumentList.Arguments;
            var isExtension = method.ReducedFrom is not null;
            int actualArgStartIndex;
            if(isExtension){
                actualArgStartIndex=0;
            }else{
                actualArgStartIndex=1;
            }
            var numInnerArgs = regularArgs.Count-actualArgStartIndex;

            if(numInnerArgs>1) {
                return false;
            }

            if(numInnerArgs==1) {
                var innerArgExpr = regularArgs[actualArgStartIndex].Expression;
                var paramType = method.Parameters[actualArgStartIndex].Type;
                if(!services.BuildFunc(innerArgExpr,paramType,ref uniqueNumber,out var innerFunc)
                    ||!services.CanUseFunc(innerFunc)
                    ||innerFunc.ContainsAnchor) {
                    return false;
                }
                item.InnerFunc=innerFunc;
            }

            DetermineTypes(item,groupElementType,services);
            aggPlan.Items.Add(item);
        }

        if(aggPlan.Items.Count==0) {
            return false;
        }

        // Determine AccumulatorType
        if(aggPlan.Items.Count==1) {
            aggPlan.AccumulatorType=aggPlan.Items[0].AccumulatorFieldType;
        }else{
            var fields = string.Join(",",aggPlan.Items.Select(item => $"{item.AccumulatorFieldType} {item.FieldName}"));
            aggPlan.AccumulatorType=$"({fields})";
        }

        // Render ProjectedResultExpression
        var bodyText = services.RenderExpression(bodyExpr);

        // Replace g.Key (and keyParamName if present) with __key
        bodyText=System.Text.RegularExpressions.Regex.Replace(
            bodyText,
            $@"\b{groupParamName}\.Key\b",
            "__key",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        if(!string.IsNullOrEmpty(keyParamName)){
            bodyText=System.Text.RegularExpressions.Regex.Replace(
                bodyText,
                $@"\b{keyParamName}\b",
                "__key",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        }

        // Replace each aggregate invocation with acc expression
        for(var i = 0;i<aggPlan.Items.Count;i++) {
            var item = aggPlan.Items[i];
            string fieldAccess;
            if(aggPlan.IsSingleField){
                fieldAccess="__acc";
            }else{
                fieldAccess=$"__acc.{item.FieldName}";
            }
            string replacementExpr;
            if(item.Kind==GroupAggregateKind.Average){
                replacementExpr=$"(({item.ResultExpressionType})({fieldAccess}.Sum)/{fieldAccess}.Count)";
            }else{
                replacementExpr=fieldAccess;
            }
            var normInv = item.OriginalInvocationText;
            bodyText=bodyText.Replace(normInv,replacementExpr,StringComparison.Ordinal);
            if(!string.IsNullOrEmpty(item.RawInvocationText)&&!string.Equals(normInv,item.RawInvocationText,StringComparison.Ordinal)){
                bodyText=bodyText.Replace(item.RawInvocationText,replacementExpr,StringComparison.Ordinal);
            }
        }

        // Replace hoisted and captured local expressions with this. receivers
        if(funcFragment is not null){
            foreach(var hoisted in funcFragment.HoistedLocals){
                bodyText=bodyText.Replace(hoisted.ExpressionText,$"this.{hoisted.Name}",StringComparison.Ordinal);
                bodyText=System.Text.RegularExpressions.Regex.Replace(
                    bodyText,
                    $@"(?<![\w\.]){System.Text.RegularExpressions.Regex.Escape(hoisted.Name)}\b",
                    $"this.{hoisted.Name}",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            }
            foreach(var captured in funcFragment.CapturedLocals){
                bodyText=System.Text.RegularExpressions.Regex.Replace(
                    bodyText,
                    $@"(?<![\w\.]){System.Text.RegularExpressions.Regex.Escape(captured.Name)}\b",
                    $"this.{captured.Name}",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            }
        }

        if(System.Text.RegularExpressions.Regex.IsMatch(bodyText,$@"\b{groupParamName}\b")) {
            return false;
        }

        aggPlan.ProjectedResultExpression=bodyText;
        plan=aggPlan;
        return true;
    }

    private static bool TryMapAggregateKind(string methodName,out GroupAggregateKind kind) {
        switch(methodName) {
            case "Count":
                kind=GroupAggregateKind.Count;
                return true;
            case "LongCount":
                kind=GroupAggregateKind.LongCount;
                return true;
            case "Sum":
                kind=GroupAggregateKind.Sum;
                return true;
            case "Min":
                kind=GroupAggregateKind.Min;
                return true;
            case "Max":
                kind=GroupAggregateKind.Max;
                return true;
            case "Average":
                kind=GroupAggregateKind.Average;
                return true;
            default:
                kind=default;
                return false;
        }
    }

    private static void DetermineTypes(GroupAggregateItem item,string groupElementType,PipelineBuildServices services) {
        var inputType = item.InnerFunc?.ReturnTypeName??groupElementType;
        switch(item.Kind) {
            case GroupAggregateKind.Count:
                item.AccumulatorFieldType="int";
                item.ResultExpressionType="int";
                break;
            case GroupAggregateKind.LongCount:
                item.AccumulatorFieldType="long";
                item.ResultExpressionType="long";
                break;
            case GroupAggregateKind.Sum:
                item.AccumulatorFieldType=inputType;
                item.ResultExpressionType=inputType;
                break;
            case GroupAggregateKind.Min:
            case GroupAggregateKind.Max:
                item.AccumulatorFieldType=inputType;
                item.ResultExpressionType=inputType;
                break;
            case GroupAggregateKind.Average:
                var sumType = inputType switch {
                    "int" or "short" or "byte" => "long",
                    "float" => "float",
                    "decimal" => "decimal",
                    _ => "double",
                };
                item.AccumulatorFieldType=$"({sumType} Sum, long Count)";
                item.ResultExpressionType=inputType switch {
                    "decimal" => "decimal",
                    "float" => "float",
                    _ => "double",
                };
                break;
        }
    }
}
