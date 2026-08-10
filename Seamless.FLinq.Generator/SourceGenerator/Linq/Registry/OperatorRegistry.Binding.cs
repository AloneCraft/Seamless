namespace Seamless.FLinq;

public sealed partial class FLinqIncrementalGenerator {
    internal sealed class FragmentFactoryContext {
        private readonly SemanticModel semanticModel;
        private readonly InvocationExpressionSyntax invocation;
        private readonly ExpressionSyntax sourceExpression;
        private readonly int argumentStartIndex;
        private readonly IMethodSymbol method;
        private readonly INamedTypeSymbol containingType;
        private readonly List<string>? unsafeAccessorCollector;
        private readonly IReadOnlyDictionary<string,string>? replacementMap;
        private readonly GenerationPipeline pipeline;
        private string staticMethods=string.Empty;

        internal FragmentFactoryContext(
            SemanticModel semanticModel,
            InvocationExpressionSyntax invocation,
            ExpressionSyntax sourceExpression,
            int argumentStartIndex,
            IMethodSymbol method,
            INamedTypeSymbol containingType,
            int uniqueNumber,
            List<string>? unsafeAccessorCollector,
            IReadOnlyDictionary<string,string>? replacementMap,
            GenerationPipeline pipeline) {
            this.semanticModel=semanticModel;
            this.invocation=invocation;
            this.sourceExpression=sourceExpression;
            this.argumentStartIndex=argumentStartIndex;
            this.method=method;
            this.containingType=containingType;
            this.UniqueNumber=uniqueNumber;
            this.unsafeAccessorCollector=unsafeAccessorCollector;
            this.replacementMap=replacementMap;
            this.pipeline=pipeline;
        }

        internal int UniqueNumber;
        internal string StaticMethods => this.staticMethods;
        internal string[] OuterTypeParameters => this.pipeline.OuterTypeParameters;
        internal string ResultType => this.method.ReturnType.ToDisplayString(完全修飾形式);

        internal int TakeUniqueNumber() => this.UniqueNumber++;

        internal bool TryBuildSource(out Enumerable source) {
            source=null!;
            if(!this.pipeline.列挙式フラグメント生成(this.semanticModel,this.sourceExpression,this.containingType,ref this.UniqueNumber,out source,out var sourceMethods,this.unsafeAccessorCollector)) {
                return false;
            }
            this.AppendStaticMethods(sourceMethods);
            return true;
        }

        internal bool TryBuildEnumerableArgument(int relativeIndex,out Enumerable enumerable) {
            enumerable=null!;
            if(!this.TryGetArgument(this.argumentStartIndex+relativeIndex,out var expression)
                ||!this.pipeline.列挙式フラグメント生成(this.semanticModel,expression,this.containingType,ref this.UniqueNumber,out enumerable,out var methods,this.unsafeAccessorCollector)) {
                return false;
            }
            this.AppendStaticMethods(methods);
            return true;
        }

        internal bool TryBuildFunctionArgument(int relativeIndex,out FuncFragment function) {
            function=null!;
            if(!this.TryGetArgument(this.argumentStartIndex+relativeIndex,out var expression)) {
                return false;
            }
            var methodParameterIndex=relativeIndex;
            return methodParameterIndex>=0
                &&methodParameterIndex<this.method.Parameters.Length
                &&this.pipeline.関数式フラグメント生成(expression,this.method.Parameters[methodParameterIndex].Type,ref this.UniqueNumber,out function,this.semanticModel,this.containingType)
                &&this.pipeline.関数フラグメントが利用可能か(function);
        }

        internal bool TryBuildExpressionArgument(int relativeIndex,string? parameterName,out string expression,out bool consumed) {
            consumed=false;
            if(!this.TryGetArgument(this.argumentStartIndex+relativeIndex,out var syntax)) {
                if(string.Equals(parameterName,"comparer",StringComparison.Ordinal)) {
                    expression="null";
                    return true;
                }
                expression=string.Empty;
                return false;
            }
            expression=RenderExpressionForGeneratedMethod(syntax,this.semanticModel,this.containingType,this.unsafeAccessorCollector,this.replacementMap);
            consumed=true;
            return true;
        }

        internal bool TryGetResultElementType(out string elementType) {
            elementType=string.Empty;
            if(!this.pipeline.列挙要素型取得(this.method.ReturnType,out var symbol,out _)) {
                return false;
            }
            elementType=symbol.ToDisplayString(完全修飾形式);
            return true;
        }

