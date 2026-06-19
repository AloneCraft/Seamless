using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using static Tsavorite.SourceGenerator.GeneratorHelpers;

using Tsavorite.SourceGenerator.Linq.Fragments;
using Enumerable = Tsavorite.SourceGenerator.Linq.Fragments.Enumerable;

namespace Tsavorite.SourceGenerator;

public sealed partial class TsavoriteIncrementalGenerator {

    private sealed class フラグメント候補 {
        public required Type FragmentType { get; init; }
        public required ConstructorInfo Constructor { get; init; }
        public required string MethodName { get; init; }
        public required string VariantName { get; init; }
        public required ParameterInfo[] Parameters { get; init; }
        public Func<IMethodSymbol, bool>? IsApplicableOverload { get; init; }
    }

    private static readonly SymbolDisplayFormat 完全修飾形式 = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.ExpandNullable
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
            | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);
    private static readonly SymbolDisplayFormat cref形式 = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);
    // Fragments.Fragment を継承した具体型を列挙し、ローカルメソッド再生成にもそのまま使う。
    // これで Aggregate の overload 個別対応を増やすたびに辞書を手で更新しなくてよい。
    private static readonly IReadOnlyDictionary<string,ImmutableArray<フラグメント候補>> フラグメント候補一覧 = フラグメント候補一覧構築();
    private static readonly IReadOnlySet<string> SystemLinqEnumerableMethodNames = typeof(global::System.Linq.Enumerable)
        .GetMethods(BindingFlags.Public|BindingFlags.Static)
        .Select(static method=>method.Name)
        .ToHashSet(StringComparer.Ordinal);
    private static readonly ConditionalWeakTable<Compilation,System.Collections.Concurrent.ConcurrentDictionary<IMethodSymbol,ローカルメソッドLinq解析結果>> ローカルメソッドLinq解析キャッシュ = new();

    // 匿名型を含む LINQ が検出されたとき、インターセプト元のメソッドに対して発行する警告。
    // 匿名型は生成メソッドの署名に出せないため完全なフラグメント最適化ができず、
    // 匿名型を含む LINQ 呼び出しのみ IEnumerable<T> 汎用ヘルパーへ委譲する部分最適化になる。
    // ValueTuple や struct record を使えば完全最適化が可能になることをユーザーに伝える。
    private static readonly DiagnosticDescriptor 匿名型警告Descriptor = new(
        id: "Tsavorite001",
        title: "匿名型が検出されました",
        messageFormat: "'{0}' に匿名型を含む LINQ 式が検出されました。最適化可能な部分だけインターセプトします。ValueTuple または struct record を使用することを検討してください。",
        category: "Tsavorite.Performance",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "匿名型を含む LINQ 式は、生成メソッドの署名に匿名型を記述できないため完全なフラグメント最適化が適用できません。ValueTuple (例: (int x, int y)) または struct record を使うことで最適化が有効になります.");

    // LINQ インターセプト対象メソッドで展開に失敗し、生成コードが例外を throw する形になるとき発行する警告。
    // これにより「インターセプトはされたが展開できなかった」ことをコンパイル時に検出できる。
    // 警告が出ない = 全 LINQ 呼び出しがインライン展開されている ことを確認するために使う。
    private static readonly DiagnosticDescriptor 展開失敗警告Descriptor = new(
        id: "Tsavorite002",
        title: "LINQ インターセプト展開失敗",
        messageFormat: "'{0}' の LINQ 呼び出しをインライン展開できませんでした。実行時に例外が発生します。未対応メソッドまたはデリゲートパターンが含まれている可能性があります。",
        category: "Tsavorite.Performance",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "対象メソッド内の LINQ 呼び出しをインライン展開できなかったため、生成されたインターセプターコードは実行時に例外を発生させます。Fragment 未実装のメソッド、または未対応のデリゲートパターンが含まれている可能性があります.");
    private sealed class ローカルメソッドLinq解析結果(bool hasEnumerableCall,ImmutableArray<string> unsupportedMethods,bool hasAnonymousTypeLinq) {
        public static readonly ローカルメソッドLinq解析結果 Empty = new(false,ImmutableArray<string>.Empty,false);
        public bool HasEnumerableCall { get; } = hasEnumerableCall;
        public ImmutableArray<string> UnsupportedMethods { get; } = unsupportedMethods;
        /// <summary>
        /// メソッド本体内の LINQ 呼び出しに匿名型が含まれる場合 true。
        /// 匿名型は生成メソッドの署名に出せないため完全なフラグメント最適化ができず、
        /// 該当 LINQ 呼び出しは IEnumerable&lt;T&gt; 汎用ヘルパーへ委譲する部分最適化になる。
        /// ValueTuple や struct record を使えば完全最適化が可能になるため、
        /// この値が true のとき Tsavorite001 警告を発行してユーザーに通知する。
        /// </summary>
        public bool HasAnonymousTypeLinq { get; } = hasAnonymousTypeLinq;
    }
    private sealed class Linq傍受対象型(string identifier,string containingMethodName,string interceptsLocationAttributeText,IMethodSymbol method,IMethodSymbol templateMethod,InvocationExpressionSyntax invocation,INamedTypeSymbol enclosingType,SemanticModel semanticModel,TsavoriteIncrementalGenerator.InterceptParameterSpec[] parameters,TsavoriteIncrementalGenerator.ローカルメソッドLinq解析結果? linqAnalysis = null,string? sourceElementTypeName = null,string[]? methodTypeParameterNames = null,bool isLocalMethodInterception = false,bool optimizeLinq = true,bool counterInstrumentation = false):IEquatable<Linq傍受対象型> {
        public bool Equals(Linq傍受対象型? other) {
            if(other is null) return false;
            if(ReferenceEquals(this,other)) return true;
            return string.Equals(this.InterceptsLocationAttributeText,other.InterceptsLocationAttributeText,StringComparison.Ordinal);
        }
        public override bool Equals(object? obj)=>obj is Linq傍受対象型 other &&this.Equals(other);
        public override int GetHashCode()=>this.InterceptsLocationAttributeText.GetHashCode(StringComparison.Ordinal);
        private string Identifier { get; } = identifier;
        public string ContainingMethodName { get; } = containingMethodName;
        public string InterceptsLocationAttributeText { get; } = interceptsLocationAttributeText;
        public IMethodSymbol Method { get; } = method;
        private IMethodSymbol TemplateMethod { get; } = templateMethod;
        public InvocationExpressionSyntax Invocation { get; } = invocation;
        public string SourcePath=>this.Invocation.SyntaxTree.FilePath;
        public INamedTypeSymbol EnclosingType { get; } = enclosingType;
        public SemanticModel SemanticModel { get; } = semanticModel;
        private InterceptParameterSpec[] Parameters { get; } = parameters;
        public ローカルメソッドLinq解析結果 LinqAnalysis { get; } = linqAnalysis??ローカルメソッドLinq解析結果.Empty;
        private string? ExplicitSourceElementTypeName { get; } = sourceElementTypeName;
        private string[] MethodTypeParameters { get; } = methodTypeParameterNames?? [];
        public bool IsLocalMethodInterception { get; } = isLocalMethodInterception;
        public bool OptimizeLinq { get; } = optimizeLinq;
        public bool CounterInstrumentation { get; } = counterInstrumentation;
        private string SourceElementTypeName {
            get {
                if(!string.IsNullOrWhiteSpace(this.ExplicitSourceElementTypeName)) {
                    return this.ExplicitSourceElementTypeName;
                }
                if(this.Method.TypeArguments.Length>0) {
                    return this.Method.TypeArguments[0].ToDisplayString(完全修飾形式);
                }
                var parameters = this.TemplateMethod.Parameters;
                if(parameters.Length==0
                    ||parameters[0].Type is not INamedTypeSymbol sourceType
                    ||sourceType.TypeArguments.Length!=1) {
                    throw new InvalidOperationException($"Could not determine source element type for '{this.TemplateMethod.ToDisplayString(完全修飾形式)}'.");
                }
                return sourceType.TypeArguments[0].ToDisplayString(完全修飾形式);
            }
        }
        public string ReturnTypeName=>this.Method.ReturnsVoid ? "void" : this.Method.ReturnType.ToDisplayString(完全修飾形式);
        public string InterceptorParameterList {
            get {
                if(this.IsLocalMethodInterception) {
                    var parameters = new List<string>();
                    if(!this.Method.IsStatic) {
                        parameters.Add($"this {this.Method.ContainingType.ToDisplayString(完全修飾形式)} @this");
                    }
                    parameters.AddRange(this.Parameters.Select(static parameter=>parameter.Declaration));
                    return string.Join(", ",parameters);
                }
                return $"this global::System.Collections.Generic.IEnumerable<{this.SourceElementTypeName}> source{this.MethodParameterList}";
            }
        }
        private string MethodParameterList=>this.Parameters.Length==0
            ? string.Empty
            : ", "+string.Join(", ",this.Parameters.Select(static parameter=>parameter.Declaration));
        public string MethodTypeParameterList=>this.MethodTypeParameters.Length==0
            ? string.Empty
            : "<"+string.Join(", ",this.MethodTypeParameters)+">";
        public string MethodName=>$"{(this.OptimizeLinq ? "OptimizedLinq" : "MeasuredLinq")}_{this.Identifier}";
    }
    private sealed class InterceptParameterSpec(string name,string typeName,RefKind refKind = RefKind.None) {
        private string Name { get; } = name;
        private string TypeName { get; } = typeName;
        private RefKind RefKind { get; } = refKind;
        public string Declaration=>$"{this.RefKindModifier}{this.TypeName} {this.Name}";
        private string RefKindModifier=>this.RefKind switch {
            RefKind.Ref=>"ref ",
            RefKind.Out=>"out ",
            RefKind.In=>"in ",
            _=>string.Empty,
        };
    }
    private sealed class LambdaSpec(string returnTypeName,string[] parameterTypeNames,string[] parameterNames,string body) {
        public string ReturnTypeName { get; } = returnTypeName;
        public string[] ParameterTypeNames { get; } = parameterTypeNames;
        public string[] ParameterNames { get; } = parameterNames;
        public string Body { get; } = body;
    }
    private sealed class LinqInterceptTargetComparer : IEqualityComparer<Linq傍受対象型> {
        public static readonly LinqInterceptTargetComparer Instance = new();
        public bool Equals(Linq傍受対象型? x,Linq傍受対象型? y) {
            if(ReferenceEquals(x,y)) {
                return true;
            }
            if(x is null || y is null) {
                return false;
            }
            return x.InterceptsLocationAttributeText==y.InterceptsLocationAttributeText;
        }
        public int GetHashCode(Linq傍受対象型 obj) {
            return obj.InterceptsLocationAttributeText.GetHashCode(StringComparison.Ordinal);
        }
    }
    private sealed class PerFileInterceptGroup(string sourcePath,ImmutableArray<TsavoriteIncrementalGenerator.Linq傍受対象型> targets):IEquatable<PerFileInterceptGroup> {
        public string SourcePath { get; } = sourcePath;
        public ImmutableArray<Linq傍受対象型> Targets { get; } = targets;
        public bool Equals(PerFileInterceptGroup? other) {
            if(other is null) return false;
            if(ReferenceEquals(this,other)) return true;
            if(!string.Equals(this.SourcePath,other.SourcePath,StringComparison.Ordinal)) return false;
            if(this.Targets.Length!=other.Targets.Length) return false;
            for(var i = 0;i<this.Targets.Length;i++) {
                if(!this.Targets[i].Equals(other.Targets[i])) return false;
            }
            return true;
        }
        public override bool Equals(object? obj)=>obj is PerFileInterceptGroup other &&this.Equals(other);
        public override int GetHashCode() {
            var hc = StringComparer.Ordinal.GetHashCode(this.SourcePath);
            foreach(var t in this.Targets) {
                hc=HashCode.Combine(hc,t.GetHashCode());
            }
            return hc;
        }
    }
    private sealed class 型対象型(INamedTypeSymbol symbol,bool isPartial) {
        public INamedTypeSymbol Symbol { get; } = symbol;
        public bool IsPartial { get; } = isPartial;
    }
}
