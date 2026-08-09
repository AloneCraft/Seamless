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

        internal bool TryBind(IReadOnlyList<FragmentParameter> parameters,out FragmentArguments arguments) {
            arguments=new FragmentArguments(this.UniqueNumber++);
            var argumentIndex=this.argumentStartIndex;
            foreach(var parameter in parameters) {
                switch(parameter.Kind) {
                    case FragmentArgumentKind.Source:
                        if(!this.pipeline.列挙式フラグメント生成(this.semanticModel,this.sourceExpression,this.containingType,ref this.UniqueNumber,out var source,out var sourceMethods,this.unsafeAccessorCollector)) {
                            return false;
                        }
                        arguments.Add(source);
                        this.AppendStaticMethods(sourceMethods);
                        break;
                    case FragmentArgumentKind.Enumerable:
                        if(!this.TryGetArgument(argumentIndex,out var enumerableExpression)
                            ||!this.pipeline.列挙式フラグメント生成(this.semanticModel,enumerableExpression,this.containingType,ref this.UniqueNumber,out var enumerable,out var enumerableMethods,this.unsafeAccessorCollector)) {
                            return false;
                        }
                        arguments.Add(enumerable);
                        this.AppendStaticMethods(enumerableMethods);
                        argumentIndex++;
                        break;
                    case FragmentArgumentKind.Function:
                        if(!this.TryGetArgument(argumentIndex,out var functionExpression)) {
                            return false;
                        }
                        var methodParameterIndex=argumentIndex-this.argumentStartIndex;
                        if(methodParameterIndex<0
                            ||methodParameterIndex>=this.method.Parameters.Length
                            ||!this.pipeline.関数式フラグメント生成(functionExpression,this.method.Parameters[methodParameterIndex].Type,ref this.UniqueNumber,out var function,this.semanticModel,this.containingType)
                            ||!this.pipeline.関数フラグメントが利用可能か(function)) {
                            return false;
                        }
                        arguments.Add(function);
                        argumentIndex++;
                        break;
                    case FragmentArgumentKind.Expression:
                        if(!this.TryGetArgument(argumentIndex,out var expression)) {
                            if(parameter.Name=="comparer") {
                                arguments.Add("null");
                                break;
                            }
                            return false;
                        }
                        arguments.Add(RenderExpressionForGeneratedMethod(expression,this.semanticModel,this.containingType,this.unsafeAccessorCollector,this.replacementMap));
                        argumentIndex++;
                        break;
                    case FragmentArgumentKind.ResultType:
                        arguments.Add(this.method.ReturnType.ToDisplayString(完全修飾形式));
                        break;
                    case FragmentArgumentKind.ResultElementType:
                        if(!this.pipeline.列挙要素型取得(this.method.ReturnType,out var elementType,out _)) {
                            return false;
                        }
                        arguments.Add(elementType.ToDisplayString(完全修飾形式));
                        break;
                    default:
                        return false;
                }
            }
            return argumentIndex==this.invocation.ArgumentList.Arguments.Count;
        }

        internal bool TryCreateProviderNative(OperatorKey key,FragmentArguments arguments,out Fragment fragment) =>
            TryCreateBackendNativeIterateFragment(key,arguments,out fragment);

        internal void PropagateOuterTypeParameters(Fragment fragment) {
            fragment.PropagateOuterTypeParameters(this.OuterTypeParameters);
        }

        private bool TryGetArgument(int index,out ExpressionSyntax expression) {
            if(index>=this.invocation.ArgumentList.Arguments.Count) {
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

        internal bool TryBind(IReadOnlyList<FragmentParameter> parameters,out FragmentArguments arguments) =>
            this.bindingContext.TryBind(parameters,out arguments);

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