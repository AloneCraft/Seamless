using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Seamless.SourceGenerator;

// LINQ translation template の試作置き場。
// L は Expression の MethodInfo を Roslyn symbol に橋渡しし、
// OriginalDefinition ベースの汎用キー文字列を返す。
public class Template {
    private static readonly SymbolDisplayFormat TemplateMethodSignatureFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeContainingType|SymbolDisplayMemberOptions.IncludeParameters,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.ExpandNullable);

    public class Fragment {
        public readonly string staticメソッド;
        public readonly string BeginGlobal;
        public readonly string BeginLoopOutside;
        public readonly string BeginLoop;
        public readonly string BeginLoopInside;
        public readonly string EndLoopInside;
        public readonly string EndLoop;
        public readonly string EndLoopOutside;
        public readonly string EndGlobal;
        public readonly string result;

        public Fragment(
            string staticメソッド,
            string BeginGlobal,
            string BeginLoopOutside,
            string BeginLoop,
            string BeginLoopInside,
            string EndLoopInside,
            string EndLoop,
            string EndLoopOutside,
            string EndGlobal,
            string result)
        {
            this.staticメソッド   = staticメソッド;
            this.BeginGlobal      = BeginGlobal;
            this.BeginLoopOutside = BeginLoopOutside;
            this.BeginLoop        = BeginLoop;
            this.BeginLoopInside  = BeginLoopInside;
            this.EndLoopInside    = EndLoopInside;
            this.EndLoop          = EndLoop;
            this.EndLoopOutside   = EndLoopOutside;
            this.EndGlobal        = EndGlobal;
            this.result           = result;
        }
    }

    public class EnumerableFragment : Fragment {
        public readonly string Add;
        public readonly string ElementTypeName;
        public string MoveNext;
        public string Element;

        public string If_MoveNext_goto(string label)     => $"if({this.MoveNext})goto {label}";
        public string If_Not_MoveNext_goto(string label) => $"if(!{this.MoveNext})goto {label}";

        public EnumerableFragment(
            string staticメソッド,
            string BeginGlobal,
            string BeginLoopOutside,
            string BeginLoop,
            string BeginLoopInside,
            string EndLoopInside,
            string EndLoop,
            string EndLoopOutside,
            string EndGlobal,
            string result,
            string Element,
            string MoveNext,
            string Add,
            string? elementTypeName=null)
            : base(
                staticメソッド,
                BeginGlobal,
                BeginLoopOutside,
                BeginLoop,
                BeginLoopInside,
                EndLoopInside,
                EndLoop,
                EndLoopOutside,
                EndGlobal,
                result)
        {
            this.Add      = Add;
            this.MoveNext = MoveNext;
            this.Element  = Element;
            this.ElementTypeName = string.IsNullOrEmpty(elementTypeName) ? "object" : elementTypeName!;
        }
    }
    public abstract class FuncFragment {
        public string DefineLambda=string.Empty;
        public string ReturnTypeName="void";
        public string[] ParameterTypeNames=Array.Empty<string>();
        public string[] ParameterNames=Array.Empty<string>();

        public string DefineMethod(string methodName) {
            var invoke = this.GetType().GetMethod("Invoke");
            var parameters = invoke?.GetParameters() ?? Array.Empty<ParameterInfo>();
            var returnType = !string.IsNullOrEmpty(this.ReturnTypeName)
                ? this.ReturnTypeName
                : invoke?.ReturnType.FullName ?? "void";
            var parametersList = new System.Text.StringBuilder();
            var count = Math.Max(this.ParameterTypeNames.Length,parameters.Length);
            for(var index = 0; index<count; index++) {
                if(index>0) {
                    parametersList.Append(", ");
                }
                var paramType = index<this.ParameterTypeNames.Length
                    ? this.ParameterTypeNames[index]
                    : MetadataTypeName(parameters[index].ParameterType);
                var paramName = index<this.ParameterNames.Length&&!string.IsNullOrEmpty(this.ParameterNames[index])
                    ? this.ParameterNames[index]
                    : parameters.Length>index&&parameters[index].Name is not null ? parameters[index].Name! : $"arg{index}";
                parametersList.Append($"{paramType} {paramName}");
            }

            return $"public static {returnType} {methodName}({parametersList}){this.DefineLambda}";
        }
    }
    public class SelectMany_selector:FuncFragment{
        public object? Source;
    }
    public class LambdaFuncFragment:FuncFragment{
    }

    public delegate Fragment FragmentTemplate(int 番号,object[] arguments);

    private readonly Compilation compilation;

    public Template()
        : this(CreateTemplateCompilation()) {
    }

    public Template(Compilation compilation) {
        this.compilation=compilation ?? throw new ArgumentNullException(nameof(compilation));
        this.InitializeFragments();
    }

    private string L<T>(Expression<Func<T>> expression) {
        if(expression.Body is not MethodCallExpression methodCall) {
            throw new ArgumentException("L requires a method-call expression.",nameof(expression));
        }

        return RoslynMethodSignature(this.compilation,methodCall.Method);
    }

    private static string RoslynMethodSignature(Compilation compilation,MethodInfo reflectionMethod) {
        var definition = reflectionMethod.IsGenericMethod
            ? reflectionMethod.GetGenericMethodDefinition()
            : reflectionMethod;
        var declaringType = definition.DeclaringType
            ?? throw new InvalidOperationException($"Method '{definition}' does not have a declaring type.");
        var containingType = compilation.GetTypeByMetadataName(declaringType.FullName!)
            ?? throw new InvalidOperationException($"Could not resolve '{declaringType.FullName}' in the current compilation.");

        foreach(var candidate in containingType.GetMembers(definition.Name)) {
            if(candidate is not IMethodSymbol methodSymbol) {
                continue;
            }
            if(MethodMatches(compilation,definition,methodSymbol)) {
                return methodSymbol.OriginalDefinition.ToDisplayString(TemplateMethodSignatureFormat);
            }
        }

        throw new InvalidOperationException($"Could not map method '{definition}' to a Roslyn symbol.");
    }

    private static bool MethodMatches(Compilation compilation,MethodInfo reflectionMethod,IMethodSymbol symbol) {
        if(symbol.Arity!=reflectionMethod.GetGenericArguments().Length) {
            return false;
        }

        var reflectionParameters = reflectionMethod.GetParameters();
        if(symbol.Parameters.Length!=reflectionParameters.Length) {
            return false;
        }

        for(var index = 0; index<reflectionParameters.Length; index++) {
            if(!TypeMatches(compilation,reflectionParameters[index].ParameterType,symbol.Parameters[index].Type)) {
                return false;
            }
        }

        return TypeMatches(compilation,reflectionMethod.ReturnType,symbol.ReturnType);
    }

    private static bool TypeMatches(Compilation compilation,Type reflectionType,ITypeSymbol symbolType) {
        if(reflectionType.IsByRef) {
            reflectionType=reflectionType.GetElementType()
                ?? throw new InvalidOperationException("ByRef type did not expose its element type.");
        }

        if(reflectionType.IsGenericParameter) {
            return symbolType is ITypeParameterSymbol typeParameter
                &&typeParameter.TypeParameterKind==TypeParameterKind.Method
                &&typeParameter.Ordinal==reflectionType.GenericParameterPosition;
        }

        if(reflectionType.IsArray) {
            return symbolType is IArrayTypeSymbol arrayType
                &&arrayType.Rank==reflectionType.GetArrayRank()
                &&TypeMatches(compilation,reflectionType.GetElementType()!,arrayType.ElementType);
        }

        if(symbolType is ITypeParameterSymbol) {
            return false;
        }

        if(reflectionType.IsGenericType) {
            if(symbolType is not INamedTypeSymbol namedType) {
                return false;
            }

            if(!TypeDefinitionMatches(reflectionType.GetGenericTypeDefinition(),namedType)) {
                return false;
            }

            var reflectionArguments = reflectionType.GetGenericArguments();
            if(namedType.TypeArguments.Length!=reflectionArguments.Length) {
                return false;
            }

            for(var index = 0; index<reflectionArguments.Length; index++) {
                if(!TypeMatches(compilation,reflectionArguments[index],namedType.TypeArguments[index])) {
                    return false;
                }
            }

            return true;
        }

        if(symbolType is not INamedTypeSymbol nonGenericNamedType||reflectionType.FullName is null) {
            return false;
        }

        return TypeDefinitionMatches(reflectionType,nonGenericNamedType);
    }

    private static bool TypeDefinitionMatches(Type reflectionType,INamedTypeSymbol symbolType) {
        if(!string.Equals(symbolType.MetadataName,reflectionType.Name,StringComparison.Ordinal)) {
            return false;
        }

        var reflectionDeclaringType = reflectionType.DeclaringType;
        if(reflectionDeclaringType is null) {
            if(symbolType.ContainingType is not null) {
                return false;
            }

            return string.Equals(
                symbolType.ContainingNamespace.ToDisplayString(),
                reflectionType.Namespace ?? string.Empty,
                StringComparison.Ordinal);
        }

        return symbolType.ContainingType is not null
            &&TypeDefinitionMatches(reflectionDeclaringType,symbolType.ContainingType);
    }

    private static string MetadataTypeName(Type type) {
        var fullName = type.FullName ?? type.Name;
        return "global::"+fullName.Replace("+",".");
    }

    private static string TypeName<T>() => MetadataTypeName(typeof(T));

    private FragmentTemplate M(global::System.Func<int,object[],Fragment> factory) => new(factory);

    private void Add<T>(Expression<Func<T>> expression,global::System.Func<int,object[],Fragment> factory) {
        this.dictionaryFragment.Add(this.L(expression),this.M(factory));
    }

    private static string StructDefinition(string structName,FuncFragment func,string methodName) {
        return $$"""
            private struct {{structName}}{
                {{func.DefineMethod(methodName)}}
            }
            """;
    }

    private static string FuncParameterTypeName(FuncFragment func,int index) {
        if(func.ParameterTypeNames.Length>index&&!string.IsNullOrEmpty(func.ParameterTypeNames[index])) {
            return func.ParameterTypeNames[index];
        }
        var invoke = func.GetType().GetMethod("Invoke")
            ?? throw new InvalidOperationException("FuncFragment does not expose Invoke.");
        return MetadataTypeName(invoke.GetParameters()[index].ParameterType);
    }

    private static string FuncReturnTypeName(FuncFragment func) {
        if(!string.IsNullOrEmpty(func.ReturnTypeName)&&func.ReturnTypeName!="void") {
            return func.ReturnTypeName;
        }
        var invoke = func.GetType().GetMethod("Invoke")
            ?? throw new InvalidOperationException("FuncFragment does not expose Invoke.");
        return MetadataTypeName(invoke.ReturnType);
    }

    private static string NullableTypeName(string typeName) {
        return typeName.EndsWith("?",StringComparison.Ordinal) ? typeName : $"{typeName}?";
    }

    private static string SequenceElementTypeName(string typeName) {
        const string enumerablePrefix = "global::System.Collections.Generic.IEnumerable<";
        if(typeName.StartsWith(enumerablePrefix,StringComparison.Ordinal)&&typeName.EndsWith(">",StringComparison.Ordinal)) {
            return typeName.Substring(enumerablePrefix.Length,typeName.Length-enumerablePrefix.Length-1);
        }
        if(typeName.EndsWith("[]",StringComparison.Ordinal)) {
            return typeName.Substring(0,typeName.Length-2);
        }
        return "object";
    }

    private static string MaterializeToList(EnumerableFragment source,string temp) {
        return $$"""
            {{temp}}.Clear();
            {{source.BeginLoopOutside}}
            {{source.BeginLoop}}
                {{source.BeginLoopInside}}
                {{temp}}.Add({{source.Element}});
                {{source.EndLoopInside}}
            {{source.EndLoop}}
            {{source.EndLoopOutside}}
            """;
    }

    private static EnumerableFragment Carrierize(
        int 番号,
        string staticメソッド,
        string beginGlobal,
        string beginLoopOutside,
        string sourceResult,
        string elementTypeName,
        string endGlobal="") {
        var enumerator = $"e{番号}";
        var start = $"Start{番号}";
        var end = $"End{番号}";
        var element = $"item{番号}";
        var moveNext = $"{enumerator}.MoveNext()";
        return new EnumerableFragment(
            staticメソッド,
            beginGlobal,
            beginLoopOutside,
            $$"""
                var {{enumerator}}={{sourceResult}}.GetEnumerator();
                goto {{start}};
                """,
            string.Empty,
            $$"""
                {{start}}:;
                if(!{{moveNext}})goto {{end}};
                var {{element}}={{enumerator}}.Current;
                """,
            $$"""
                goto {{start}};
                """,
            $$"""
                {{end}}:;
                """,
            endGlobal,
            sourceResult,
            element,
            moveNext,
            $"result.Add({element});",
            elementTypeName);
    }

    private static Fragment AverageFragment(
        int 番号,
        EnumerableFragment source,
        string staticメソッド,
        string sumType,
        string beginLoopInside,
        string endLoopInside,
        string resultExpression,
        string emptyExpression="throw NoElements()") {
        var sum = $"sum{番号}";
        var count = $"count{番号}";
        var success = $"Success{番号}";
        var result = $"result{番号}";
        return new Fragment(
            staticメソッド,
            source.BeginGlobal,
            $$"""
                {{source.BeginLoopOutside}}
                var {{sum}}=default({{sumType}});
                var {{count}}=0L;
                """,
            source.BeginLoop,
            $$"""
                {{source.BeginLoopInside}}
                {{beginLoopInside}}
                """,
            endLoopInside,
            source.EndLoop,
            $$"""
                if({{count}}>0)goto {{success}};
                {{emptyExpression}};
                {{success}}:
                var {{result}}={{resultExpression}};
                {{source.EndLoopOutside}}
                """,
            source.EndGlobal,
            result);
    }

    // 登録テーブルは未実装。L() でキー文字列を安定化したうえで今後ここへ追加する。
    private readonly Dictionary<string,FragmentTemplate> dictionaryFragment=new();

    private void InitializeFragments() {
        this.Add(
            () => Array.Empty<int>().Aggregate((left,right) => left+right),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var func = (FuncFragment)arguments[1];
                var Struct=$"Struct{番号}";
                var Head = $"Head{番号}";
                var Continue = $"Continue{番号}";
                var Success = $"Success{番号}";
                var hasValue = $"hasValue{番号}";
                var result = $"result{番号}";
                return new Fragment(
                    $$"""
                        private struct {{Struct}}{
                            {{func.DefineMethod(nameof(func))}}
                        }
                        """,
                    source.BeginGlobal,
                    $$"""
                        {{source.BeginLoopOutside}}
                        var {{result}}=default!;
                        var {{hasValue}}=false;
                        """,
                    source.BeginLoop,
                    $$"""
                        {{source.BeginLoopInside}}
                        if({{hasValue}})goto {{Head}};
                        {{result}}={{source.Element}};
                        {{hasValue}}=true;
                        goto {{Continue}};
                        {{Head}}:
                        {{result}}={{Struct}}.func({{result}},{{source.Element}});
                        """,
                    $$"""
                        {{Continue}}:;
                        {{source.EndLoopInside}}
                        """,
                    source.EndLoop,
                    $$"""
                        if({{hasValue}})goto {{Success}};
                        throw NoElements();
                        {{Success}}:;
                        {{source.EndLoopOutside}}
                        """,
                    source.EndGlobal,
                    result
                );
            });

        this.Add(
            () => Array.Empty<int>().Aggregate<int,long>(0L,(accumulator,value) => accumulator+value),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var seed = (string)arguments[1];
                var func = (FuncFragment)arguments[2];
                var Struct = $"Struct{番号}";
                var result = $"result{番号}";
                return new Fragment(
                    StructDefinition(Struct,func,nameof(func)),
                    source.BeginGlobal,
                    $$"""
                        {{source.BeginLoopOutside}}
                        var {{result}}={{seed}};
                        """,
                    source.BeginLoop,
                    $$"""
                        {{source.BeginLoopInside}}
                        {{result}}={{Struct}}.func({{result}},{{source.Element}});
                        """,
                    source.EndLoopInside,
                    source.EndLoop,
                    source.EndLoopOutside,
                    source.EndGlobal,
                    result);
            });

        this.Add(
            () => Array.Empty<int>().Aggregate<int,long,string>(0L,(accumulator,value) => accumulator+value,accumulator => accumulator.ToString()),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var seed = (string)arguments[1];
                var func = (FuncFragment)arguments[2];
                var resultSelector = (FuncFragment)arguments[3];
                var Struct = $"Struct{番号}";
                var result = $"result{番号}";
                return new Fragment(
                    $$"""
                        private struct {{Struct}}{
                            {{func.DefineMethod(nameof(func))}}
                            {{resultSelector.DefineMethod(nameof(resultSelector))}}
                        }
                        """,
                    source.BeginGlobal,
                    $$"""
                        {{source.BeginLoopOutside}}
                        var {{result}}={{seed}};
                        """,
                    source.BeginLoop,
                    $$"""
                        {{source.BeginLoopInside}}
                        {{result}}={{Struct}}.func({{result}},{{source.Element}});
                        """,
                    source.EndLoopInside,
                    source.EndLoop,
                    source.EndLoopOutside,
                    source.EndGlobal,
                    $"{Struct}.resultSelector({result})");
            });

        this.Add(
            () => Array.Empty<int>().AggregateBy(value => value,0L,(accumulator,value) => accumulator+value,(IEqualityComparer<int>?)null),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var keySelector = (FuncFragment)arguments[1];
                var seed = (string)arguments[2];
                var func = (FuncFragment)arguments[3];
                var keyComparer = arguments.Length>4 ? arguments[4] as string : null;
                var result = $"result{番号}";
                var Struct = $"Struct{番号}";
                var seed0 = $"seed{番号}";
                var dictionary = $"dictionary{番号}";
                var Element = $"output{番号}";
                var accumulator = $"accumulator{番号}";
                var Key = $"key{番号}";
                var keyTypeName = FuncReturnTypeName(keySelector);
                var accumulateTypeName = FuncParameterTypeName(func,0);
                return new EnumerableFragment(
                    $$"""
                        private struct {{Struct}}{
                            {{keySelector.DefineMethod(nameof(keySelector))}}
                            {{func.DefineMethod(nameof(func))}}
                        }
                        """,
                    $$"""
                        {{source.BeginGlobal}}
                        var {{dictionary}} = new global::System.Collections.Generic.Dictionary<{{keyTypeName}},{{accumulateTypeName}}>({{keyComparer}});
                        var {{result}} = new global::System.Collections.Generic.List<global::System.Collections.Generic.KeyValuePair<{{keyTypeName}}, {{accumulateTypeName}}>>();
                        """,
                    $$"""
                        var {{seed0}}={{seed}};
                        {{dictionary}}.Clear();
                        {{result}}.Clear();
                        {{source.BeginLoopOutside}}
                        {{source.BeginLoop}}
                            {{source.BeginLoopInside}}
                            var {{Key}}={{Struct}}.keySelector({{source.Element}});
                            var {{accumulator}} = {{dictionary}}.GetValueOrDefault({{Key}}, {{seed0}});
                            {{dictionary}}[{{Key}}] = {{Struct}}.func({{accumulator}}, {{source.Element}});
                            {{source.EndLoopInside}}
                        {{source.EndLoop}}
                        {{source.EndLoopOutside}}
                        """,
                    $$"""
                        foreach(var {{Element}} in {{dictionary}}){
                        """,
                    string.Empty,
                    $$"""
                        {{result}}.Add({{Element}});
                        """,
                    string.Empty,
                    $$"""
                        }
                        """,
                    source.EndGlobal,
                    result,
                    Element,
                    string.Empty,
                    string.Empty,
                    $"global::System.Collections.Generic.KeyValuePair<{keyTypeName}, {accumulateTypeName}>");
            });

        this.Add(
            () => Array.Empty<int>().AggregateBy(value => value,key => (long)key,(accumulator,value) => accumulator+value,(IEqualityComparer<int>?)null),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var keySelector = (FuncFragment)arguments[1];
                var seedSelector = (FuncFragment)arguments[2];
                var func = (FuncFragment)arguments[3];
                var keyComparer = arguments.Length>4 ? arguments[4] as string : null;
                var result = $"result{番号}";
                var Struct = $"Struct{番号}";
                var dictionary = $"dictionary{番号}";
                var Element = $"item{番号}";
                var accumulator = $"accumulator{番号}";
                var Key = $"key{番号}";
                var keyTypeName = FuncReturnTypeName(keySelector);
                var accumulateTypeName = FuncReturnTypeName(seedSelector);
                return new EnumerableFragment(
                    $$"""
                        private struct {{Struct}}{
                            {{keySelector.DefineMethod(nameof(keySelector))}}
                            {{seedSelector.DefineMethod(nameof(seedSelector))}}
                            {{func.DefineMethod(nameof(func))}}
                        }
                        """,
                    $$"""
                        {{source.BeginGlobal}}
                        var {{dictionary}} = new global::System.Collections.Generic.Dictionary<{{keyTypeName}},{{accumulateTypeName}}>({{keyComparer}});
                        var {{result}} = new global::System.Collections.Generic.List<global::System.Collections.Generic.KeyValuePair<{{keyTypeName}}, {{accumulateTypeName}}>>();
                        """,
                    $$"""
                        {{dictionary}}.Clear();
                        {{result}}.Clear();
                        {{source.BeginLoopOutside}}
                        {{source.BeginLoop}}
                            {{source.BeginLoopInside}}
                            var {{Key}}={{Struct}}.keySelector({{source.Element}});
                            if(!{{dictionary}}.TryGetValue({{Key}},out var {{accumulator}}))
                                {{accumulator}}={{Struct}}.seedSelector({{Key}});
                            {{dictionary}}[{{Key}}]={{Struct}}.func({{accumulator}},{{source.Element}});
                            {{source.EndLoopInside}}
                        {{source.EndLoop}}
                        {{source.EndLoopOutside}}
                        """,
                    $$"""
                        foreach(var {{Element}} in {{dictionary}}){
                        """,
                    $$"""
                        {{result}}.Add({{Element}});
                        """,
                    string.Empty,
                    $$"""
                        }
                        """,
                    string.Empty,
                    source.EndGlobal,
                    result,
                    Element,
                    string.Empty,
                    string.Empty,
                    $"global::System.Collections.Generic.KeyValuePair<{keyTypeName}, {accumulateTypeName}>");
            });

        this.Add(
            () => Array.Empty<int>().All(value => value>0),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var predicate = (FuncFragment)arguments[1];
                var Struct = $"Struct{番号}";
                var Continue = $"Continue{番号}";
                var End = $"End{番号}";
                var result = $"result{番号}";
                return new Fragment(
                    StructDefinition(Struct,predicate,nameof(predicate)),
                    source.BeginGlobal,
                    $$"""
                        {{source.BeginLoopOutside}}
                        var {{result}}=true;
                        """,
                    source.BeginLoop,
                    $$"""
                        {{source.BeginLoopInside}}
                        if({{Struct}}.predicate({{source.Element}}))goto {{Continue}};
                        {{result}}=false;
                        goto {{End}};
                        """,
                    $$"""
                        {{Continue}}:;
                        {{source.EndLoopInside}}
                        """,
                    source.EndLoop,
                    $$"""
                        {{End}}:;
                        {{source.EndLoopOutside}}
                        """,
                    source.EndGlobal,
                    result);
            });

        this.Add(
            () => Array.Empty<int>().Any(),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var End = $"End{番号}";
                var result = $"result{番号}";
                return new Fragment(
                    string.Empty,
                    source.BeginGlobal,
                    $$"""
                        {{source.BeginLoopOutside}}
                        var {{result}}=false;
                        """,
                    source.BeginLoop,
                    $$"""
                        {{source.BeginLoopInside}}
                        {{result}}=true;
                        goto {{End}};
                        """,
                    source.EndLoopInside,
                    source.EndLoop,
                    $$"""
                        {{End}}:;
                        {{source.EndLoopOutside}}
                        """,
                    source.EndGlobal,
                    result);
            });

        this.Add(
            () => Array.Empty<int>().Any(value => value>0),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var predicate = (FuncFragment)arguments[1];
                var Struct = $"Struct{番号}";
                var Continue = $"Continue{番号}";
                var End = $"End{番号}";
                var result = $"result{番号}";
                return new Fragment(
                    StructDefinition(Struct,predicate,nameof(predicate)),
                    source.BeginGlobal,
                    $$"""
                        {{source.BeginLoopOutside}}
                        var {{result}}=false;
                        """,
                    source.BeginLoop,
                    $$"""
                        {{source.BeginLoopInside}}
                        if(!{{Struct}}.predicate({{source.Element}}))goto {{Continue}};
                        {{result}}=true;
                        goto {{End}};
                        """,
                    $$"""
                        {{Continue}}:;
                        {{source.EndLoopInside}}
                        """,
                    source.EndLoop,
                    $$"""
                        {{End}}:;
                        {{source.EndLoopOutside}}
                        """,
                    source.EndGlobal,
                    result);
            });

        this.Add(
            () => Array.Empty<int>().Append(0),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var result = $"result{番号}";
                return new EnumerableFragment(
                    string.Empty,
                    source.BeginGlobal,
                    $$"""
                        {{source.BeginLoopOutside}}
                        var {{result}}=new global::System.Collections.Generic.List<{{source.ElementTypeName}}>();
                        """,
                    source.BeginLoop,
                    $$"""
                        {{source.BeginLoopInside}}
                        {{result}}.Add({{source.Element}});
                        """,
                    source.EndLoopInside,
                    source.EndLoop,
                    source.EndLoopOutside,
                    source.EndGlobal,
                    result,
                    source.Element,
                    source.MoveNext,
                    string.Empty,
                    source.ElementTypeName);
            });

        this.Add(
            () => Array.Empty<int>().AsEnumerable(),
            (_,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                return new EnumerableFragment(
                    source.staticメソッド,
                    source.BeginGlobal,
                    source.BeginLoopOutside,
                    source.BeginLoop,
                    source.BeginLoopInside,
                    source.EndLoopInside,
                    source.EndLoop,
                    source.EndLoopOutside,
                    source.EndGlobal,
                    source.result,
                    source.Element,
                    source.MoveNext,
                    source.Add,
                    source.ElementTypeName);
            });

        this.Add(
            () => Array.Empty<double>().Average(),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                return AverageFragment(
                    番号,
                    source,
                    string.Empty,
                    "double",
                    $$"""
                    sum{{番号}}+={{source.Element}};
                    checked{
                        count{{番号}}++;
                    }
                    """,
                    source.EndLoopInside,
                    $"sum{番号}/count{番号}");
            });

        this.Add(
            () => Array.Empty<int>().Average(),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                return AverageFragment(
                    番号,
                    source,
                    string.Empty,
                    "long",
                    $$"""
                    sum{{番号}}+={{source.Element}};
                    checked{
                        count{{番号}}++;
                    }
                    """,
                    source.EndLoopInside,
                    $"(double)sum{番号}/count{番号}");
            });

        this.Add(
            () => Array.Empty<long>().Average(),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                return AverageFragment(
                    番号,
                    source,
                    string.Empty,
                    "decimal",
                    $$"""
                    sum{{番号}}+={{source.Element}};
                    checked{
                        count{{番号}}++;
                    }
                    """,
                    source.EndLoopInside,
                    $"(double)(sum{番号}/count{番号})");
            });

        this.Add(
            () => Array.Empty<decimal>().Average(),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                return AverageFragment(
                    番号,
                    source,
                    string.Empty,
                    "decimal",
                    $$"""
                    sum{{番号}}+={{source.Element}};
                    checked{
                        count{{番号}}++;
                    }
                    """,
                    source.EndLoopInside,
                    $"sum{番号}/count{番号}");
            });

        this.Add(
            () => Array.Empty<decimal?>().Average(),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var Continue = $"Continue{番号}";
                return AverageFragment(
                    番号,
                    source,
                    string.Empty,
                    "decimal",
                    $$"""
                    if(!{{source.Element}}.HasValue)goto {{Continue}};
                    checked{
                        sum{{番号}}+={{source.Element}}.GetValueOrDefault();
                        count{{番号}}++;
                    }
                    """,
                    $"{Continue}:;",
                    $"sum{番号}/count{番号}",
                    "return null");
            });

        this.Add(
            () => Array.Empty<double?>().Average(),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var Continue = $"Continue{番号}";
                return AverageFragment(
                    番号,
                    source,
                    string.Empty,
                    "double",
                    $$"""
                    if(!{{source.Element}}.HasValue)goto {{Continue}};
                    sum{{番号}}+={{source.Element}}.GetValueOrDefault();
                    checked{
                        count{{番号}}++;
                    }
                    """,
                    $"{Continue}:;",
                    $"sum{番号}/count{番号}",
                    "return null");
            });

        this.Add(
            () => Array.Empty<int?>().Average(),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var Continue = $"Continue{番号}";
                return AverageFragment(
                    番号,
                    source,
                    string.Empty,
                    "long",
                    $$"""
                    if(!{{source.Element}}.HasValue)goto {{Continue}};
                    sum{{番号}}+={{source.Element}}.GetValueOrDefault();
                    checked{
                        count{{番号}}++;
                    }
                    """,
                    $"{Continue}:;",
                    $"(double)sum{番号}/count{番号}",
                    "return null");
            });

        this.Add(
            () => Array.Empty<long?>().Average(),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var Continue = $"Continue{番号}";
                return AverageFragment(
                    番号,
                    source,
                    string.Empty,
                    "decimal",
                    $$"""
                    if(!{{source.Element}}.HasValue)goto {{Continue}};
                    checked{
                        sum{{番号}}+={{source.Element}}.GetValueOrDefault();
                        count{{番号}}++;
                    }
                    """,
                    $"{Continue}:;",
                    $"(double)(sum{番号}/count{番号})",
                    "return null");
            });

        this.Add(
            () => Array.Empty<float?>().Average(),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var Continue = $"Continue{番号}";
                return AverageFragment(
                    番号,
                    source,
                    string.Empty,
                    "double",
                    $$"""
                    if(!{{source.Element}}.HasValue)goto {{Continue}};
                    sum{{番号}}+={{source.Element}}.GetValueOrDefault();
                    checked{
                        count{{番号}}++;
                    }
                    """,
                    $"{Continue}:;",
                    $"(float)(sum{番号}/count{番号})",
                    "return null");
            });

        this.Add(
            () => Array.Empty<float>().Average(),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                return AverageFragment(
                    番号,
                    source,
                    string.Empty,
                    "double",
                    $$"""
                    sum{{番号}}+={{source.Element}};
                    checked{
                        count{{番号}}++;
                    }
                    """,
                    source.EndLoopInside,
                    $"(float)(sum{番号}/count{番号})");
            });

        this.Add(
            () => Array.Empty<int>().Average(value => (decimal)value),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var selector = (FuncFragment)arguments[1];
                var Struct = $"Struct{番号}";
                return AverageFragment(
                    番号,
                    source,
                    StructDefinition(Struct,selector,nameof(selector)),
                    "decimal",
                    $$"""
                    checked{
                        sum{{番号}}+={{Struct}}.selector({{source.Element}});
                        count{{番号}}++;
                    }
                    """,
                    source.EndLoopInside,
                    $"sum{番号}/count{番号}");
            });

        this.Add(
            () => Array.Empty<int>().Average(value => (double)value),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var selector = (FuncFragment)arguments[1];
                var Struct = $"Struct{番号}";
                return AverageFragment(
                    番号,
                    source,
                    StructDefinition(Struct,selector,nameof(selector)),
                    "double",
                    $$"""
                    sum{{番号}}+={{Struct}}.selector({{source.Element}});
                    checked{
                        count{{番号}}++;
                    }
                    """,
                    source.EndLoopInside,
                    $"sum{番号}/count{番号}");
            });

        this.Add(
            () => Array.Empty<int>().Average(value => value),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var selector = (FuncFragment)arguments[1];
                var Struct = $"Struct{番号}";
                return AverageFragment(
                    番号,
                    source,
                    StructDefinition(Struct,selector,nameof(selector)),
                    "long",
                    $$"""
                    checked{
                        sum{{番号}}+={{Struct}}.selector({{source.Element}});
                        count{{番号}}++;
                    }
                    """,
                    source.EndLoopInside,
                    $"(double)sum{番号}/count{番号}");
            });

        this.Add(
            () => Array.Empty<int>().Average(value => (long)value),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var selector = (FuncFragment)arguments[1];
                var Struct = $"Struct{番号}";
                return AverageFragment(
                    番号,
                    source,
                    StructDefinition(Struct,selector,nameof(selector)),
                    "decimal",
                    $$"""
                    checked{
                        sum{{番号}}+={{Struct}}.selector({{source.Element}});
                        count{{番号}}++;
                    }
                    """,
                    source.EndLoopInside,
                    $"(double)(sum{番号}/count{番号})");
            });

        this.Add(
            () => Array.Empty<int>().Average(value => (decimal?)value),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var selector = (FuncFragment)arguments[1];
                var Struct = $"Struct{番号}";
                var value = $"value{番号}";
                var Continue = $"Continue{番号}";
                return AverageFragment(
                    番号,
                    source,
                    StructDefinition(Struct,selector,nameof(selector)),
                    "decimal",
                    $$"""
                    var {{value}} = {{Struct}}.selector({{source.Element}});
                    if(!{{value}}.HasValue) goto {{Continue}};
                    checked{
                        sum{{番号}}+={{value}}.GetValueOrDefault();
                        count{{番号}}++;
                    }
                    """,
                    $"{Continue}:;",
                    $"sum{番号}/count{番号}",
                    "return null");
            });

        this.Add(
            () => Array.Empty<int>().Average(value => (double?)value),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var selector = (FuncFragment)arguments[1];
                var Struct = $"Struct{番号}";
                var value = $"value{番号}";
                var Continue = $"Continue{番号}";
                return AverageFragment(
                    番号,
                    source,
                    StructDefinition(Struct,selector,nameof(selector)),
                    "double",
                    $$"""
                    var {{value}} = {{Struct}}.selector({{source.Element}});
                    if(!{{value}}.HasValue) goto {{Continue}};
                    sum{{番号}}+={{value}}.GetValueOrDefault();
                    checked{
                        count{{番号}}++;
                    }
                    """,
                    $"{Continue}:;",
                    $"sum{番号}/count{番号}",
                    "return null");
            });

        this.Add(
            () => Array.Empty<int>().Average(value => (int?)value),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var selector = (FuncFragment)arguments[1];
                var Struct = $"Struct{番号}";
                var value = $"value{番号}";
                var Continue = $"Continue{番号}";
                return AverageFragment(
                    番号,
                    source,
                    StructDefinition(Struct,selector,nameof(selector)),
                    "long",
                    $$"""
                    var {{value}} = {{Struct}}.selector({{source.Element}});
                    if(!{{value}}.HasValue) goto {{Continue}};
                    checked{
                        sum{{番号}}+={{value}}.GetValueOrDefault();
                        count{{番号}}++;
                    }
                    """,
                    $"{Continue}:;",
                    $"(double)sum{番号}/count{番号}",
                    "return null");
            });

        this.Add(
            () => Array.Empty<int>().Average(value => (long?)value),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var selector = (FuncFragment)arguments[1];
                var Struct = $"Struct{番号}";
                var value = $"value{番号}";
                var Continue = $"Continue{番号}";
                return AverageFragment(
                    番号,
                    source,
                    StructDefinition(Struct,selector,nameof(selector)),
                    "decimal",
                    $$"""
                    var {{value}} = {{Struct}}.selector({{source.Element}});
                    if(!{{value}}.HasValue) goto {{Continue}};
                    checked{
                        sum{{番号}}+={{value}}.GetValueOrDefault();
                        count{{番号}}++;
                    }
                    """,
                    $"{Continue}:;",
                    $"(double)(sum{番号}/count{番号})",
                    "return null");
            });

        this.Add(
            () => Array.Empty<int>().Average(value => (float?)value),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var selector = (FuncFragment)arguments[1];
                var Struct = $"Struct{番号}";
                var value = $"value{番号}";
                var Continue = $"Continue{番号}";
                return AverageFragment(
                    番号,
                    source,
                    StructDefinition(Struct,selector,nameof(selector)),
                    "double",
                    $$"""
                    var {{value}} = {{Struct}}.selector({{source.Element}});
                    if(!{{value}}.HasValue) goto {{Continue}};
                    sum{{番号}}+={{value}}.GetValueOrDefault();
                    checked{
                        count{{番号}}++;
                    }
                    """,
                    $"{Continue}:;",
                    $"(float)(sum{番号}/count{番号})",
                    "return null");
            });

        this.Add(
            () => Array.Empty<int>().Average(value => (float)value),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var selector = (FuncFragment)arguments[1];
                var Struct = $"Struct{番号}";
                return AverageFragment(
                    番号,
                    source,
                    StructDefinition(Struct,selector,nameof(selector)),
                    "double",
                    $$"""
                    sum{{番号}}+={{Struct}}.selector({{source.Element}});
                    checked{
                        count{{番号}}++;
                    }
                    """,
                    source.EndLoopInside,
                    $"(float)(sum{番号}/count{番号})");
            });

        this.Add(
            () => Array.Empty<int>().Chunk(1),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var size = (string)arguments[1];
                var result = $"result{番号}";
                var chunk = $"chunk{番号}";
                var index = $"index{番号}";
                var Continue = $"Continue{番号}";
                var End = $"End{番号}";
                return new EnumerableFragment(
                    string.Empty,
                    $$"""
                        {{source.BeginGlobal}}
                        var {{result}} = new global::System.Collections.Generic.List<{{source.ElementTypeName}}[]>();
                        """,
                    $$"""
                        {{source.BeginLoopOutside}}
                        if({{size}}<1)throw new ArgumentOutOfRangeException(nameof(size));
                        {{result}}.Clear();
                        var {{chunk}} = new {{source.ElementTypeName}}[{{size}}];
                        var {{index}} = 0;
                        """,
                    source.BeginLoop,
                    $$"""
                        {{source.BeginLoopInside}}
                        {{chunk}}[{{index}}++] = {{source.Element}};
                        if({{index}}<{{size}})goto {{Continue}};
                        {{result}}.Add({{chunk}});
                        """,
                    $$"""
                        {{chunk}} = new {{source.ElementTypeName}}[{{size}}];
                        {{index}} = 0;
                        {{Continue}}:;
                        {{source.EndLoopInside}}
                        """,
                    $$"""
                        if({{index}}==0) goto {{End}};
                        Array.Resize(ref {{chunk}},{{index}});
                        {{result}}.Add({{chunk}});
                        {{End}}:
                        {{source.EndLoop}}
                        """,
                    source.EndLoopOutside,
                    source.EndGlobal,
                    result,
                    chunk,
                    string.Empty,
                    string.Empty,
                    $"{source.ElementTypeName}[]");
            });

        this.Add(
            () => Array.Empty<int>().Concat(Array.Empty<int>()),
            (番号,arguments) => {
                var first = (EnumerableFragment)arguments[0];
                var second = (EnumerableFragment)arguments[1];
                var result = $"result{番号}";
                var Element = $"item{番号}";
                return new EnumerableFragment(
                    string.Empty,
                    $$"""
                        var {{result}} = new global::System.Collections.Generic.List<{{first.ElementTypeName}}>();
                        {{first.BeginGlobal}}
                        {{second.BeginGlobal}}
                        """,
                    $$"""
                        {{result}}.Clear();
                        {{first.BeginLoopOutside}}
                        {{first.BeginLoop}}
                            {{first.BeginLoopInside}}
                            {{result}}.Add({{first.Element}});
                            {{first.EndLoopInside}}
                        {{first.EndLoop}}
                        {{first.EndLoopOutside}}
                        {{second.BeginLoopOutside}}
                        {{second.BeginLoop}}
                            {{second.BeginLoopInside}}
                            {{result}}.Add({{second.Element}});
                            {{second.EndLoopInside}}
                        {{second.EndLoop}}
                        {{second.EndLoopOutside}}
                        """,
                    $$"""
                        foreach(var {{Element}} in {{result}}){
                        """,
                    string.Empty,
                    string.Empty,
                    $$"""
                        }
                        """,
                    string.Empty,
                    $$"""
                        {{first.EndGlobal}}
                        {{second.EndGlobal}}
                        """,
                    result,
                    Element,
                    string.Empty,
                    string.Empty,
                    first.ElementTypeName);
            });

        this.Add(
            () => Enumerable.Contains(Array.Empty<int>(),0),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var value = (string)arguments[1];
                var result = $"result{番号}";
                var End = $"End{番号}";
                var comparer = $"comparer{番号}";
                return new Fragment(
                    string.Empty,
                    $$"""
                        {{source.BeginGlobal}}
                        var {{comparer}}=EqualityComparer<{{source.ElementTypeName}}>.Default;
                        """,
                    $$"""
                        {{source.BeginLoopOutside}}
                        var {{result}}=false;
                        """,
                    source.BeginLoop,
                    $$"""
                        {{source.BeginLoopInside}}
                        if({{comparer}}.Equals({{source.Element}},{{value}})){
                            {{result}}=true;
                            goto {{End}};
                        }
                        """,
                    source.EndLoopInside,
                    source.EndLoop,
                    $$"""
                        {{End}}:;
                        {{source.EndLoopOutside}}
                        """,
                    source.EndGlobal,
                    result);
            });

        this.Add(
            () => Enumerable.Contains(Array.Empty<int>(),0,(IEqualityComparer<int>?)null),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var value = (string)arguments[1];
                var comparerArg = arguments.Length>2 ? arguments[2] as string ?? "null" : "null";
                var result = $"result{番号}";
                var End = $"End{番号}";
                var comparer = $"comparer{番号}";
                return new Fragment(
                    string.Empty,
                    $$"""
                        {{source.BeginGlobal}}
                        var {{comparer}}={{comparerArg}}??EqualityComparer<{{source.ElementTypeName}}>.Default;
                        """,
                    $$"""
                        {{source.BeginLoopOutside}}
                        var {{result}}=false;
                        """,
                    source.BeginLoop,
                    $$"""
                        {{source.BeginLoopInside}}
                        if({{comparer}}.Equals({{source.Element}},{{value}})){
                            {{result}}=true;
                            goto {{End}};
                        }
                        """,
                    source.EndLoopInside,
                    source.EndLoop,
                    $$"""
                        {{End}}:;
                        {{source.EndLoopOutside}}
                        """,
                    source.EndGlobal,
                    result);
            });

        this.Add(
            () => Array.Empty<int>().Count(),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var count = $"count{番号}";
                return new Fragment(
                    string.Empty,
                    source.BeginGlobal,
                    $$"""
                        {{source.BeginLoopOutside}}
                        var {{count}}=0;
                        """,
                    source.BeginLoop,
                    $$"""
                        {{source.BeginLoopInside}}
                        checked {
                            {{count}}++;
                        }
                        """,
                    source.EndLoopInside,
                    source.EndLoop,
                    source.EndLoopOutside,
                    source.EndGlobal,
                    count);
            });

        this.Add(
            () => Array.Empty<int>().Count(value => value>0),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var predicate = (FuncFragment)arguments[1];
                var Struct = $"Struct{番号}";
                var count = $"count{番号}";
                return new Fragment(
                    StructDefinition(Struct,predicate,nameof(predicate)),
                    source.BeginGlobal,
                    $$"""
                        {{source.BeginLoopOutside}}
                        var {{count}}=0;
                        """,
                    source.BeginLoop,
                    $$"""
                        {{source.BeginLoopInside}}
                        if({{Struct}}.predicate({{source.Element}})){
                            checked {
                                {{count}}++;
                            }
                        }
                        """,
                    source.EndLoopInside,
                    source.EndLoop,
                    source.EndLoopOutside,
                    source.EndGlobal,
                    count);
            });

        this.Add(
            () => Array.Empty<int>().CountBy(value => value,(IEqualityComparer<int>?)null),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var keySelector = (FuncFragment)arguments[1];
                var keyComparer = arguments.Length>2 ? arguments[2] as string ?? "null" : "null";
                var Struct = $"Struct{番号}";
                var counts = $"counts{番号}";
                var count = $"count{番号}";
                var key = $"key{番号}";
                var result = $"result{番号}";
                var Element = $"pair{番号}";
                var keyTypeName = FuncReturnTypeName(keySelector);
                var pairTypeName = $"global::System.Collections.Generic.KeyValuePair<{keyTypeName}, int>";
                return new EnumerableFragment(
                    $$"""
                        private struct {{Struct}}{
                            {{keySelector.DefineMethod(nameof(keySelector))}}
                        }
                        """,
                    $$"""
                        {{source.BeginGlobal}}
                        var {{counts}}=new global::System.Collections.Generic.Dictionary<{{keyTypeName}},int>({{keyComparer}});
                        var {{result}}=new global::System.Collections.Generic.List<{{pairTypeName}}>();
                        """,
                    $$"""
                        {{counts}}.Clear();
                        {{result}}.Clear();
                        {{source.BeginLoopOutside}}
                        {{source.BeginLoop}}
                            {{source.BeginLoopInside}}
                            var {{key}}={{Struct}}.keySelector({{source.Element}});
                            {{counts}}.TryGetValue({{key}},out var {{count}});
                            checked {
                                {{counts}}[{{key}}]={{count}}+1;
                            }
                            {{source.EndLoopInside}}
                        {{source.EndLoop}}
                        {{source.EndLoopOutside}}
                        """,
                    $$"""
                        foreach(var {{Element}} in {{counts}}){
                        """,
                    $$"""
                        {{result}}.Add({{Element}});
                        """,
                    string.Empty,
                    $$"""
                        }
                        """,
                    string.Empty,
                    source.EndGlobal,
                    result,
                    Element,
                    string.Empty,
                    string.Empty,
                    pairTypeName);
            });

        this.Add(
            () => Array.Empty<int>().DefaultIfEmpty(),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var result = $"result{番号}";
                var Element = $"item{番号}";
                var nullableElementTypeName = NullableTypeName(source.ElementTypeName);
                return new EnumerableFragment(
                    string.Empty,
                    $$"""
                        {{source.BeginGlobal}}
                        var {{result}}=new global::System.Collections.Generic.List<{{nullableElementTypeName}}>();
                        """,
                    $$"""
                        {{result}}.Clear();
                        {{source.BeginLoopOutside}}
                        {{source.BeginLoop}}
                            {{source.BeginLoopInside}}
                            {{result}}.Add({{source.Element}});
                            {{source.EndLoopInside}}
                        {{source.EndLoop}}
                        {{source.EndLoopOutside}}
                        if({{result}}.Count==0){{result}}.Add(default);
                        """,
                    $$"""
                        foreach(var {{Element}} in {{result}}){
                        """,
                    string.Empty,
                    string.Empty,
                    $$"""
                        }
                        """,
                    string.Empty,
                    source.EndGlobal,
                    result,
                    Element,
                    string.Empty,
                    string.Empty,
                    nullableElementTypeName);
            });

        this.Add(
            () => Array.Empty<int>().DefaultIfEmpty(0),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var defaultValue = (string)arguments[1];
                var result = $"result{番号}";
                var Element = $"item{番号}";
                return new EnumerableFragment(
                    string.Empty,
                    $$"""
                        {{source.BeginGlobal}}
                        var {{result}}=new global::System.Collections.Generic.List<{{source.ElementTypeName}}>();
                        """,
                    $$"""
                        {{result}}.Clear();
                        {{source.BeginLoopOutside}}
                        {{source.BeginLoop}}
                            {{source.BeginLoopInside}}
                            {{result}}.Add({{source.Element}});
                            {{source.EndLoopInside}}
                        {{source.EndLoop}}
                        {{source.EndLoopOutside}}
                        if({{result}}.Count==0){{result}}.Add({{defaultValue}});
                        """,
                    $$"""
                        foreach(var {{Element}} in {{result}}){
                        """,
                    string.Empty,
                    string.Empty,
                    $$"""
                        }
                        """,
                    string.Empty,
                    source.EndGlobal,
                    result,
                    Element,
                    string.Empty,
                    string.Empty,
                    source.ElementTypeName);
            });

        this.Add(
            () => Array.Empty<int>().DistinctBy(value => value),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var keySelector = (FuncFragment)arguments[1];
                var comparer = arguments.Length>2 ? arguments[2] as string ?? "null" : "null";
                var Struct = $"Struct{番号}";
                var seen = $"seen{番号}";
                var result = $"result{番号}";
                var Element = $"item{番号}";
                var keyTypeName = FuncReturnTypeName(keySelector);
                return new EnumerableFragment(
                    $$"""
                        private struct {{Struct}}{
                            {{keySelector.DefineMethod(nameof(keySelector))}}
                        }
                        """,
                    $$"""
                        {{source.BeginGlobal}}
                        var {{result}}=new global::System.Collections.Generic.List<{{source.ElementTypeName}}>();
                        var {{seen}}=new global::System.Collections.Generic.HashSet<{{keyTypeName}}>({{comparer}});
                        """,
                    $$"""
                        {{result}}.Clear();
                        {{seen}}.Clear();
                        {{source.BeginLoopOutside}}
                        {{source.BeginLoop}}
                            {{source.BeginLoopInside}}
                            if({{seen}}.Add({{Struct}}.keySelector({{source.Element}}))){{result}}.Add({{source.Element}});
                            {{source.EndLoopInside}}
                        {{source.EndLoop}}
                        {{source.EndLoopOutside}}
                        """,
                    $$"""
                        foreach(var {{Element}} in {{result}}){
                        """,
                    string.Empty,
                    string.Empty,
                    $$"""
                        }
                        """,
                    string.Empty,
                    source.EndGlobal,
                    result,
                    Element,
                    string.Empty,
                    string.Empty,
                    source.ElementTypeName);
            });

        this.Add(
            () => Array.Empty<int>().DistinctBy(value => value,(IEqualityComparer<int>?)null),
            (番号,arguments) => {
                var forwardedArguments = new object[3];
                Array.Copy(arguments,forwardedArguments,Math.Min(arguments.Length,3));
                return this.dictionaryFragment[this.L(() => Array.Empty<int>().DistinctBy(value => value))](番号,forwardedArguments);
            });

        this.Add(
            () => Array.Empty<int>().Distinct(),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var comparer = arguments.Length>1 ? arguments[1] as string ?? "null" : "null";
                var seen = $"seen{番号}";
                var result = $"result{番号}";
                var Element = $"item{番号}";
                return new EnumerableFragment(
                    string.Empty,
                    $$"""
                        {{source.BeginGlobal}}
                        var {{result}}=new global::System.Collections.Generic.List<{{source.ElementTypeName}}>();
                        var {{seen}}=new global::System.Collections.Generic.HashSet<{{source.ElementTypeName}}>({{comparer}});
                        """,
                    $$"""
                        {{result}}.Clear();
                        {{seen}}.Clear();
                        {{source.BeginLoopOutside}}
                        {{source.BeginLoop}}
                            {{source.BeginLoopInside}}
                            if({{seen}}.Add({{source.Element}})){{result}}.Add({{source.Element}});
                            {{source.EndLoopInside}}
                        {{source.EndLoop}}
                        {{source.EndLoopOutside}}
                        """,
                    $$"""
                        foreach(var {{Element}} in {{result}}){
                        """,
                    string.Empty,
                    string.Empty,
                    $$"""
                        }
                        """,
                    string.Empty,
                    source.EndGlobal,
                    result,
                    Element,
                    string.Empty,
                    string.Empty,
                    source.ElementTypeName);
            });

        this.Add(
            () => Array.Empty<int>().Distinct((IEqualityComparer<int>?)null),
            (番号,arguments) => {
                var forwardedArguments = new object[2];
                Array.Copy(arguments,forwardedArguments,Math.Min(arguments.Length,2));
                return this.dictionaryFragment[this.L(() => Array.Empty<int>().Distinct())](番号,forwardedArguments);
            });

        this.Add(
            () => Array.Empty<int>().ElementAtOrDefault(0),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var index = (string)arguments[1];
                var current = $"current{番号}";
                var result = $"result{番号}";
                var Success = $"Success{番号}";
                var found = $"found{番号}";
                return new Fragment(
                    string.Empty,
                    source.BeginGlobal,
                    $$"""
                        {{source.BeginLoopOutside}}
                        var {{current}}=0;
                        var {{found}}=false;
                        {{source.ElementTypeName}} {{result}}=default!;
                        """,
                    source.BeginLoop,
                    $$"""
                        {{source.BeginLoopInside}}
                        if({{current}}=={{index}}){
                            {{result}}={{source.Element}};
                            {{found}}=true;
                            goto {{Success}};
                        }
                        {{current}}++;
                        """,
                    source.EndLoopInside,
                    source.EndLoop,
                    $$"""
                        {{Success}}:
                        {{source.EndLoopOutside}}
                        """,
                    source.EndGlobal,
                    $"{index}<0 || !{found} ? default : {result}");
            });

        this.Add(
            () => Array.Empty<int>().ElementAt(0),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var index = (string)arguments[1];
                var current = $"current{番号}";
                var result = $"result{番号}";
                var Success = $"Success{番号}";
                var found = $"found{番号}";
                return new Fragment(
                    string.Empty,
                    source.BeginGlobal,
                    $$"""
                        {{source.BeginLoopOutside}}
                        var {{current}}=0;
                        var {{found}}=false;
                        {{source.ElementTypeName}} {{result}}=default!;
                        """,
                    source.BeginLoop,
                    $$"""
                        {{source.BeginLoopInside}}
                        if({{current}}=={{index}}){
                            {{result}}={{source.Element}};
                            {{found}}=true;
                            goto {{Success}};
                        }
                        {{current}}++;
                        """,
                    source.EndLoopInside,
                    source.EndLoop,
                    $$"""
                        {{Success}}:
                        {{source.EndLoopOutside}}
                        """,
                    source.EndGlobal,
                    $"{index}<0 || !{found} ? throw new ArgumentOutOfRangeException(nameof(index)) : {result}");
            });

        this.Add(
            () => Array.Empty<int>().ExceptBy(Array.Empty<int>(),value => value,(IEqualityComparer<int>?)null),
            (番号,arguments) => {
                var first = (EnumerableFragment)arguments[0];
                var second = (string)arguments[1];
                var keySelector = (FuncFragment)arguments[2];
                var comparer = arguments.Length>3 ? arguments[3] as string ?? "null" : "null";
                var Struct = $"Struct{番号}";
                var temp = $"temp{番号}";
                var result = $"result{番号}";
                var keyTypeName = FuncReturnTypeName(keySelector);
                return Carrierize(
                    番号,
                    $$"""
                        private struct {{Struct}}{
                            {{keySelector.DefineMethod(nameof(keySelector))}}
                        }
                        """,
                    $$"""
                        {{first.BeginGlobal}}
                        var {{temp}} = new global::System.Collections.Generic.List<{{first.ElementTypeName}}>();
                        global::System.Collections.Generic.IEnumerable<{{first.ElementTypeName}}> {{result}} = default!;
                        """,
                    $$"""
                        {{MaterializeToList(first,temp)}}
                        {{result}} = global::Seamless.Linq.Enumerable.ExceptBy<{{first.ElementTypeName}},{{keyTypeName}}>({{temp}},{{second}},{{Struct}}.keySelector,{{comparer}});
                        """,
                    result,
                    first.ElementTypeName);
            });

        this.Add(
            () => Array.Empty<int>().Except(Array.Empty<int>(),(IEqualityComparer<int>?)null),
            (番号,arguments) => {
                var first = (EnumerableFragment)arguments[0];
                var second = (string)arguments[1];
                var comparer = arguments.Length>2 ? arguments[2] as string ?? "null" : "null";
                var temp = $"temp{番号}";
                var result = $"result{番号}";
                return Carrierize(
                    番号,
                    string.Empty,
                    $$"""
                        {{first.BeginGlobal}}
                        var {{temp}} = new global::System.Collections.Generic.List<{{first.ElementTypeName}}>();
                        global::System.Collections.Generic.IEnumerable<{{first.ElementTypeName}}> {{result}} = default!;
                        """,
                    $$"""
                        {{MaterializeToList(first,temp)}}
                        {{result}} = global::Seamless.Linq.Enumerable.Except<{{first.ElementTypeName}}>({{temp}},{{second}},{{comparer}});
                        """,
                    result,
                    first.ElementTypeName);
            });

        this.Add(
            () => Array.Empty<int>().FirstOrDefault(),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var result = $"result{番号}";
                var End = $"End{番号}";
                return new Fragment(
                    string.Empty,
                    source.BeginGlobal,
                    $$"""
                        {{source.BeginLoopOutside}}
                        {{source.ElementTypeName}} {{result}}=default!;
                        """,
                    source.BeginLoop,
                    $$"""
                        {{source.BeginLoopInside}}
                        {{result}}={{source.Element}};
                        goto {{End}};
                        """,
                    source.EndLoopInside,
                    source.EndLoop,
                    $$"""
                        {{End}}:
                        {{source.EndLoopOutside}}
                        """,
                    source.EndGlobal,
                    result);
            });

        this.Add(
            () => Array.Empty<int>().FirstOrDefault(0),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var defaultValue = (string)arguments[1];
                var result = $"result{番号}";
                var End = $"End{番号}";
                return new Fragment(
                    string.Empty,
                    source.BeginGlobal,
                    $$"""
                        {{source.BeginLoopOutside}}
                        var {{result}}={{defaultValue}};
                        """,
                    source.BeginLoop,
                    $$"""
                        {{source.BeginLoopInside}}
                        {{result}}={{source.Element}};
                        goto {{End}};
                        """,
                    source.EndLoopInside,
                    source.EndLoop,
                    $$"""
                        {{End}}:
                        {{source.EndLoopOutside}}
                        """,
                    source.EndGlobal,
                    result);
            });

        this.Add(
            () => Array.Empty<int>().First(),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var result = $"result{番号}";
                var found = $"found{番号}";
                var End = $"End{番号}";
                return new Fragment(
                    string.Empty,
                    source.BeginGlobal,
                    $$"""
                        {{source.BeginLoopOutside}}
                        var {{found}}=false;
                        {{source.ElementTypeName}} {{result}}=default!;
                        """,
                    source.BeginLoop,
                    $$"""
                        {{source.BeginLoopInside}}
                        {{result}}={{source.Element}};
                        {{found}}=true;
                        goto {{End}};
                        """,
                    source.EndLoopInside,
                    source.EndLoop,
                    $$"""
                        {{End}}:
                        {{source.EndLoopOutside}}
                        """,
                    source.EndGlobal,
                    $"{found} ? {result} : throw NoElements()");
            });

        this.Add(
            () => Array.Empty<int>().First(value => value>0),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var predicate = (FuncFragment)arguments[1];
                var Struct = $"Struct{番号}";
                var result = $"result{番号}";
                var found = $"found{番号}";
                var End = $"End{番号}";
                var Continue = $"Continue{番号}";
                return new Fragment(
                    $$"""
                        private struct {{Struct}}{
                            {{predicate.DefineMethod(nameof(predicate))}}
                        }
                        """,
                    source.BeginGlobal,
                    $$"""
                        {{source.BeginLoopOutside}}
                        var {{found}}=false;
                        {{source.ElementTypeName}} {{result}}=default!;
                        """,
                    source.BeginLoop,
                    $$"""
                        {{source.BeginLoopInside}}
                        if(!{{Struct}}.predicate({{source.Element}}))goto {{Continue}};
                        {{result}}={{source.Element}};
                        {{found}}=true;
                        goto {{End}};
                        {{Continue}}:;
                        """,
                    source.EndLoopInside,
                    source.EndLoop,
                    $$"""
                        {{End}}:
                        {{source.EndLoopOutside}}
                        """,
                    source.EndGlobal,
                    $"{found} ? {result} : throw NoElements()");
            });

        this.Add(
            () => Array.Empty<object>().Cast<int>(),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var result = $"result{番号}";
                var elementTypeName = TypeName<int>();
                return Carrierize(
                    番号,
                    string.Empty,
                    $$"""
                        {{source.BeginGlobal}}
                        var {{result}} = new global::System.Collections.Generic.List<{{elementTypeName}}>();
                        """,
                    $$"""
                        {{source.BeginLoopOutside}}
                        {{result}}.Clear();
                        {{source.BeginLoop}}
                            {{source.BeginLoopInside}}
                            {{result}}.Add(({{elementTypeName}}){{source.Element}}!);
                            {{source.EndLoopInside}}
                        {{source.EndLoop}}
                        {{source.EndLoopOutside}}
                        """,
                    result,
                    elementTypeName,
                    source.EndGlobal);
            });

        this.Add(
            () => Enumerable.Empty<int>(),
            (番号,_) => {
                var result = $"result{番号}";
                var elementTypeName = TypeName<int>();
                return Carrierize(
                    番号,
                    string.Empty,
                    $$"""
                        var {{result}} = global::Seamless.Linq.Enumerable.Empty<{{elementTypeName}}>();
                        """,
                    string.Empty,
                    result,
                    elementTypeName);
            });

        this.Add(
            () => Array.Empty<int>().GroupBy(value => value,(IEqualityComparer<int>?)null),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var keySelector = (FuncFragment)arguments[1];
                var comparer = arguments.Length>2 ? arguments[2] as string ?? "null" : "null";
                var Struct = $"Struct{番号}";
                var lookup = $"lookup{番号}";
                var result = $"result{番号}";
                var item = $"item{番号}";
                var keyTypeName = FuncReturnTypeName(keySelector);
                var groupingTypeName = $"global::IGrouping<{keyTypeName},{source.ElementTypeName}>";
                return Carrierize(
                    番号,
                    $$"""
                        private struct {{Struct}}{
                            {{keySelector.DefineMethod(nameof(keySelector))}}
                        }
                        """,
                    $$"""
                        {{source.BeginGlobal}}
                        var {{lookup}} = new global::Seamless.Linq.Enumerable.LookupImpl<{{keyTypeName}},{{source.ElementTypeName}}>({{comparer}});
                        var {{result}} = new global::System.Collections.Generic.List<{{groupingTypeName}}>();
                        """,
                    $$"""
                        {{result}}.Clear();
                        {{lookup}}.Groups.Clear();
                        {{source.BeginLoopOutside}}
                        {{source.BeginLoop}}
                            {{source.BeginLoopInside}}
                            {{lookup}}.Add({{Struct}}.keySelector({{source.Element}}),{{source.Element}});
                            {{source.EndLoopInside}}
                        {{source.EndLoop}}
                        {{source.EndLoopOutside}}
                        foreach(var {{item}} in {{lookup}}){{result}}.Add({{item}});
                        """,
                    result,
                    groupingTypeName,
                    source.EndGlobal);
            });

        this.Add(
            () => Array.Empty<int>().GroupBy(value => value,value => value.ToString(),(IEqualityComparer<int>?)null),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var keySelector = (FuncFragment)arguments[1];
                var elementSelector = (FuncFragment)arguments[2];
                var comparer = arguments.Length>3 ? arguments[3] as string ?? "null" : "null";
                var Struct = $"Struct{番号}";
                var lookup = $"lookup{番号}";
                var result = $"result{番号}";
                var item = $"item{番号}";
                var keyTypeName = FuncReturnTypeName(keySelector);
                var elementTypeName = FuncReturnTypeName(elementSelector);
                var groupingTypeName = $"global::IGrouping<{keyTypeName},{elementTypeName}>";
                return Carrierize(
                    番号,
                    $$"""
                        private struct {{Struct}}{
                            {{keySelector.DefineMethod(nameof(keySelector))}}
                            {{elementSelector.DefineMethod(nameof(elementSelector))}}
                        }
                        """,
                    $$"""
                        {{source.BeginGlobal}}
                        var {{lookup}} = new global::Seamless.Linq.Enumerable.LookupImpl<{{keyTypeName}},{{elementTypeName}}>({{comparer}});
                        var {{result}} = new global::System.Collections.Generic.List<{{groupingTypeName}}>();
                        """,
                    $$"""
                        {{result}}.Clear();
                        {{lookup}}.Groups.Clear();
                        {{source.BeginLoopOutside}}
                        {{source.BeginLoop}}
                            {{source.BeginLoopInside}}
                            {{lookup}}.Add({{Struct}}.keySelector({{source.Element}}),{{Struct}}.elementSelector({{source.Element}}));
                            {{source.EndLoopInside}}
                        {{source.EndLoop}}
                        {{source.EndLoopOutside}}
                        foreach(var {{item}} in {{lookup}}){{result}}.Add({{item}});
                        """,
                    result,
                    groupingTypeName,
                    source.EndGlobal);
            });

        this.Add(
            () => Array.Empty<int>().IntersectBy(Array.Empty<int>(),value => value,(IEqualityComparer<int>?)null),
            (番号,arguments) => {
                var first = (EnumerableFragment)arguments[0];
                var second = (string)arguments[1];
                var keySelector = (FuncFragment)arguments[2];
                var comparer = arguments.Length>3 ? arguments[3] as string ?? "null" : "null";
                var Struct = $"Struct{番号}";
                var temp = $"temp{番号}";
                var result = $"result{番号}";
                var keyTypeName = FuncReturnTypeName(keySelector);
                return Carrierize(
                    番号,
                    $$"""
                        private struct {{Struct}}{
                            {{keySelector.DefineMethod(nameof(keySelector))}}
                        }
                        """,
                    $$"""
                        {{first.BeginGlobal}}
                        var {{temp}} = new global::System.Collections.Generic.List<{{first.ElementTypeName}}>();
                        global::System.Collections.Generic.IEnumerable<{{first.ElementTypeName}}> {{result}} = default!;
                        """,
                    $$"""
                        {{MaterializeToList(first,temp)}}
                        {{result}} = global::Seamless.Linq.Enumerable.IntersectBy<{{first.ElementTypeName}},{{keyTypeName}}>({{temp}},{{second}},{{Struct}}.keySelector,{{comparer}});
                        """,
                    result,
                    first.ElementTypeName,
                    first.EndGlobal);
            });

        this.Add(
            () => Array.Empty<int>().Intersect(Array.Empty<int>(),(IEqualityComparer<int>?)null),
            (番号,arguments) => {
                var first = (EnumerableFragment)arguments[0];
                var second = (string)arguments[1];
                var comparer = arguments.Length>2 ? arguments[2] as string ?? "null" : "null";
                var temp = $"temp{番号}";
                var result = $"result{番号}";
                return Carrierize(
                    番号,
                    string.Empty,
                    $$"""
                        {{first.BeginGlobal}}
                        var {{temp}} = new global::System.Collections.Generic.List<{{first.ElementTypeName}}>();
                        global::System.Collections.Generic.IEnumerable<{{first.ElementTypeName}}> {{result}} = default!;
                        """,
                    $$"""
                        {{MaterializeToList(first,temp)}}
                        {{result}} = global::Seamless.Linq.Enumerable.Intersect<{{first.ElementTypeName}}>({{temp}},{{second}},{{comparer}});
                        """,
                    result,
                    first.ElementTypeName,
                    first.EndGlobal);
            });

        this.Add(
            () => Array.Empty<int>().LastOrDefault(),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var result = $"result{番号}";
                return new Fragment(
                    string.Empty,
                    source.BeginGlobal,
                    $$"""
                        {{source.BeginLoopOutside}}
                        {{source.ElementTypeName}} {{result}}=default!;
                        """,
                    source.BeginLoop,
                    $$"""
                        {{source.BeginLoopInside}}
                        {{result}}={{source.Element}};
                        """,
                    source.EndLoopInside,
                    source.EndLoop,
                    source.EndLoopOutside,
                    source.EndGlobal,
                    result);
            });

        this.Add(
            () => Array.Empty<int>().Last(),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var result = $"result{番号}";
                var found = $"found{番号}";
                return new Fragment(
                    string.Empty,
                    source.BeginGlobal,
                    $$"""
                        {{source.BeginLoopOutside}}
                        var {{found}}=false;
                        {{source.ElementTypeName}} {{result}}=default!;
                        """,
                    source.BeginLoop,
                    $$"""
                        {{source.BeginLoopInside}}
                        {{result}}={{source.Element}};
                        {{found}}=true;
                        """,
                    source.EndLoopInside,
                    source.EndLoop,
                    source.EndLoopOutside,
                    source.EndGlobal,
                    $"{found} ? {result} : throw NoElements()");
            });

        this.Add(
            () => Array.Empty<int>().LongCount(),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var result = $"result{番号}";
                return new Fragment(
                    string.Empty,
                    source.BeginGlobal,
                    $$"""
                        {{source.BeginLoopOutside}}
                        long {{result}}=0;
                        """,
                    source.BeginLoop,
                    $$"""
                        {{source.BeginLoopInside}}
                        checked{
                            {{result}}++;
                        }
                        """,
                    source.EndLoopInside,
                    source.EndLoop,
                    source.EndLoopOutside,
                    source.EndGlobal,
                    result);
            });

        this.Add(
            () => Array.Empty<int>().Max(),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var result = $"result{番号}";
                var found = $"found{番号}";
                var Continue = $"Continue{番号}";
                return new Fragment(
                    string.Empty,
                    source.BeginGlobal,
                    $$"""
                        {{source.BeginLoopOutside}}
                        var {{found}}=false;
                        int {{result}}=default;
                        """,
                    source.BeginLoop,
                    $$"""
                        {{source.BeginLoopInside}}
                        if(!{{found}}){
                            {{result}}={{source.Element}};
                            {{found}}=true;
                            goto {{Continue}};
                        }
                        if({{source.Element}}>{{result}}){{result}}={{source.Element}};
                        {{Continue}}:;
                        """,
                    source.EndLoopInside,
                    source.EndLoop,
                    source.EndLoopOutside,
                    source.EndGlobal,
                    $"{found} ? {result} : throw NoElements()");
            });

        this.Add(
            () => Array.Empty<int>().Min(),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var result = $"result{番号}";
                var found = $"found{番号}";
                var Continue = $"Continue{番号}";
                return new Fragment(
                    string.Empty,
                    source.BeginGlobal,
                    $$"""
                        {{source.BeginLoopOutside}}
                        var {{found}}=false;
                        int {{result}}=default;
                        """,
                    source.BeginLoop,
                    $$"""
                        {{source.BeginLoopInside}}
                        if(!{{found}}){
                            {{result}}={{source.Element}};
                            {{found}}=true;
                            goto {{Continue}};
                        }
                        if({{source.Element}}<{{result}}){{result}}={{source.Element}};
                        {{Continue}}:;
                        """,
                    source.EndLoopInside,
                    source.EndLoop,
                    source.EndLoopOutside,
                    source.EndGlobal,
                    $"{found} ? {result} : throw NoElements()");
            });

        this.Add(
            () => Array.Empty<object>().OfType<int>(),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var temp = $"temp{番号}";
                var typed = $"typed{番号}";
                var elementTypeName = TypeName<int>();
                return Carrierize(
                    番号,
                    string.Empty,
                    $$"""
                        {{source.BeginGlobal}}
                        var {{temp}} = new global::System.Collections.Generic.List<{{elementTypeName}}>();
                        """,
                    $$"""
                        {{temp}}.Clear();
                        {{source.BeginLoopOutside}}
                        {{source.BeginLoop}}
                            {{source.BeginLoopInside}}
                            if({{source.Element}} is {{elementTypeName}} {{typed}}){
                                {{temp}}.Add({{typed}});
                            }
                            {{source.EndLoopInside}}
                        {{source.EndLoop}}
                        {{source.EndLoopOutside}}
                        """,
                    temp,
                    elementTypeName,
                    source.EndGlobal);
            });

        this.Add(
            () => Array.Empty<int>().OrderByDescending(value => value,(IComparer<int>?)null),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var keySelector = (FuncFragment)arguments[1];
                var comparer = arguments.Length>2 ? arguments[2] as string ?? "null" : "null";
                var Struct = $"Struct{番号}";
                var buffer = $"buffer{番号}";
                var result = $"result{番号}";
                return Carrierize(
                    番号,
                    $$"""
                        private struct {{Struct}}{
                            {{keySelector.DefineMethod(nameof(keySelector))}}
                        }
                        """,
                    $$"""
                        {{source.BeginGlobal}}
                        var {{buffer}} = new global::System.Collections.Generic.List<{{source.ElementTypeName}}>();
                        global::IOrderedEnumerable<{{source.ElementTypeName}}> {{result}} = default!;
                        """,
                    $$"""
                        {{buffer}}.Clear();
                        {{source.BeginLoopOutside}}
                        {{source.BeginLoop}}
                            {{source.BeginLoopInside}}
                            {{buffer}}.Add({{source.Element}});
                            {{source.EndLoopInside}}
                        {{source.EndLoop}}
                        {{source.EndLoopOutside}}
                        {{result}} = global::Seamless.Linq.Enumerable.OrderByDescending<{{source.ElementTypeName}},{{FuncReturnTypeName(keySelector)}}>({{buffer}},{{Struct}}.keySelector,{{comparer}});
                        """,
                    result,
                    source.ElementTypeName,
                    source.EndGlobal);
            });

        this.Add(
            () => Array.Empty<int>().OrderBy(value => value,(IComparer<int>?)null),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var keySelector = (FuncFragment)arguments[1];
                var comparer = arguments.Length>2 ? arguments[2] as string ?? "null" : "null";
                var Struct = $"Struct{番号}";
                var buffer = $"buffer{番号}";
                var result = $"result{番号}";
                return Carrierize(
                    番号,
                    $$"""
                        private struct {{Struct}}{
                            {{keySelector.DefineMethod(nameof(keySelector))}}
                        }
                        """,
                    $$"""
                        {{source.BeginGlobal}}
                        var {{buffer}} = new global::System.Collections.Generic.List<{{source.ElementTypeName}}>();
                        global::IOrderedEnumerable<{{source.ElementTypeName}}> {{result}} = default!;
                        """,
                    $$"""
                        {{buffer}}.Clear();
                        {{source.BeginLoopOutside}}
                        {{source.BeginLoop}}
                            {{source.BeginLoopInside}}
                            {{buffer}}.Add({{source.Element}});
                            {{source.EndLoopInside}}
                        {{source.EndLoop}}
                        {{source.EndLoopOutside}}
                        {{result}} = global::Seamless.Linq.Enumerable.OrderBy<{{source.ElementTypeName}},{{FuncReturnTypeName(keySelector)}}>({{buffer}},{{Struct}}.keySelector,{{comparer}});
                        """,
                    result,
                    source.ElementTypeName,
                    source.EndGlobal);
            });

        this.Add(
            () => Array.Empty<int>().Prepend(0),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var element = (string)arguments[1];
                var temp = $"temp{番号}";
                return Carrierize(
                    番号,
                    string.Empty,
                    $$"""
                        {{source.BeginGlobal}}
                        var {{temp}} = new global::System.Collections.Generic.List<{{source.ElementTypeName}}>();
                        """,
                    $$"""
                        {{temp}}.Clear();
                        {{temp}}.Add({{element}});
                        {{source.BeginLoopOutside}}
                        {{source.BeginLoop}}
                            {{source.BeginLoopInside}}
                            {{temp}}.Add({{source.Element}});
                            {{source.EndLoopInside}}
                        {{source.EndLoop}}
                        {{source.EndLoopOutside}}
                        """,
                    temp,
                    source.ElementTypeName,
                    source.EndGlobal);
            });

        this.Add(
            () => Enumerable.Range(0,0),
            (番号,arguments) => {
                var start = (string)arguments[0];
                var count = (string)arguments[1];
                var result = $"result{番号}";
                var elementTypeName = TypeName<int>();
                return Carrierize(
                    番号,
                    string.Empty,
                    $$"""
                        var {{result}} = global::Seamless.Linq.Enumerable.Range({{start}},{{count}});
                        """,
                    string.Empty,
                    result,
                    elementTypeName);
            });

        this.Add(
            () => Enumerable.Repeat(0,0),
            (番号,arguments) => {
                var element = (string)arguments[0];
                var count = (string)arguments[1];
                var result = $"result{番号}";
                var elementTypeName = TypeName<int>();
                return Carrierize(
                    番号,
                    string.Empty,
                    $$"""
                        var {{result}} = global::Seamless.Linq.Enumerable.Repeat<{{elementTypeName}}>({{element}},{{count}});
                        """,
                    string.Empty,
                    result,
                    elementTypeName);
            });

        this.Add(
            () => Array.Empty<int>().Reverse(),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var temp = $"temp{番号}";
                return Carrierize(
                    番号,
                    string.Empty,
                    $$"""
                        {{source.BeginGlobal}}
                        var {{temp}} = new global::System.Collections.Generic.List<{{source.ElementTypeName}}>();
                        """,
                    $$"""
                        {{MaterializeToList(source,temp)}}
                        {{temp}}.Reverse();
                        """,
                    temp,
                    source.ElementTypeName,
                    source.EndGlobal);
            });

        this.Add(
            () => Array.Empty<int>().Select(value => value.ToString()),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var selector = (FuncFragment)arguments[1];
                var Struct = $"Struct{番号}";
                var result = $"result{番号}";
                var elementTypeName = FuncReturnTypeName(selector);
                return Carrierize(
                    番号,
                    StructDefinition(Struct,selector,nameof(selector)),
                    $$"""
                        {{source.BeginGlobal}}
                        var {{result}} = new global::System.Collections.Generic.List<{{elementTypeName}}>();
                        """,
                    $$"""
                        {{source.BeginLoopOutside}}
                        {{result}}.Clear();
                        {{source.BeginLoop}}
                            {{source.BeginLoopInside}}
                            {{result}}.Add({{Struct}}.selector({{source.Element}}));
                            {{source.EndLoopInside}}
                        {{source.EndLoop}}
                        {{source.EndLoopOutside}}
                        """,
                    result,
                    elementTypeName,
                    source.EndGlobal);
            });

        this.Add(
            () => Array.Empty<int>().Select((value,index) => value.ToString()),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var selector = (FuncFragment)arguments[1];
                var Struct = $"Struct{番号}";
                var result = $"result{番号}";
                var index = $"index{番号}";
                var elementTypeName = FuncReturnTypeName(selector);
                return Carrierize(
                    番号,
                    StructDefinition(Struct,selector,nameof(selector)),
                    $$"""
                        {{source.BeginGlobal}}
                        var {{result}} = new global::System.Collections.Generic.List<{{elementTypeName}}>();
                        """,
                    $$"""
                        {{source.BeginLoopOutside}}
                        {{result}}.Clear();
                        var {{index}}=-1;
                        {{source.BeginLoop}}
                            {{source.BeginLoopInside}}
                            checked{
                                {{index}}++;
                            }
                            {{result}}.Add({{Struct}}.selector({{source.Element}},{{index}}));
                            {{source.EndLoopInside}}
                        {{source.EndLoop}}
                        {{source.EndLoopOutside}}
                        """,
                    result,
                    elementTypeName,
                    source.EndGlobal);
            });

        this.Add(
            () => Enumerable.SequenceEqual(Array.Empty<int>(),Array.Empty<int>(),(IEqualityComparer<int>?)null),
            (番号,arguments) => {
                var first = (EnumerableFragment)arguments[0];
                var second = (string)arguments[1];
                var comparer = arguments.Length>2 ? arguments[2] as string ?? "null" : "null";
                var temp = $"temp{番号}";
                return new Fragment(
                    string.Empty,
                    $$"""
                        {{first.BeginGlobal}}
                        var {{temp}} = new global::System.Collections.Generic.List<{{first.ElementTypeName}}>();
                        """,
                    $$"""
                        {{MaterializeToList(first,temp)}}
                        """,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    first.EndGlobal,
                    string.Empty,
                    $"global::Seamless.Linq.Enumerable.SequenceEqual<{first.ElementTypeName}>({temp},{second},{comparer})");
            });

        this.Add(
            () => Array.Empty<int>().SkipWhile(value => value>0),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var predicate = (FuncFragment)arguments[1];
                var Struct = $"Struct{番号}";
                var temp = $"temp{番号}";
                var output = $"output{番号}";
                var start = $"Start{番号}";
                var end = $"End{番号}";
                var enumerator = $"e{番号}";
                return Carrierize(
                    番号,
                    StructDefinition(Struct,predicate,nameof(predicate)),
                    $$"""
                        {{source.BeginGlobal}}
                        var {{temp}} = new global::System.Collections.Generic.List<{{source.ElementTypeName}}>();
                        var {{output}} = new global::System.Collections.Generic.List<{{source.ElementTypeName}}>();
                        """,
                    $$"""
                        {{MaterializeToList(source,temp)}}
                        {{output}}.Clear();
                        var {{enumerator}}={{temp}}.GetEnumerator();
                        {{start}}:;
                        if(!{{enumerator}}.MoveNext())goto {{end}};
                        if({{Struct}}.predicate({{enumerator}}.Current))goto {{start}};
                        {{output}}.Add({{enumerator}}.Current);
                        while({{enumerator}}.MoveNext()){{output}}.Add({{enumerator}}.Current);
                        {{end}}:;
                        """,
                    output,
                    source.ElementTypeName,
                    source.EndGlobal);
            });

        this.Add(
            () => Array.Empty<int>().Skip(0),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var count = (string)arguments[1];
                var temp = $"temp{番号}";
                var output = $"output{番号}";
                var remaining = $"remaining{番号}";
                var start = $"Start{番号}";
                var end = $"End{番号}";
                var enumerator = $"e{番号}";
                return Carrierize(
                    番号,
                    string.Empty,
                    $$"""
                        {{source.BeginGlobal}}
                        var {{temp}} = new global::System.Collections.Generic.List<{{source.ElementTypeName}}>();
                        var {{output}} = new global::System.Collections.Generic.List<{{source.ElementTypeName}}>();
                        """,
                    $$"""
                        var {{remaining}}={{count}};
                        if({{remaining}}<0){{remaining}}=0;
                        {{MaterializeToList(source,temp)}}
                        {{output}}.Clear();
                        var {{enumerator}}={{temp}}.GetEnumerator();
                        {{start}}:;
                        if(!{{enumerator}}.MoveNext())goto {{end}};
                        if({{remaining}}>0){
                            {{remaining}}--;
                            goto {{start}};
                        }
                        {{output}}.Add({{enumerator}}.Current);
                        goto {{start}};
                        {{end}}:;
                        """,
                    output,
                    source.ElementTypeName,
                    source.EndGlobal);
            });

        this.Add(
            () => Array.Empty<int>().Sum(),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var result = $"result{番号}";
                return new Fragment(
                    string.Empty,
                    source.BeginGlobal,
                    $$"""
                        {{source.BeginLoopOutside}}
                        int {{result}}=0;
                        """,
                    source.BeginLoop,
                    $$"""
                        {{source.BeginLoopInside}}
                        checked{
                            {{result}}+={{source.Element}};
                        }
                        """,
                    source.EndLoopInside,
                    source.EndLoop,
                    source.EndLoopOutside,
                    source.EndGlobal,
                    result);
            });

        this.Add(
            () => Array.Empty<int>().Sum(value => value),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var selector = (FuncFragment)arguments[1];
                var Struct = $"Struct{番号}";
                var result = $"result{番号}";
                return new Fragment(
                    StructDefinition(Struct,selector,nameof(selector)),
                    source.BeginGlobal,
                    $$"""
                        {{source.BeginLoopOutside}}
                        int {{result}}=0;
                        """,
                    source.BeginLoop,
                    $$"""
                        {{source.BeginLoopInside}}
                        checked{
                            {{result}}+={{Struct}}.selector({{source.Element}});
                        }
                        """,
                    source.EndLoopInside,
                    source.EndLoop,
                    source.EndLoopOutside,
                    source.EndGlobal,
                    result);
            });

        this.Add(
            () => Array.Empty<int>().TakeWhile(value => value>0),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var predicate = (FuncFragment)arguments[1];
                var Struct = $"Struct{番号}";
                var temp = $"temp{番号}";
                var output = $"output{番号}";
                var start = $"Start{番号}";
                var end = $"End{番号}";
                var enumerator = $"e{番号}";
                return Carrierize(
                    番号,
                    StructDefinition(Struct,predicate,nameof(predicate)),
                    $$"""
                        {{source.BeginGlobal}}
                        var {{temp}} = new global::System.Collections.Generic.List<{{source.ElementTypeName}}>();
                        var {{output}} = new global::System.Collections.Generic.List<{{source.ElementTypeName}}>();
                        """,
                    $$"""
                        {{MaterializeToList(source,temp)}}
                        {{output}}.Clear();
                        var {{enumerator}}={{temp}}.GetEnumerator();
                        {{start}}:;
                        if(!{{enumerator}}.MoveNext())goto {{end}};
                        if(!{{Struct}}.predicate({{enumerator}}.Current))goto {{end}};
                        {{output}}.Add({{enumerator}}.Current);
                        goto {{start}};
                        {{end}}:;
                        """,
                    output,
                    source.ElementTypeName,
                    source.EndGlobal);
            });

        this.Add(
            () => Array.Empty<int>().Take(0),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var count = (string)arguments[1];
                var temp = $"temp{番号}";
                var output = $"output{番号}";
                var remaining = $"remaining{番号}";
                var start = $"Start{番号}";
                var end = $"End{番号}";
                var enumerator = $"e{番号}";
                return Carrierize(
                    番号,
                    string.Empty,
                    $$"""
                        {{source.BeginGlobal}}
                        var {{temp}} = new global::System.Collections.Generic.List<{{source.ElementTypeName}}>();
                        var {{output}} = new global::System.Collections.Generic.List<{{source.ElementTypeName}}>();
                        """,
                    $$"""
                        var {{remaining}}={{count}};
                        {{MaterializeToList(source,temp)}}
                        {{output}}.Clear();
                        if({{remaining}}<=0)goto {{end}};
                        var {{enumerator}}={{temp}}.GetEnumerator();
                        {{start}}:;
                        if(!{{enumerator}}.MoveNext())goto {{end}};
                        {{output}}.Add({{enumerator}}.Current);
                        {{remaining}}--;
                        if({{remaining}}<=0)goto {{end}};
                        goto {{start}};
                        {{end}}:;
                        """,
                    output,
                    source.ElementTypeName,
                    source.EndGlobal);
            });

        this.Add(
            () => Enumerable.Empty<int>().OrderBy(value => value).ThenByDescending(value => value,(IComparer<int>?)null),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var keySelector = (FuncFragment)arguments[1];
                var comparer = arguments.Length>2 ? arguments[2] as string ?? "null" : "null";
                var Struct = $"Struct{番号}";
                var result = $"result{番号}";
                return Carrierize(
                    番号,
                    StructDefinition(Struct,keySelector,nameof(keySelector)),
                    source.BeginGlobal,
                    $$"""
                        {{source.BeginLoopOutside}}
                        var {{result}} = global::Seamless.Linq.Enumerable.ThenByDescending<{{source.ElementTypeName}},{{FuncReturnTypeName(keySelector)}}>((global::IOrderedEnumerable<{{source.ElementTypeName}}>){{source.result}},{{Struct}}.keySelector,{{comparer}});
                        """,
                    result,
                    source.ElementTypeName,
                    source.EndGlobal);
            });

        this.Add(
            () => Enumerable.Empty<int>().OrderBy(value => value).ThenBy(value => value,(IComparer<int>?)null),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var keySelector = (FuncFragment)arguments[1];
                var comparer = arguments.Length>2 ? arguments[2] as string ?? "null" : "null";
                var Struct = $"Struct{番号}";
                var result = $"result{番号}";
                return Carrierize(
                    番号,
                    StructDefinition(Struct,keySelector,nameof(keySelector)),
                    source.BeginGlobal,
                    $$"""
                        {{source.BeginLoopOutside}}
                        var {{result}} = global::Seamless.Linq.Enumerable.ThenBy<{{source.ElementTypeName}},{{FuncReturnTypeName(keySelector)}}>((global::IOrderedEnumerable<{{source.ElementTypeName}}>){{source.result}},{{Struct}}.keySelector,{{comparer}});
                        """,
                    result,
                    source.ElementTypeName,
                    source.EndGlobal);
            });

        this.Add(
            () => Array.Empty<int>().ToArray(),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var buffer = $"buffer{番号}";
                return new Fragment(
                    string.Empty,
                    $$"""
                        {{source.BeginGlobal}}
                        var {{buffer}} = new global::System.Collections.Generic.List<{{source.ElementTypeName}}>();
                        """,
                    $$"""
                        {{source.BeginLoopOutside}}
                        {{buffer}}.Clear();
                        """,
                    source.BeginLoop,
                    $$"""
                        {{source.BeginLoopInside}}
                        {{buffer}}.Add({{source.Element}});
                        """,
                    source.EndLoopInside,
                    source.EndLoop,
                    source.EndLoopOutside,
                    source.EndGlobal,
                    $"{buffer}.ToArray()");
            });

        this.Add(
            () => Array.Empty<int>().ToList(),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var result = $"result{番号}";
                return new Fragment(
                    string.Empty,
                    $$"""
                        {{source.BeginGlobal}}
                        var {{result}} = new global::System.Collections.Generic.List<{{source.ElementTypeName}}>();
                        """,
                    $$"""
                        {{source.BeginLoopOutside}}
                        {{result}}.Clear();
                        """,
                    source.BeginLoop,
                    $$"""
                        {{source.BeginLoopInside}}
                        {{result}}.Add({{source.Element}});
                        """,
                    source.EndLoopInside,
                    source.EndLoop,
                    source.EndLoopOutside,
                    source.EndGlobal,
                    result);
            });

        this.Add(
            () => Array.Empty<int>().Where(value => value>0),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var predicate = (FuncFragment)arguments[1];
                var Struct = $"Struct{番号}";
                var result = $"result{番号}";
                return Carrierize(
                    番号,
                    StructDefinition(Struct,predicate,nameof(predicate)),
                    $$"""
                        {{source.BeginGlobal}}
                        var {{result}} = new global::System.Collections.Generic.List<{{source.ElementTypeName}}>();
                        """,
                    $$"""
                        {{source.BeginLoopOutside}}
                        {{result}}.Clear();
                        {{source.BeginLoop}}
                            {{source.BeginLoopInside}}
                            if({{Struct}}.predicate({{source.Element}})){{result}}.Add({{source.Element}});
                            {{source.EndLoopInside}}
                        {{source.EndLoop}}
                        {{source.EndLoopOutside}}
                        """,
                    result,
                    source.ElementTypeName,
                    source.EndGlobal);
            });

        this.Add(
            () => Array.Empty<int>().Where((value,index) => value>index),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var predicate = (FuncFragment)arguments[1];
                var Struct = $"Struct{番号}";
                var result = $"result{番号}";
                var index = $"index{番号}";
                return Carrierize(
                    番号,
                    StructDefinition(Struct,predicate,nameof(predicate)),
                    $$"""
                        {{source.BeginGlobal}}
                        var {{result}} = new global::System.Collections.Generic.List<{{source.ElementTypeName}}>();
                        """,
                    $$"""
                        {{source.BeginLoopOutside}}
                        {{result}}.Clear();
                        var {{index}}=-1;
                        {{source.BeginLoop}}
                            {{source.BeginLoopInside}}
                            checked{
                                {{index}}++;
                            }
                            if({{Struct}}.predicate({{source.Element}},{{index}})){{result}}.Add({{source.Element}});
                            {{source.EndLoopInside}}
                        {{source.EndLoop}}
                        {{source.EndLoopOutside}}
                        """,
                    result,
                    source.ElementTypeName,
                    source.EndGlobal);
            });

        this.Add(
            () => Array.Empty<int>().Zip(Array.Empty<string>()),
            (番号,arguments) => {
                var first = (EnumerableFragment)arguments[0];
                var second = (EnumerableFragment)arguments[1];
                var result = $"result{番号}";
                var tupleTypeName = $"({first.ElementTypeName}, {second.ElementTypeName})";
                var Head = $"Head{番号}";
                var End = $"End{番号}";
                return Carrierize(
                    番号,
                    string.Empty,
                    $$"""
                        var {{result}}= new global::System.Collections.Generic.List<{{tupleTypeName}}>();
                        """,
                    $$"""
                        {{first.BeginLoopOutside}}
                        {{second.BeginLoopOutside}}
                        {{Head}}:
                        {{first.BeginLoopInside}}
                        {{first.If_Not_MoveNext_goto(End)}}
                        {{second.BeginLoopInside}}
                        {{second.If_Not_MoveNext_goto(End)}}
                        {{result}}.Add(({{first.Element}}, {{second.Element}}));
                        {{first.EndLoopInside}}
                        {{second.EndLoopInside}}
                        goto {{Head}};
                        {{End}}:
                        {{first.EndLoopOutside}}
                        {{second.EndLoopOutside}}
                        """,
                    result,
                    tupleTypeName,
                    $"{first.EndGlobal}\n{second.EndGlobal}");
            });

        this.Add(
            () => Array.Empty<int>().Zip(Array.Empty<string>(),Array.Empty<double>()),
            (番号,arguments) => {
                var first = (EnumerableFragment)arguments[0];
                var second = (EnumerableFragment)arguments[1];
                var third = (EnumerableFragment)arguments[2];
                var result = $"result{番号}";
                var tupleTypeName = $"({first.ElementTypeName}, {second.ElementTypeName}, {third.ElementTypeName})";
                var Head = $"Head{番号}";
                var End = $"End{番号}";
                return Carrierize(
                    番号,
                    string.Empty,
                    $$"""
                        var {{result}}= new global::System.Collections.Generic.List<{{tupleTypeName}}>();
                        """,
                    $$"""
                        {{first.BeginLoopOutside}}
                        {{second.BeginLoopOutside}}
                        {{third.BeginLoopOutside}}
                        {{Head}}:
                        {{first.BeginLoopInside}}
                        {{first.If_Not_MoveNext_goto(End)}}
                        {{second.BeginLoopInside}}
                        {{second.If_Not_MoveNext_goto(End)}}
                        {{third.BeginLoopInside}}
                        {{third.If_Not_MoveNext_goto(End)}}
                        {{result}}.Add(({{first.Element}}, {{second.Element}}, {{third.Element}}));
                        {{first.EndLoopInside}}
                        {{second.EndLoopInside}}
                        {{third.EndLoopInside}}
                        goto {{Head}};
                        {{End}}:
                        {{first.EndLoopOutside}}
                        {{second.EndLoopOutside}}
                        {{third.EndLoopOutside}}
                        """,
                    result,
                    tupleTypeName,
                    $"{first.EndGlobal}\n{second.EndGlobal}\n{third.EndGlobal}");
            });

        this.Add(
            () => Array.Empty<int>().Zip(Array.Empty<string>(),(first,second) => first.ToString()+second),
            (番号,arguments) => {
                var first = (EnumerableFragment)arguments[0];
                var second = (EnumerableFragment)arguments[1];
                var resultSelector = (FuncFragment)arguments[2];
                var Struct = $"Struct{番号}";
                var result = $"result{番号}";
                var elementTypeName = FuncReturnTypeName(resultSelector);
                var Head = $"Head{番号}";
                var End = $"End{番号}";
                return Carrierize(
                    番号,
                    StructDefinition(Struct,resultSelector,nameof(resultSelector)),
                    $$"""
                        var {{result}}= new global::System.Collections.Generic.List<{{elementTypeName}}>();
                        """,
                    $$"""
                        {{first.BeginLoopOutside}}
                        {{second.BeginLoopOutside}}
                        {{Head}}:
                        {{first.BeginLoopInside}}
                        {{first.If_Not_MoveNext_goto(End)}}
                        {{second.BeginLoopInside}}
                        {{second.If_Not_MoveNext_goto(End)}}
                        {{result}}.Add({{Struct}}.resultSelector({{first.Element}}, {{second.Element}}));
                        {{first.EndLoopInside}}
                        {{second.EndLoopInside}}
                        goto {{Head}};
                        {{End}}:
                        {{first.EndLoopOutside}}
                        {{second.EndLoopOutside}}
                        """,
                    result,
                    elementTypeName,
                    $"{first.EndGlobal}\n{second.EndGlobal}");
            });

        this.Add(
            () => Array.Empty<int>().SelectMany(value => Array.Empty<string>()),
            (番号,arguments) => {
                var source = (EnumerableFragment)arguments[0];
                var selector = (SelectMany_selector)arguments[1];
                var Struct = $"Struct{番号}";
                var result = $"result{番号}";
                var element = $"output{番号}";
                var elementTypeName = selector.Source is EnumerableFragment nestedSource
                    ? nestedSource.ElementTypeName
                    : SequenceElementTypeName(FuncReturnTypeName(selector));
                var staticMethod = selector.Source is null
                    ? StructDefinition(Struct,selector,nameof(selector))
                    : string.Empty;
                var beginLoopOutside = selector.Source is null
                    ? $$"""
                        {{source.BeginLoopOutside}}
                        {{result}}.Clear();
                        {{source.BeginLoop}}
                            {{source.BeginLoopInside}}
                            foreach(var {{element}} in {{Struct}}.selector({{source.Element}})){
                                {{result}}.Add({{element}});
                            }
                            {{source.EndLoopInside}}
                        {{source.EndLoop}}
                        {{source.EndLoopOutside}}
                        """
                    : $$"""
                        {{source.BeginLoopOutside}}
                        {{result}}.Clear();
                        {{source.BeginLoop}}
                            {{source.BeginLoopInside}}
                            {{((EnumerableFragment)selector.Source).BeginLoopOutside}}
                            {{((EnumerableFragment)selector.Source).BeginLoop}}
                                {{((EnumerableFragment)selector.Source).BeginLoopInside}}
                                {{result}}.Add({{((EnumerableFragment)selector.Source).Element}});
                                {{((EnumerableFragment)selector.Source).EndLoopInside}}
                            {{((EnumerableFragment)selector.Source).EndLoop}}
                            {{((EnumerableFragment)selector.Source).EndLoopOutside}}
                            {{source.EndLoopInside}}
                        {{source.EndLoop}}
                        {{source.EndLoopOutside}}
                        """;
                var endGlobal = selector.Source is EnumerableFragment nested
                    ? $"{source.EndGlobal}\n{nested.EndGlobal}"
                    : source.EndGlobal;
                return Carrierize(
                    番号,
                    staticMethod,
                    $$"""
                        {{source.BeginGlobal}}
                        var {{result}} = new global::System.Collections.Generic.List<{{elementTypeName}}>();
                        """,
                    beginLoopOutside,
                    result,
                    elementTypeName,
                    endGlobal);
            });

        this.Add(
            () => Array.Empty<int>().UnionBy(Array.Empty<int>(),value => value,(IEqualityComparer<int>?)null),
            (番号,arguments) => {
                var first = (EnumerableFragment)arguments[0];
                var second = (EnumerableFragment)arguments[1];
                var keySelector = (FuncFragment)arguments[2];
                var comparer = arguments.Length>3 ? arguments[3] as string ?? "null" : "null";
                var Struct = $"Struct{番号}";
                var seen = $"seen{番号}";
                var result = $"result{番号}";
                return Carrierize(
                    番号,
                    StructDefinition(Struct,keySelector,nameof(keySelector)),
                    $$"""
                        var {{seen}} = new global::System.Collections.Generic.HashSet<{{FuncReturnTypeName(keySelector)}}>({{comparer}});
                        var {{result}} = new global::System.Collections.Generic.List<{{first.ElementTypeName}}>();
                        """,
                    $$"""
                        {{seen}}.Clear();
                        {{result}}.Clear();
                        {{first.BeginLoopOutside}}
                        {{first.BeginLoop}}
                            {{first.BeginLoopInside}}
                            if({{seen}}.Add({{Struct}}.keySelector({{first.Element}}))){{result}}.Add({{first.Element}});
                            {{first.EndLoopInside}}
                        {{first.EndLoop}}
                        {{first.EndLoopOutside}}
                        {{second.BeginLoopOutside}}
                        {{second.BeginLoop}}
                            {{second.BeginLoopInside}}
                            if({{seen}}.Add({{Struct}}.keySelector({{second.Element}}))){{result}}.Add({{second.Element}});
                            {{second.EndLoopInside}}
                        {{second.EndLoop}}
                        {{second.EndLoopOutside}}
                        """,
                    result,
                    first.ElementTypeName,
                    $"{first.EndGlobal}\n{second.EndGlobal}");
            });

        this.Add(
            () => Array.Empty<int>().Union(Array.Empty<int>()),
            (番号,arguments) => {
                var first = (EnumerableFragment)arguments[0];
                var second = (EnumerableFragment)arguments[1];
                var result = $"result{番号}";
                var seen = $"seen{番号}";
                return Carrierize(
                    番号,
                    string.Empty,
                    $$"""
                        var {{seen}} = new global::System.Collections.Generic.HashSet<{{first.ElementTypeName}}>();
                        var {{result}} = new global::System.Collections.Generic.List<{{first.ElementTypeName}}>();
                        """,
                    $$"""
                        {{seen}}.Clear();
                        {{result}}.Clear();
                        {{first.BeginLoopOutside}}
                        {{first.BeginLoop}}
                            {{first.BeginLoopInside}}
                            if({{seen}}.Add({{first.Element}})){{result}}.Add({{first.Element}});
                            {{first.EndLoopInside}}
                        {{first.EndLoop}}
                        {{first.EndLoopOutside}}
                        {{second.BeginLoopOutside}}
                        {{second.BeginLoop}}
                            {{second.BeginLoopInside}}
                            if({{seen}}.Add({{second.Element}})){{result}}.Add({{second.Element}});
                            {{second.EndLoopInside}}
                        {{second.EndLoop}}
                        {{second.EndLoopOutside}}
                        """,
                    result,
                    first.ElementTypeName,
                    $"{first.EndGlobal}\n{second.EndGlobal}");
            });

        this.Add(
            () => Array.Empty<int>().Union(Array.Empty<int>(),(IEqualityComparer<int>?)null),
            (番号,arguments) => {
                var first = (EnumerableFragment)arguments[0];
                var second = (EnumerableFragment)arguments[1];
                var comparer = arguments.Length>2 ? arguments[2] as string ?? "null" : "null";
                var result = $"result{番号}";
                var seen = $"seen{番号}";
                return Carrierize(
                    番号,
                    string.Empty,
                    $$"""
                        var {{seen}} = new global::System.Collections.Generic.HashSet<{{first.ElementTypeName}}>({{comparer}});
                        var {{result}} = new global::System.Collections.Generic.List<{{first.ElementTypeName}}>();
                        """,
                    $$"""
                        {{seen}}.Clear();
                        {{result}}.Clear();
                        {{first.BeginLoopOutside}}
                        {{first.BeginLoop}}
                            {{first.BeginLoopInside}}
                            if({{seen}}.Add({{first.Element}})){{result}}.Add({{first.Element}});
                            {{first.EndLoopInside}}
                        {{first.EndLoop}}
                        {{first.EndLoopOutside}}
                        {{second.BeginLoopOutside}}
                        {{second.BeginLoop}}
                            {{second.BeginLoopInside}}
                            if({{seen}}.Add({{second.Element}})){{result}}.Add({{second.Element}});
                            {{second.EndLoopInside}}
                        {{second.EndLoop}}
                        {{second.EndLoopOutside}}
                        """,
                    result,
                    first.ElementTypeName,
                    $"{first.EndGlobal}\n{second.EndGlobal}");
            });
    }

    internal bool TryResolve(string signature,Fragment source,out Fragment fragment) {
        if(this.dictionaryFragment.TryGetValue(signature,out var template)) {
            fragment=template(0,new object[] { source });
            return true;
        }

        fragment=null!;
        return false;
    }

    internal bool TryResolve(string signature,Fragment source,out Fragment fragment,params object[] arguments) {
        if(this.dictionaryFragment.TryGetValue(signature,out var template)) {
            var forwardedArguments = new object[arguments.Length+1];
            forwardedArguments[0]=source;
            Array.Copy(arguments,0,forwardedArguments,1,arguments.Length);
            fragment=template(0,forwardedArguments);
            return true;
        }

        fragment=null!;
        return false;
    }

    internal bool TryResolve(string signature,int 番号,Fragment source,out Fragment fragment,params object[] arguments) {
        if(this.dictionaryFragment.TryGetValue(signature,out var template)) {
            var forwardedArguments = new object[arguments.Length+1];
            forwardedArguments[0]=source;
            Array.Copy(arguments,0,forwardedArguments,1,arguments.Length);
            fragment=template(番号,forwardedArguments);
            return true;
        }

        fragment=null!;
        return false;
    }

    internal bool TryResolve(IMethodSymbol method,Fragment source,out Fragment fragment,params object[] arguments) {
        if(method is null) {
            throw new ArgumentNullException(nameof(method));
        }

        var signature = method.OriginalDefinition.ToDisplayString(TemplateMethodSignatureFormat);
        return this.TryResolve(signature,source,out fragment,arguments);
    }

    internal bool TryResolve(IMethodSymbol method,int 番号,Fragment source,out Fragment fragment,params object[] arguments) {
        if(method is null) {
            throw new ArgumentNullException(nameof(method));
        }

        var signature = method.OriginalDefinition.ToDisplayString(TemplateMethodSignatureFormat);
        return this.TryResolve(signature,番号,source,out fragment,arguments);
    }

    internal bool TryResolveAggregateFunc(Fragment source,FuncFragment func,out Fragment fragment,int 番号 = 0) {
        var signature = this.L(() => Array.Empty<int>().Aggregate((left,right) => left+right));
        return this.TryResolve(signature,番号,source,out fragment,func);
    }

    internal static Fragment ResolveAggregateFunc(EnumerableFragment source,FuncFragment func,int 番号 = 0) {
        var template = new Template();
        if(template.TryResolveAggregateFunc(source,func,out var fragment,番号)) {
            return fragment;
        }

        throw new InvalidOperationException("Could not resolve Aggregate fragment template.");
    }

    private static Compilation CreateTemplateCompilation() {
        var references = new[] {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Collections.Generic.IEnumerable<>).Assembly.Location),
        };
        return CSharpCompilation.Create(
            assemblyName: "Seamless.SourceGenerator.Template",
            references: references);
    }
}