        internal bool HasExactlyArguments(int relativeArgumentCount) =>
            this.argumentStartIndex+relativeArgumentCount==this.invocation.ArgumentList.Arguments.Count;

        internal bool TryCreateProviderNative(OperatorKey key,IProviderNativeBoundArguments arguments,out Fragment fragment) =>
            TryCreateBackendNativeIterateFragment(key,arguments,out fragment);

        internal FragmentRenderContext RenderContext =>
            FragmentRenderContext.ForOuterTypeParameters(this.OuterTypeParameters);

        internal void ApplyRenderContext(Fragment fragment) {
            fragment.ApplyRenderContext(this.RenderContext);
        }

        private bool TryGetArgument(int index,out ExpressionSyntax expression) {
            if(index<0||index>=this.invocation.ArgumentList.Arguments.Count) {
                expression=null!;
                return false;
            }
            expression=this.invocation.ArgumentList.Arguments[index].Expression;
            return true;
        }

        private void AppendStaticMethods(string methods) {
            this.staticMethods=静的メソッドコード連結(this.staticMethods,methods);
        }
    }

    internal sealed class RootFactoryContext {
        private readonly SemanticModel semanticModel;
        private readonly InvocationExpressionSyntax invocation;
        private readonly IMethodSymbol method;
        private readonly INamedTypeSymbol containingType;
        private readonly List<string>? unsafeAccessorCollector;
        private readonly IReadOnlyDictionary<string,string>? replacementMap;
        private readonly GenerationPipeline pipeline;
        private readonly FragmentFactoryContext bindingContext;

        internal RootFactoryContext(
            SemanticModel semanticModel,
            InvocationExpressionSyntax invocation,
            IMethodSymbol method,
            INamedTypeSymbol containingType,
            int uniqueNumber,
            List<string>? unsafeAccessorCollector,
            IReadOnlyDictionary<string,string>? replacementMap,
            GenerationPipeline pipeline) {
            this.semanticModel=semanticModel;
            this.invocation=invocation;
            this.method=method;
            this.containingType=containingType;
            this.unsafeAccessorCollector=unsafeAccessorCollector;
            this.replacementMap=replacementMap;
            this.pipeline=pipeline;
            this.bindingContext=new FragmentFactoryContext(
                semanticModel,
                invocation,
                invocation,
                0,
                method,
                containingType,
                uniqueNumber,
                unsafeAccessorCollector,
                replacementMap,
                pipeline);
        }

        internal IMethodSymbol Method => this.method;
        internal int UniqueNumber => this.bindingContext.UniqueNumber;
        internal int TakeUniqueNumber() => this.bindingContext.TakeUniqueNumber();
        internal string ResultType => this.bindingContext.ResultType;
        internal bool TryBuildSource(out Enumerable source) => this.bindingContext.TryBuildSource(out source);
        internal bool TryBuildEnumerableArgument(int relativeIndex,out Enumerable enumerable) => this.bindingContext.TryBuildEnumerableArgument(relativeIndex,out enumerable);
        internal bool TryBuildFunctionArgument(int relativeIndex,out FuncFragment function) => this.bindingContext.TryBuildFunctionArgument(relativeIndex,out function);
        internal bool TryBuildExpressionArgument(int relativeIndex,string? parameterName,out string expression,out bool consumed) => this.bindingContext.TryBuildExpressionArgument(relativeIndex,parameterName,out expression,out consumed);
        internal bool TryGetResultElementType(out string elementType) => this.bindingContext.TryGetResultElementType(out elementType);
        internal bool HasExactlyArguments(int relativeArgumentCount) => this.bindingContext.HasExactlyArguments(relativeArgumentCount);

        internal bool TryBuildAsEnumerableSource(out Enumerable fragment) {
            fragment=null!;
            if(!GenerationPipeline.TryGetInvocationArgumentExpression(this.method,this.invocation,0,out var sourceExpression)) {
                return false;
            }
            return this.pipeline.TryBuildEnumerableRootFromExpressionWithReplacement(
                this.semanticModel,
                sourceExpression,
                this.containingType,
                ref this.bindingContext.UniqueNumber,
                out fragment,
                this.unsafeAccessorCollector,
                this.replacementMap);
        }
    }
}