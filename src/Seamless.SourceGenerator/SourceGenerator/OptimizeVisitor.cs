using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Seamless.SourceGenerator;

// method-chain を翻訳しやすい形に正規化する syntax rewriter。
// 主目的は Where をできるだけ source 側へ押し下げ、後段の generated code を素直な chain にすること。
// 解析できないケースでは式を変えずに返すので、安全側に倒す設計になっている。
internal sealed class OptimizeVisitor:CSharpSyntaxRewriter {
    private static readonly HashSet<string> PushWhereThroughUnaryMethods = new(StringComparer.Ordinal) {
        "Distinct",
        "DistinctBy",
        "Order",
        "OrderBy",
        "OrderDescending",
        "OrderByDescending",
        "Reverse",
    };

    private static readonly HashSet<string> PushWhereThroughFirstSourceMethods = new(StringComparer.Ordinal) {
        "Except",
        "ExceptBy",
        "Intersect",
        "IntersectBy",
    };

    private static readonly HashSet<string> PushWhereThroughBothSourceMethods = new(StringComparer.Ordinal) {
        "Concat",
        "Union",
    };

    // public entry point。呼び出し側は visitor の内部状態を意識せず、式 1 つを投げればよい。
    public static ExpressionSyntax Optimize(ExpressionSyntax expression) {
        return (ExpressionSyntax)new OptimizeVisitor().Visit(expression)!;
    }

    // 子ノードを先に処理し、その結果でもう一度自分を最適化する。
    // 1 回の押し下げで新しい押し下げ機会が生まれるので、変化がある限り再訪する。
    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node) {
        var rewritten = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;
        var optimized = TryOptimizeInvocation(rewritten);
        if(optimized is null||SyntaxFactory.AreEquivalent(optimized,rewritten)) {
            return rewritten;
        }
        return VisitInvocationExpression(optimized);
    }

    // 最適化対象は「Where の直下に別の sequence call がある」形だけ。
    // 上流メソッドの種類ごとに安全な押し下げ先が違うので、ここで戦略を分岐する。
    private static InvocationExpressionSyntax? TryOptimizeInvocation(InvocationExpressionSyntax invocation) {
        if(!TryGetSequenceCallInfo(invocation,out var whereCall)
            ||whereCall.MethodName!="Where"
            ||whereCall.Arguments.Count!=1
            ||whereCall.Arguments[0].Expression is not LambdaExpressionSyntax whereLambda
            ||whereCall.Source is not InvocationExpressionSyntax upstreamInvocation
            ||!TryGetSequenceCallInfo(upstreamInvocation,out var upstreamCall)) {
            return null;
        }

        if(upstreamCall.MethodName=="Select") {
            return TryPushWhereThroughSelect(upstreamCall,whereLambda);
        }
        if(upstreamCall.MethodName=="SelectMany") {
            return TryPushWhereThroughSelectMany(upstreamCall,whereLambda);
        }
        if(upstreamCall.MethodName=="Join"||upstreamCall.MethodName=="LeftJoin") {
            return TryPushWhereThroughBinaryCarrier(upstreamCall,whereLambda,resultSelectorArgumentIndex: 3);
        }
        if(PushWhereThroughUnaryMethods.Contains(upstreamCall.MethodName)) {
            return TryPushWhereThroughUnary(upstreamCall,whereLambda);
        }
        if(PushWhereThroughFirstSourceMethods.Contains(upstreamCall.MethodName)) {
            return TryPushWhereThroughFirstSource(upstreamCall,whereLambda);
        }
        if(PushWhereThroughBothSourceMethods.Contains(upstreamCall.MethodName)) {
            return TryPushWhereThroughBothSources(upstreamCall,whereLambda);
        }
        return null;
    }

    // Select の結果 carrier から predicate を持ち上げて、元 source の parameter に戻す。
    private static InvocationExpressionSyntax? TryPushWhereThroughSelect(SequenceCallInfo selectCall,LambdaExpressionSyntax whereLambda) {
        if(selectCall.Arguments.Count<1||selectCall.Arguments[0].Expression is not LambdaExpressionSyntax selectorLambda) {
            return null;
        }
        if(!TryLiftPredicate(whereLambda,selectorLambda,out var liftedBody,out var referencedParameterNames)) {
            return null;
        }

        var selectorParameters = GetLambdaParameterNames(selectorLambda);
        if(selectorParameters.Count!=1) {
            return null;
        }
        if(referencedParameterNames.Count>1) {
            return null;
        }
        if(referencedParameterNames.Count==1&&!referencedParameterNames.Contains(selectorParameters[0])) {
            return null;
        }

        var pushedPredicate = RecreateLambda(selectorLambda,liftedBody);
        var newSource = CreateSequenceCall(selectCall.Source,"Where",pushedPredicate);
        return RebuildCall(selectCall,newSource,selectCall.Arguments.Select(static argument => argument.Expression));
    }

    // SelectMany は outer / inner のどちらだけを見ている predicate かを判定して押し下げる。
    private static InvocationExpressionSyntax? TryPushWhereThroughSelectMany(SequenceCallInfo selectManyCall,LambdaExpressionSyntax whereLambda) {
        if(selectManyCall.Arguments.Count<2
            ||selectManyCall.Arguments[0].Expression is not LambdaExpressionSyntax collectionSelector
            ||selectManyCall.Arguments[1].Expression is not LambdaExpressionSyntax resultSelector) {
            return null;
        }
        if(!TryLiftPredicate(whereLambda,resultSelector,out var liftedBody,out var referencedParameterNames)) {
            return null;
        }

        var resultSelectorParameters = GetLambdaParameterNames(resultSelector);
        if(resultSelectorParameters.Count!=2) {
            return null;
        }

        if(referencedParameterNames.Count==0||referencedParameterNames.SetEquals(new[] { resultSelectorParameters[0] })) {
            var outerPredicate = RecreateLambdaWithParameter(resultSelectorParameters[0],liftedBody);
            var newSource = CreateSequenceCall(selectManyCall.Source,"Where",outerPredicate);
            return RebuildCall(selectManyCall,newSource,selectManyCall.Arguments.Select(static argument => argument.Expression));
        }

        if(!referencedParameterNames.SetEquals(new[] { resultSelectorParameters[1] })) {
            return null;
        }

        if(!TryGetLambdaBody(collectionSelector,out var collectionBody)) {
            return null;
        }

        var innerPredicate = RecreateLambdaWithParameter(resultSelectorParameters[1],liftedBody);
        var rewrittenCollectionBody = CreateSequenceCall(collectionBody,"Where",innerPredicate);
        var rewrittenCollectionSelector = RecreateLambda(collectionSelector,rewrittenCollectionBody);
        var rewrittenArguments = selectManyCall.Arguments.Select(static argument => argument.Expression).ToArray();
        rewrittenArguments[0]=rewrittenCollectionSelector;
        return RebuildCall(selectManyCall,selectManyCall.Source,rewrittenArguments);
    }

    // Join / LeftJoin の result selector で作った carrier を逆解析して、片側だけに依存する predicate を source 側へ戻す。
    private static InvocationExpressionSyntax? TryPushWhereThroughBinaryCarrier(SequenceCallInfo call,LambdaExpressionSyntax whereLambda,int resultSelectorArgumentIndex) {
        if(call.Arguments.Count<=resultSelectorArgumentIndex
            ||call.Arguments[resultSelectorArgumentIndex].Expression is not LambdaExpressionSyntax resultSelector) {
            return null;
        }
        if(!TryLiftPredicate(whereLambda,resultSelector,out var liftedBody,out var referencedParameterNames)) {
            return null;
        }

        var resultSelectorParameters = GetLambdaParameterNames(resultSelector);
        if(resultSelectorParameters.Count!=2) {
            return null;
        }

        if(referencedParameterNames.Count==0||referencedParameterNames.SetEquals(new[] { resultSelectorParameters[0] })) {
            var outerPredicate = RecreateLambdaWithParameter(resultSelectorParameters[0],liftedBody);
            var newOuter = CreateSequenceCall(call.Source,"Where",outerPredicate);
            return RebuildCall(call,newOuter,call.Arguments.Select(static argument => argument.Expression));
        }

        if(!referencedParameterNames.SetEquals(new[] { resultSelectorParameters[1] })) {
            return null;
        }

        var rewrittenArguments = call.Arguments.Select(static argument => argument.Expression).ToArray();
        var innerPredicate = RecreateLambdaWithParameter(resultSelectorParameters[1],liftedBody);
        rewrittenArguments[0]=CreateSequenceCall(rewrittenArguments[0],"Where",innerPredicate);
        return RebuildCall(call,call.Source,rewrittenArguments);
    }

    // Distinct / Order / Reverse 系は source を 1 本だけ包むので、そのまま前へ押し戻せる。
    private static InvocationExpressionSyntax TryPushWhereThroughUnary(SequenceCallInfo call,LambdaExpressionSyntax whereLambda) {
        var newSource = CreateSequenceCall(call.Source,"Where",whereLambda);
        return RebuildCall(call,newSource,call.Arguments.Select(static argument => argument.Expression));
    }

    // Except / Intersect 系は第 1 source に対する絞り込みとして扱う。
    private static InvocationExpressionSyntax TryPushWhereThroughFirstSource(SequenceCallInfo call,LambdaExpressionSyntax whereLambda) {
        var newSource = CreateSequenceCall(call.Source,"Where",whereLambda);
        return RebuildCall(call,newSource,call.Arguments.Select(static argument => argument.Expression));
    }

    // Concat / Union は左右両 source に同じ predicate を掛けても意味を保てる。
    private static InvocationExpressionSyntax TryPushWhereThroughBothSources(SequenceCallInfo call,LambdaExpressionSyntax whereLambda) {
        if(call.Arguments.Count==0) {
            return call.Invocation;
        }
        var rewrittenArguments = call.Arguments.Select(static argument => argument.Expression).ToArray();
        rewrittenArguments[0]=CreateSequenceCall(rewrittenArguments[0],"Where",whereLambda);
        var newSource = CreateSequenceCall(call.Source,"Where",whereLambda);
        return RebuildCall(call,newSource,rewrittenArguments);
    }

    // carrier から predicate を持ち上げるコア。
    // root parameter 名の衝突を避けるため、いったん sentinel 名へ rename してから member 展開する。
    private static bool TryLiftPredicate(LambdaExpressionSyntax predicateLambda,LambdaExpressionSyntax selectorLambda,out ExpressionSyntax liftedBody,out HashSet<string> referencedParameterNames) {
        liftedBody=null!;
        referencedParameterNames=new HashSet<string>(StringComparer.Ordinal);

        var predicateParameters = GetLambdaParameterNames(predicateLambda);
        if(predicateParameters.Count!=1
            ||!TryGetLambdaBody(predicateLambda,out var predicateBody)
            ||!TryGetLambdaBody(selectorLambda,out var selectorBody)
            ||!CarrierShape.TryCreate(selectorBody,out var carrierShape)) {
            return false;
        }

        const string predicateRootName = "__seamless_predicate_root";
        var rewrittenPredicateBody = (ExpressionSyntax)new ParameterRenameRewriter(predicateParameters[0],predicateRootName).Visit(predicateBody)!;
        var rewrittenBody = (ExpressionSyntax)new CarrierAccessRewriter(predicateRootName,carrierShape).Visit(rewrittenPredicateBody)!;
        if(ContainsIdentifier(rewrittenBody,predicateRootName)) {
            return false;
        }

        referencedParameterNames=CollectParameterReferences(rewrittenBody,GetLambdaParameterNames(selectorLambda));
        liftedBody=rewrittenBody;
        return true;
    }

    private static HashSet<string> CollectParameterReferences(ExpressionSyntax expression,IReadOnlyList<string> parameterNames) {
        var candidateNames = new HashSet<string>(parameterNames,StringComparer.Ordinal);
        var matches = new HashSet<string>(StringComparer.Ordinal);
        foreach(var identifier in expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()) {
            if(candidateNames.Contains(identifier.Identifier.ValueText)) {
                matches.Add(identifier.Identifier.ValueText);
            }
        }
        return matches;
    }

    private static bool ContainsIdentifier(ExpressionSyntax expression,string identifierName) {
        return expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
            .Any(identifier => string.Equals(identifier.Identifier.ValueText,identifierName,StringComparison.Ordinal));
    }

    private static LambdaExpressionSyntax RecreateLambda(LambdaExpressionSyntax template,ExpressionSyntax body) {
        var parameters = GetLambdaParameterNames(template);
        if(parameters.Count==1&&template is SimpleLambdaExpressionSyntax) {
            return SyntaxFactory.SimpleLambdaExpression(SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameters[0])),body);
        }

        var parameterList = SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters.Select(static parameterName => SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameterName)))));
        return SyntaxFactory.ParenthesizedLambdaExpression(parameterList,body);
    }

    private static LambdaExpressionSyntax RecreateLambdaWithParameter(string parameterName,ExpressionSyntax body) {
        return SyntaxFactory.SimpleLambdaExpression(SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameterName)),body);
    }

    private static bool TryGetLambdaBody(LambdaExpressionSyntax lambda,out ExpressionSyntax body) {
        if(lambda.Body is ExpressionSyntax expressionBody) {
            body=expressionBody;
            return true;
        }
        body=null!;
        return false;
    }

    private static IReadOnlyList<string> GetLambdaParameterNames(LambdaExpressionSyntax lambda) {
        if(lambda is SimpleLambdaExpressionSyntax simpleLambda) {
            return new[] { simpleLambda.Parameter.Identifier.ValueText };
        }
        if(lambda is ParenthesizedLambdaExpressionSyntax parenthesizedLambda) {
            return parenthesizedLambda.ParameterList.Parameters.Select(static parameter => parameter.Identifier.ValueText).ToArray();
        }
        return Array.Empty<string>();
    }

    private static InvocationExpressionSyntax RebuildCall(SequenceCallInfo call,ExpressionSyntax source,IEnumerable<ExpressionSyntax> argumentExpressions) {
        return CreateSequenceCall(source,call.MethodName,argumentExpressions);
    }

    private static InvocationExpressionSyntax CreateSequenceCall(ExpressionSyntax source,string methodName,IEnumerable<ExpressionSyntax> arguments) {
        var memberAccess = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            ParenthesizeIfNeeded(source),
            SyntaxFactory.IdentifierName(methodName));
        var argumentList = SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments.Select(static argument => SyntaxFactory.Argument(argument))));
        return SyntaxFactory.InvocationExpression(memberAccess,argumentList);
    }

    private static InvocationExpressionSyntax CreateSequenceCall(ExpressionSyntax source,string methodName,params ExpressionSyntax[] arguments) {
        return CreateSequenceCall(source,methodName,(IEnumerable<ExpressionSyntax>)arguments);
    }

    private static ExpressionSyntax ParenthesizeIfNeeded(ExpressionSyntax expression) {
        expression=RemoveParentheses(expression);
        return expression is IdentifierNameSyntax
            or GenericNameSyntax
            or QualifiedNameSyntax
            or AliasQualifiedNameSyntax
            or LiteralExpressionSyntax
            or ThisExpressionSyntax
            or BaseExpressionSyntax
            or MemberAccessExpressionSyntax
            or InvocationExpressionSyntax
            or ElementAccessExpressionSyntax
            ? expression
            : SyntaxFactory.ParenthesizedExpression(expression);
    }

    private static ExpressionSyntax RemoveParentheses(ExpressionSyntax expression) {
        while(expression is ParenthesizedExpressionSyntax parenthesized) {
            expression=parenthesized.Expression;
        }
        return expression;
    }

    private static bool TryGetSequenceCallInfo(InvocationExpressionSyntax invocation,out SequenceCallInfo call) {
        if(invocation.Expression is not MemberAccessExpressionSyntax memberAccess) {
            call=default;
            return false;
        }

        var methodName = memberAccess.Name.Identifier.ValueText;
        var isStaticEnumerableCall = IsStaticEnumerableReceiver(memberAccess.Expression);
        if(isStaticEnumerableCall) {
            if(invocation.ArgumentList.Arguments.Count==0) {
                call=default;
                return false;
            }
            call=new SequenceCallInfo(invocation,methodName,invocation.ArgumentList.Arguments[0].Expression,invocation.ArgumentList.Arguments.Skip(1).ToArray());
            return true;
        }

        call=new SequenceCallInfo(invocation,methodName,memberAccess.Expression,invocation.ArgumentList.Arguments.ToArray());
        return true;
    }

    private static bool IsStaticEnumerableReceiver(ExpressionSyntax expression) {
        var receiverText = RemoveParentheses(expression).ToString();
        return string.Equals(receiverText,"Enumerable",StringComparison.Ordinal)
            ||string.Equals(receiverText,"SystemEnumerable",StringComparison.Ordinal)
            ||string.Equals(receiverText,"SeamlessEnumerable",StringComparison.Ordinal)
            ||receiverText.EndsWith(".Enumerable",StringComparison.Ordinal);
    }

    // invocation を「method 名」「source」「残り引数」に正規化して扱うための軽量 DTO。
    private readonly struct SequenceCallInfo {
        public SequenceCallInfo(InvocationExpressionSyntax invocation,string methodName,ExpressionSyntax source,ArgumentSyntax[] arguments) {
            Invocation=invocation;
            MethodName=methodName;
            Source=source;
            Arguments=arguments;
        }

        public InvocationExpressionSyntax Invocation { get; }
        public string MethodName { get; }
        public ExpressionSyntax Source { get; }
        public IReadOnlyList<ArgumentSyntax> Arguments { get; }
    }

    // selector / result selector が作る carrier の形を表す抽象モデル。
    // anonymous type や tuple のような「データを保持するだけの形」だけを対象にする。
    private abstract class CarrierShape {
        public abstract bool TryResolve(IReadOnlyList<string> path,out ExpressionSyntax expression);

        public static bool TryCreate(ExpressionSyntax expression,out CarrierShape shape) {
            expression=RemoveParentheses(expression);

            if(expression is AnonymousObjectCreationExpressionSyntax anonymousObjectCreation) {
                var members = new Dictionary<string,CarrierShape>(StringComparer.Ordinal);
                foreach(var initializer in anonymousObjectCreation.Initializers) {
                    var memberName = GetAnonymousMemberName(initializer);
                    if(string.IsNullOrWhiteSpace(memberName)||!TryCreate(initializer.Expression,out var memberShape)) {
                        shape=null!;
                        return false;
                    }
                    members[memberName]=memberShape;
                }
                shape=new ObjectCarrierShape(members);
                return true;
            }

            if(expression is TupleExpressionSyntax tupleExpression) {
                return TryCreateTupleShape(tupleExpression.Arguments.Select(static argument => argument.Expression).ToArray(),tupleExpression.Arguments.Select(GetTupleElementAliases).ToArray(),out shape);
            }

            if(expression is ObjectCreationExpressionSyntax objectCreation
                &&objectCreation.ArgumentList is not null
                &&TryGetTupleTypeAliases(objectCreation.Type,objectCreation.ArgumentList.Arguments.Count,out var tupleAliases)) {
                return TryCreateTupleShape(objectCreation.ArgumentList.Arguments.Select(static argument => argument.Expression).ToArray(),tupleAliases,out shape);
            }

            if(expression is ImplicitObjectCreationExpressionSyntax implicitObjectCreation
                &&implicitObjectCreation.ArgumentList is not null) {
                var aliases = Enumerable.Range(0,implicitObjectCreation.ArgumentList.Arguments.Count).Select(static _ => Array.Empty<string>()).ToArray();
                return TryCreateTupleShape(implicitObjectCreation.ArgumentList.Arguments.Select(static argument => argument.Expression).ToArray(),aliases,out shape);
            }

            if(IsLiftableLeaf(expression)) {
                shape=new LeafCarrierShape(expression);
                return true;
            }

            shape=null!;
            return false;
        }

        private static bool TryCreateTupleShape(ExpressionSyntax[] elements,string[][] aliasesPerElement,out CarrierShape shape) {
            var members = new Dictionary<string,CarrierShape>(StringComparer.Ordinal);
            for(var index = 0; index<elements.Length; index++) {
                if(!TryCreate(elements[index],out var elementShape)) {
                    shape=null!;
                    return false;
                }
                members[$"Item{index+1}"]=elementShape;
                foreach(var alias in aliasesPerElement[index]) {
                    if(!string.IsNullOrWhiteSpace(alias)) {
                        members[alias]=elementShape;
                    }
                }
            }
            shape=new ObjectCarrierShape(members);
            return true;
        }

        private static string GetAnonymousMemberName(AnonymousObjectMemberDeclaratorSyntax initializer) {
            if(initializer.NameEquals is not null) {
                return initializer.NameEquals.Name.Identifier.ValueText;
            }
            return TryInferSimpleName(initializer.Expression,out var inferredName) ? inferredName : string.Empty;
        }

        private static string[] GetTupleElementAliases(ArgumentSyntax argument) {
            var aliases = new List<string>();
            if(argument.NameColon is not null) {
                aliases.Add(argument.NameColon.Name.Identifier.ValueText);
            }
            else if(TryInferSimpleName(argument.Expression,out var inferredName)) {
                aliases.Add(inferredName);
            }
            return aliases.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static bool TryGetTupleTypeAliases(TypeSyntax type,int elementCount,out string[][] aliases) {
            aliases=Enumerable.Range(0,elementCount).Select(static _ => Array.Empty<string>()).ToArray();
            type=type switch {
                QualifiedNameSyntax qualifiedName => qualifiedName.Right,
                AliasQualifiedNameSyntax aliasQualifiedName => aliasQualifiedName.Name,
                _ => type,
            };

            if(type is TupleTypeSyntax tupleType) {
                aliases=tupleType.Elements.Select(static element => string.IsNullOrWhiteSpace(element.Identifier.ValueText) ? Array.Empty<string>() : new[] { element.Identifier.ValueText }).ToArray();
                return tupleType.Elements.Count==elementCount;
            }

            if(type is GenericNameSyntax genericName
                &&(genericName.Identifier.ValueText=="ValueTuple"||genericName.Identifier.ValueText=="Tuple")) {
                return genericName.TypeArgumentList.Arguments.Count==elementCount;
            }

            if(type is IdentifierNameSyntax identifierName
                &&(identifierName.Identifier.ValueText=="ValueTuple"||identifierName.Identifier.ValueText=="Tuple")) {
                return true;
            }

            return false;
        }

        private static bool TryInferSimpleName(ExpressionSyntax expression,out string name) {
            expression=RemoveParentheses(expression);
            if(expression is IdentifierNameSyntax identifierName) {
                name=identifierName.Identifier.ValueText;
                return true;
            }
            if(expression is MemberAccessExpressionSyntax memberAccess) {
                name=memberAccess.Name.Identifier.ValueText;
                return true;
            }
            name=string.Empty;
            return false;
        }

        private static bool IsLiftableLeaf(ExpressionSyntax expression) {
            expression=RemoveParentheses(expression);
            switch(expression) {
                case IdentifierNameSyntax:
                case LiteralExpressionSyntax:
                case ThisExpressionSyntax:
                case BaseExpressionSyntax:
                case DefaultExpressionSyntax:
                case TypeOfExpressionSyntax:
                    return true;
                case MemberAccessExpressionSyntax memberAccess:
                    return IsLiftableLeaf(memberAccess.Expression);
                case ElementAccessExpressionSyntax elementAccess:
                    return IsLiftableLeaf(elementAccess.Expression)
                        &&elementAccess.ArgumentList.Arguments.All(static argument => IsLiftableLeaf(argument.Expression));
                case CastExpressionSyntax castExpression:
                    return IsLiftableLeaf(castExpression.Expression);
                case ConditionalExpressionSyntax:
                case BinaryExpressionSyntax:
                case AssignmentExpressionSyntax:
                case AnonymousObjectCreationExpressionSyntax:
                case InvocationExpressionSyntax:
                case ObjectCreationExpressionSyntax:
                case ImplicitObjectCreationExpressionSyntax:
                case TupleExpressionSyntax:
                    return false;
                default:
                    return false;
            }
        }
    }

    // member 展開の終点。元 expression をそのまま返す。
    private sealed class LeafCarrierShape:CarrierShape {
        public LeafCarrierShape(ExpressionSyntax expression) {
            this.expression=expression;
        }

        private readonly ExpressionSyntax expression;

        public override bool TryResolve(IReadOnlyList<string> path,out ExpressionSyntax expression) {
            expression=this.expression;
            return path.Count==0;
        }
    }

    // member 名から次の carrier 形状へ辿るノード。
    private sealed class ObjectCarrierShape:CarrierShape {
        public ObjectCarrierShape(Dictionary<string,CarrierShape> members) {
            this.members=members;
        }

        private readonly Dictionary<string,CarrierShape> members;

        public override bool TryResolve(IReadOnlyList<string> path,out ExpressionSyntax expression) {
            if(path.Count==0) {
                expression=null!;
                return false;
            }
            if(!members.TryGetValue(path[0],out var next)) {
                expression=null!;
                return false;
            }
            return next.TryResolve(path.Skip(1).ToArray(),out expression);
        }
    }

    // predicate 中の carrier member access を元 expression に戻す rewriter。
    private sealed class CarrierAccessRewriter:CSharpSyntaxRewriter {
        public CarrierAccessRewriter(string rootParameterName,CarrierShape carrierShape) {
            this.rootParameterName=rootParameterName;
            this.carrierShape=carrierShape;
        }

        private readonly string rootParameterName;
        private readonly CarrierShape carrierShape;

        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node) {
            if(TryGetCarrierPath(node,out var path)
                &&carrierShape.TryResolve(path,out var replacement)) {
                return ParenthesizeIfNeeded(replacement);
            }
            return base.VisitMemberAccessExpression(node);
        }

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node) {
            if(string.Equals(node.Identifier.ValueText,rootParameterName,StringComparison.Ordinal)
                &&!(node.Parent is MemberAccessExpressionSyntax memberAccess&&memberAccess.Expression==node)
                &&carrierShape.TryResolve(Array.Empty<string>(),out var replacement)) {
                return ParenthesizeIfNeeded(replacement);
            }
            return base.VisitIdentifierName(node);
        }

        private bool TryGetCarrierPath(ExpressionSyntax expression,out IReadOnlyList<string> path) {
            expression=RemoveParentheses(expression);
            if(expression is IdentifierNameSyntax identifierName
                &&string.Equals(identifierName.Identifier.ValueText,rootParameterName,StringComparison.Ordinal)) {
                path=Array.Empty<string>();
                return true;
            }

            if(expression is MemberAccessExpressionSyntax memberAccess
                &&TryGetCarrierPath(memberAccess.Expression,out var parentPath)) {
                var parts = parentPath.ToList();
                parts.Add(memberAccess.Name.Identifier.ValueText);
                path=parts;
                return true;
            }

            path=Array.Empty<string>();
            return false;
        }
    }

    // predicate 側と selector 側で同じ parameter 名を使っていても壊れないようにする rename rewriter。
    private sealed class ParameterRenameRewriter:CSharpSyntaxRewriter {
        public ParameterRenameRewriter(string from,string to) {
            this.from=from;
            this.to=to;
        }

        private readonly string from;
        private readonly string to;

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node) {
            if(string.Equals(node.Identifier.ValueText,from,StringComparison.Ordinal)) {
                return SyntaxFactory.IdentifierName(to);
            }
            return base.VisitIdentifierName(node);
        }
    }
}
