using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

#pragma warning disable RS2008

namespace Seamless.SourceGenerator;

[Generator]
// この generator は 2 系統の生成をまとめて担当する。
// 1. List<T>.Where を軽量 enumerable に差し替える interceptor 系コード
// 2. DatabaseGenerator マーカー配下の schema / optimized container コード
public sealed class SeamlessIncrementalGenerator:IIncrementalGenerator {
    private const string DiagnosticCategory = "Seamless.SourceGenerator";
    private const string EnumerableImplementationResourceName = "Seamless.Linq.Enumerable.cs";
    private const string HashSetImplementationResourceName = "Seamless.Linq.HashSet.cs";

    private static readonly Lazy<LinqImplementationSource?> EnumerableImplementation = new(() => LoadLinqImplementationSource(EnumerableImplementationResourceName));
    private static readonly Lazy<LinqImplementationSource?> HashSetImplementation = new(() => LoadLinqImplementationSource(HashSetImplementationResourceName));

    private static readonly DiagnosticDescriptor OptimizeClassMustBeNonSealed = new(
        "SEAMLESS001",
        "[Optimize] can only be used on non-sealed classes",
        "[Optimize] は非 sealed class にのみ指定できます。'{0}' は継承できないため Optimized{0} を生成できません。",
        DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor OptimizeMethodMustBeVirtual = new(
        "SEAMLESS002",
        "[Optimize] can only be used on virtual methods",
        "[Optimize] は body を持つ virtual instance method にのみ指定できます。'{0}' は override 最適化の対象にできません。",
        DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor OptimizeMethodRequiresOptimizeClass = new(
        "SEAMLESS003",
        "[Optimize] method requires an [Optimize] class",
        "[Optimize] method '{0}' を最適化するには、包含 class '{1}' にも [Optimize] を指定してください。",
        DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly SymbolDisplayFormat FullyQualifiedFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.ExpandNullable
    );

    // SyntaxProvider は候補を粗く拾い、後段で意味解析して本対象だけを残す。
    public void Initialize(IncrementalGeneratorInitializationContext context) {
        context.RegisterPostInitializationOutput(static postInitializationContext => postInitializationContext.AddSource(
            "Seamless.SourceGenerator.OptimizeAttribute.g.cs",
            SourceText.From(BuildOptimizeAttributeSource(),Encoding.UTF8)));

        var whereTargets = context.SyntaxProvider
            .CreateSyntaxProvider(static (node,_) => node is InvocationExpressionSyntax,static (generatorContext,cancellationToken) => CreateWhereInterceptTarget(generatorContext,cancellationToken))
            .Where(static target => target is not null)
            .Select(static (target,_) => target!);

        var containerMethodTargets = context.SyntaxProvider
            .CreateSyntaxProvider(static (node,_) => node is InvocationExpressionSyntax,static (generatorContext,cancellationToken) => CreateContainerMethodInterceptTarget(generatorContext,cancellationToken))
            .Where(static target => target is not null)
            .Select(static (target,_) => target!);

        var linqInvocationTargets = context.SyntaxProvider
            .CreateSyntaxProvider(static (node,_) => node is InvocationExpressionSyntax,static (generatorContext,cancellationToken) => CreateLinqInvocationInterceptTarget(generatorContext,cancellationToken))
            .Where(static target => target is not null)
            .Select(static (target,_) => target!);

        var typeTargets = context.SyntaxProvider
            .CreateSyntaxProvider(static (node,_) => node is TypeDeclarationSyntax,static (generatorContext,_) => CreateTypeTarget(generatorContext))
            .Where(static target => target is not null)
            .Select(static (target,_) => target!);

        var optimizeMethodTargets = context.SyntaxProvider
            .CreateSyntaxProvider(static (node,_) => node is MethodDeclarationSyntax { AttributeLists.Count:>0 },static (generatorContext,_) => CreateOptimizeMethodTarget(generatorContext))
            .Where(static target => target is not null)
            .Select(static (target,_) => target!);

        var projectDirectory = context.AnalyzerConfigOptionsProvider.Select(static (options,_) => {
            if(options.GlobalOptions.TryGetValue("build_property.MSBuildProjectDirectory",out var msBuildProjectDirectory)&&!string.IsNullOrWhiteSpace(msBuildProjectDirectory)) {
                return msBuildProjectDirectory;
            }
            if(options.GlobalOptions.TryGetValue("build_property.ProjectDir",out var projectDirectory)&&!string.IsNullOrWhiteSpace(projectDirectory)) {
                return projectDirectory;
            }
            return string.Empty;
        });

        var combined = context.CompilationProvider.Combine(projectDirectory).Combine(whereTargets.Collect()).Combine(typeTargets.Collect()).Combine(containerMethodTargets.Collect()).Combine(optimizeMethodTargets.Collect()).Combine(linqInvocationTargets.Collect());
        context.RegisterSourceOutput(combined,static (productionContext,pair) => Execute(
            productionContext,
            pair.Left.Left.Left.Left.Left.Left,
            pair.Left.Left.Left.Left.Left.Right,
            pair.Left.Left.Left.Left.Right,
            pair.Left.Left.Left.Right,
            pair.Left.Left.Right,
            pair.Left.Right,
            pair.Right));
    }

    // 役割ごとに generated source を分割して、interceptor / optimized helper / schema を読み分けやすくする。
    private static void Execute(SourceProductionContext context,Compilation compilation,string projectDirectory,ImmutableArray<WhereInterceptTarget> whereTargets,ImmutableArray<TypeTarget> typeTargets,ImmutableArray<ContainerMethodInterceptTarget> containerMethodTargets,ImmutableArray<OptimizeMethodTarget> optimizeMethodTargets,ImmutableArray<LinqInvocationInterceptTarget> linqInvocationTargets) {
        ReportOptimizeDiagnostics(context,typeTargets,optimizeMethodTargets);

        var optimizedCodeSource = BuildOptimizedCodeSource(whereTargets);
        if(optimizedCodeSource.Length>0) {
            context.AddSource("Seamless.SourceGenerator.OptimizedCode.g.cs",SourceText.From(RemoveBlankLines(optimizedCodeSource),Encoding.UTF8));
        }

        AddInterceptorSources(context,projectDirectory,whereTargets,containerMethodTargets,linqInvocationTargets);

        if(!typeTargets.IsDefaultOrEmpty) {
            var databaseSource = BuildDatabaseSource(typeTargets);
            if(databaseSource.Length>0) {
                context.AddSource("Seamless.SourceGenerator.Schema.g.cs",SourceText.From(RemoveBlankLines(databaseSource),Encoding.UTF8));
            }
        }

        var optimizedContainerSource = BuildOptimizedContainerSource(compilation,typeTargets);
        if(optimizedContainerSource.Length>0) {
            context.AddSource("Seamless.SourceGenerator.OptimizedContainers.g.cs",SourceText.From(RemoveBlankLines(optimizedContainerSource),Encoding.UTF8));
        }
    }

    private static void AddInterceptorSources(SourceProductionContext context,string projectDirectory,ImmutableArray<WhereInterceptTarget> whereTargets,ImmutableArray<ContainerMethodInterceptTarget> containerMethodTargets,ImmutableArray<LinqInvocationInterceptTarget> linqInvocationTargets) {
        if(whereTargets.IsDefaultOrEmpty&&containerMethodTargets.IsDefaultOrEmpty&&linqInvocationTargets.IsDefaultOrEmpty) {
            return;
        }

        var buckets = new Dictionary<string,InterceptorSourceBucket>(StringComparer.OrdinalIgnoreCase);
        foreach(var target in whereTargets) {
            GetInterceptorSourceBucket(buckets,target.FilePath).WhereTargets.Add(target);
        }
        foreach(var target in containerMethodTargets) {
            GetInterceptorSourceBucket(buckets,target.FilePath).ContainerMethodTargets.Add(target);
        }
        foreach(var target in linqInvocationTargets) {
            GetInterceptorSourceBucket(buckets,target.FilePath).LinqInvocationTargets.Add(target);
        }

        var usedHintNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var bucket in buckets.Values.OrderBy(static bucket => bucket.FilePath,StringComparer.OrdinalIgnoreCase)) {
            var interceptorSource = BuildInterceptorSource(
                bucket.WhereTargets.ToImmutableArray(),
                bucket.ContainerMethodTargets.ToImmutableArray(),
                bucket.LinqInvocationTargets.ToImmutableArray());
            if(interceptorSource.Length==0) {
                continue;
            }
            var hintName = EnsureUniqueHintName(BuildGeneratedSourceHintName(projectDirectory,bucket.FilePath),usedHintNames);
            context.AddSource(hintName,SourceText.From(RemoveBlankLines(interceptorSource),Encoding.UTF8));
        }
    }

    private static InterceptorSourceBucket GetInterceptorSourceBucket(Dictionary<string,InterceptorSourceBucket> buckets,string filePath) {
        var key = string.IsNullOrWhiteSpace(filePath) ? "Seamless.SourceGenerator.Interceptors" : filePath;
        if(!buckets.TryGetValue(key,out var bucket)) {
            bucket=new InterceptorSourceBucket(key);
            buckets.Add(key,bucket);
        }
        return bucket;
    }

    private static string BuildGeneratedSourceHintName(string projectDirectory,string filePath) {
        var relativePath = TryMakeRelativePath(projectDirectory,filePath)
            ??TryMakeSiblingSharedProjectRelativePath(projectDirectory,filePath)
            ??System.IO.Path.GetFileName(filePath);
        if(string.IsNullOrWhiteSpace(relativePath)) {
            relativePath="Seamless.SourceGenerator.Interceptors.cs";
        }

        relativePath=relativePath.Replace('\\','/').TrimStart('/');
        while(relativePath.StartsWith("../",StringComparison.Ordinal)) {
            relativePath=relativePath.Substring(3);
        }
        relativePath=relativePath.Replace("..","__").Replace(':','_').Replace('/','.').Trim('.');
        if(relativePath.EndsWith(".cs",StringComparison.OrdinalIgnoreCase)) {
            relativePath=relativePath.Substring(0,relativePath.Length-3)+".g.cs";
        }
        else if(!relativePath.EndsWith(".g.cs",StringComparison.OrdinalIgnoreCase)) {
            relativePath+=".g.cs";
        }
        return relativePath;
    }

    private static string EnsureUniqueHintName(string hintName,HashSet<string> usedHintNames) {
        if(usedHintNames.Add(hintName)) {
            return hintName;
        }

        var extension = ".g.cs";
        var stem = hintName.EndsWith(extension,StringComparison.OrdinalIgnoreCase)
            ? hintName.Substring(0,hintName.Length-extension.Length)
            : hintName;
        for(var index = 2; ; index++) {
            var candidate = stem+"_"+index+extension;
            if(usedHintNames.Add(candidate)) {
                return candidate;
            }
        }
    }

    private static string? TryMakeSiblingSharedProjectRelativePath(string projectDirectory,string filePath) {
        var normalizedProjectDirectory = NormalizeDirectoryPath(projectDirectory);
        if(string.IsNullOrWhiteSpace(normalizedProjectDirectory)) {
            return null;
        }

        var projectName = System.IO.Path.GetFileName(normalizedProjectDirectory.TrimEnd(System.IO.Path.DirectorySeparatorChar,System.IO.Path.AltDirectorySeparatorChar));
        if(string.IsNullOrWhiteSpace(projectName)) {
            return null;
        }
        var sharedNameLength = projectName.Length;
        while(sharedNameLength>0&&char.IsDigit(projectName[sharedNameLength-1])) {
            sharedNameLength--;
        }
        if(sharedNameLength==projectName.Length||sharedNameLength==0) {
            return null;
        }

        var parent = System.IO.Path.GetDirectoryName(normalizedProjectDirectory.TrimEnd(System.IO.Path.DirectorySeparatorChar,System.IO.Path.AltDirectorySeparatorChar));
        if(string.IsNullOrWhiteSpace(parent)) {
            return null;
        }
        var sharedDirectory = System.IO.Path.Combine(parent,projectName.Substring(0,sharedNameLength));
        return TryMakeRelativePath(sharedDirectory,filePath);
    }

    private static string? TryMakeRelativePath(string rootDirectory,string filePath) {
        var normalizedRoot = NormalizeDirectoryPath(rootDirectory);
        var normalizedFilePath = NormalizeFilePath(filePath);
        if(string.IsNullOrWhiteSpace(normalizedRoot)||string.IsNullOrWhiteSpace(normalizedFilePath)) {
            return null;
        }

        normalizedRoot=normalizedRoot.TrimEnd(System.IO.Path.DirectorySeparatorChar,System.IO.Path.AltDirectorySeparatorChar)+System.IO.Path.DirectorySeparatorChar;
        return normalizedFilePath.StartsWith(normalizedRoot,StringComparison.OrdinalIgnoreCase)
            ? normalizedFilePath.Substring(normalizedRoot.Length)
            : null;
    }

    private static string NormalizeDirectoryPath(string path) {
        if(string.IsNullOrWhiteSpace(path)) {
            return string.Empty;
        }
        try {
            return System.IO.Path.GetFullPath(path);
        }
        catch {
            return path;
        }
    }

    private static string NormalizeFilePath(string path) {
        if(string.IsNullOrWhiteSpace(path)) {
            return string.Empty;
        }
        try {
            return System.IO.Path.GetFullPath(path);
        }
        catch {
            return path;
        }
    }

    private static string BuildFilePathIdentifier(string filePath) {
        var normalized = NormalizeFilePath(filePath).Replace('\\','/');
        if(normalized.EndsWith(".cs",StringComparison.OrdinalIgnoreCase)) {
            normalized=normalized.Substring(0,normalized.Length-3);
        }
        var testsIndex = normalized.IndexOf("/tests/",StringComparison.OrdinalIgnoreCase);
        if(testsIndex>=0) {
            normalized=normalized.Substring(testsIndex+"/tests/".Length);
        }
        return SanitizeIdentifier(normalized);
    }

    private static string BuildOptimizeAttributeSource() => """
// <auto-generated />
#nullable enable
namespace Seamless {
    internal enum LinqInterceptMode {
        ContainerMethod,
        Invocation,
    }

    [global::System.AttributeUsage(global::System.AttributeTargets.Class|global::System.AttributeTargets.Method,AllowMultiple=false,Inherited=false)]
    internal sealed class LinqInterceptAttribute:global::System.Attribute {
        public LinqInterceptAttribute(LinqInterceptMode mode) {
            Mode=mode;
        }

        public LinqInterceptMode Mode { get; }
    }

    [global::System.AttributeUsage(global::System.AttributeTargets.Class|global::System.AttributeTargets.Method,AllowMultiple=false,Inherited=false)]
    internal sealed class OptimizeAttribute:global::System.Attribute {
    }

    [global::System.AttributeUsage(global::System.AttributeTargets.Class,AllowMultiple=true,Inherited=false)]
    internal sealed class DatabaseSchemaAttribute:global::System.Attribute {
        public DatabaseSchemaAttribute(string namespaceName,string definitions) {
            NamespaceName=namespaceName;
            Definitions=definitions;
        }

        public string NamespaceName { get; }
        public string Definitions { get; }
    }
}
""";

    // generated code は人が直接読むので、空行は最後にまとめて取り除く。
    // これで各所の AppendLine() の都合に引きずられず、出力全体の体裁を一定にできる。
    private static string RemoveBlankLines(string source) {
        var lines = source.Replace("\r\n","\n").Split('\n');
        var builder = new StringBuilder(source.Length);
        var firstWritten = true;
        foreach(var line in lines) {
            if(string.IsNullOrWhiteSpace(line)) {
                continue;
            }
            if(!firstWritten) {
                builder.Append('\n');
            }
            builder.Append(line);
            firstWritten=false;
        }
        return builder.ToString();
    }

    // Enumerable.Where 呼び出しのうち、List<T> 最適化に落とせるものだけを抽出する。
    private static WhereInterceptTarget? CreateWhereInterceptTarget(GeneratorSyntaxContext context,System.Threading.CancellationToken cancellationToken) {
        if(context.Node is not InvocationExpressionSyntax invocation) {
            return null;
        }
        if(invocation.Expression is not MemberAccessExpressionSyntax memberAccess) {
            return null;
        }
        var interceptableLocation = Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetInterceptableLocation(context.SemanticModel,invocation,cancellationToken);
        if(interceptableLocation is null) {
            return null;
        }
        if(context.SemanticModel.GetSymbolInfo(invocation,cancellationToken).Symbol is not IMethodSymbol method) {
            return null;
        }
        if(method.Name!="Where"||!method.IsExtensionMethod) {
            return null;
        }
        var reducedMethod = method.ReducedFrom??method;
        if(reducedMethod.ContainingType.ToDisplayString()!="System.Linq.Enumerable") {
            return null;
        }
        if(method.ReducedFrom is not null) {
            return null;
        }
        if(!TryGetWherePredicateInfo(reducedMethod,out var usesIndex)) {
            return null;
        }
        if(!TryGetWhereCallInfo(invocation,memberAccess,method,out var sourceExpression,out var predicateExpression)) {
            return null;
        }
        if(!TryGetListElementType(context.SemanticModel.GetTypeInfo(sourceExpression,cancellationToken).Type,out var elementType)) {
            return null;
        }
        if(!TryGetPredicateInfo(context.SemanticModel,predicateExpression,cancellationToken,usesIndex,out var elementParameterName,out var indexParameterName,out var predicateText,out var usePredicateDelegate)) {
            return null;
        }
        var span = memberAccess.Name.GetLocation().GetLineSpan();
        var line = span.StartLinePosition.Line+1;
        var character = span.StartLinePosition.Character+1;
        var id = SanitizeIdentifier($"{BuildFilePathIdentifier(span.Path)}_{line}_{character}");
        var interceptsLocationAttributeText = Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetInterceptsLocationAttributeSyntax(interceptableLocation);
        if(string.IsNullOrWhiteSpace(interceptsLocationAttributeText)) {
            return null;
        }
        if(!TryBuildContainingMethodCref(invocation,context.SemanticModel,cancellationToken,out var containingMethodCref)) {
            return null;
        }

        return new WhereInterceptTarget(
            id,
            span.Path,
            line,
            character,
            containingMethodCref,
            interceptsLocationAttributeText,
            elementType.ToDisplayString(FullyQualifiedFormat),
            usesIndex,
            elementParameterName,
            indexParameterName,
            predicateText,
            usePredicateDelegate);
    }

    // 非 virtual container method 呼び出しを optimized static interceptor へ差し替える。
    // virtual method は仮想 dispatch を保つため、BuildOptimizedContainerSource 側で Optimized{Container} override として生成する。
    private static ContainerMethodInterceptTarget? CreateContainerMethodInterceptTarget(GeneratorSyntaxContext context,System.Threading.CancellationToken cancellationToken) {
        if(context.Node is not InvocationExpressionSyntax invocation) {
            return null;
        }
        var interceptableLocation = Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetInterceptableLocation(context.SemanticModel,invocation,cancellationToken);
        if(interceptableLocation is null) {
            return null;
        }
        if(context.SemanticModel.GetSymbolInfo(invocation,cancellationToken).Symbol is not IMethodSymbol method) {
            return null;
        }
        var methodDefinition = method.OriginalDefinition;
        if(!IsContainerMethodInterceptCandidate(methodDefinition)) {
            return null;
        }
        var translatedBody = メソッド翻訳試行(context.SemanticModel.Compilation,methodDefinition);
        if(translatedBody is null) {
            return null;
        }

        var methodLocation = GetInvocationMethodLocation(invocation);
        var span = methodLocation.GetLineSpan();
        var line = span.StartLinePosition.Line+1;
        var character = span.StartLinePosition.Character+1;
        var id = SanitizeIdentifier($"{BuildFilePathIdentifier(span.Path)}_{method.Name}_{line}_{character}");
        var interceptsLocationAttributeText = Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetInterceptsLocationAttributeSyntax(interceptableLocation);
        if(string.IsNullOrWhiteSpace(interceptsLocationAttributeText)) {
            return null;
        }

        if(!TryBuildContainingMethodCref(invocation,context.SemanticModel,cancellationToken,out var containingMethodCref)) {
            return null;
        }

        return new ContainerMethodInterceptTarget(
            id,
            span.Path,
            line,
            character,
            containingMethodCref,
            interceptsLocationAttributeText,
            methodDefinition.ContainingType.ToDisplayString(FullyQualifiedFormat),
            BuildGeneratedTypeName(methodDefinition.ReturnType),
            BuildTypeParameterList(methodDefinition.TypeParameters),
            BuildTypeParameterConstraintList(methodDefinition.TypeParameters),
            BuildParameterDeclarationList(methodDefinition.Parameters),
            RewriteInstanceReceiverReferences(translatedBody.Value.Body),
            translatedBody.Value.IsExpressionBody);
    }

    private static LinqInvocationInterceptTarget? CreateLinqInvocationInterceptTarget(GeneratorSyntaxContext context,System.Threading.CancellationToken cancellationToken) {
        if(context.Node is not InvocationExpressionSyntax invocation) {
            return null;
        }
        if(invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>() is not { } containingMethodSyntax) {
            return null;
        }
        var containingMethod = context.SemanticModel.GetDeclaredSymbol(containingMethodSyntax,cancellationToken);
        if(containingMethod is null) {
            return null;
        }
        var interceptableLocation = Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetInterceptableLocation(context.SemanticModel,invocation,cancellationToken);
        if(interceptableLocation is null) {
            return null;
        }
        if(context.SemanticModel.GetSymbolInfo(invocation,cancellationToken).Symbol is not IMethodSymbol method) {
            return null;
        }
        if(!IsSupportedLinqMethod(method)) {
            return null;
        }
        if(ContainsAnonymousType(method.ReturnType)
            ||method.TypeArguments.Any(ContainsAnonymousType)
            ||method.Parameters.Any(static parameter => ContainsAnonymousType(parameter.Type))) {
            return null;
        }
        if(ContainsErrorType(method.ReturnType)
            ||method.TypeArguments.Any(ContainsErrorType)
            ||method.Parameters.Any(static parameter => ContainsErrorType(parameter.Type))
            ||ContainsErrorTypeInExpression(invocation,context.SemanticModel,cancellationToken)) {
            return null;
        }
        if(IsListWhereInterceptorCandidate(invocation,method,context.SemanticModel,cancellationToken)) {
            return null;
        }
        var hasLinqInvocationAncestor = HasLinqInvocationAncestor(invocation,context.SemanticModel,cancellationToken);
        if(hasLinqInvocationAncestor&&!CanDeferLinqInvocationForFusion(method)) {
            return null;
        }
        var translatedBody = LINQ呼び出しボディ翻訳試行(method,hasLinqInvocationAncestor);
        if(translatedBody is null) {
            return null;
        }
        var definition = GetLinqInvocationDefinition(method);
        var methodLocation = GetInvocationMethodLocation(invocation);
        var span = methodLocation.GetLineSpan();
        var line = span.StartLinePosition.Line+1;
        var character = span.StartLinePosition.Character+1;
        var id = SanitizeIdentifier($"{BuildFilePathIdentifier(span.Path)}_{containingMethod.Name}_{method.Name}_{line}_{character}");
        var interceptsLocationAttributeText = Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetInterceptsLocationAttributeSyntax(interceptableLocation);
        if(string.IsNullOrWhiteSpace(interceptsLocationAttributeText)) {
            return null;
        }
        var staticLambdaStrategy = TryBuildStaticLinqLambdaStrategy(invocation,method,id,out var strategy)
            &&CanUseConcreteLinqInvocationInterceptor(method)
            ? strategy
            : null;
        var useConcreteSignature = staticLambdaStrategy is not null;
        if(!(useConcreteSignature
                ? TryBuildConcreteLinqInvocationParameterList(invocation,method,out var parameterList)
                : TryBuildLinqInvocationParameterList(invocation,method,context.SemanticModel,out parameterList))) {
            return null;
        }

        var body = useConcreteSignature
            ? LINQ呼び出し型引数具体化(translatedBody.Value.Body,method)
            : translatedBody.Value.Body;
        var decoratedBody = LINQ呼び出しデバッグ式埋込(body,invocation,method);
        if(staticLambdaStrategy is not null) {
            decoratedBody=LINQ呼び出し静的ラムダ適用(decoratedBody,staticLambdaStrategy);
        }

        return new LinqInvocationInterceptTarget(
            id,
            span.Path,
            BuildMethodCref(containingMethod),
            interceptsLocationAttributeText,
            useConcreteSignature ? BuildGeneratedTypeName(method.ReturnType) : BuildGeneratedTypeName(definition.ReturnType),
            useConcreteSignature ? string.Empty : BuildTypeParameterList(definition.TypeParameters),
            useConcreteSignature ? string.Empty : BuildTypeParameterConstraintList(definition.TypeParameters),
            parameterList,
            decoratedBody,
            translatedBody.Value.IsExpressionBody,
            staticLambdaStrategy?.Source);
    }

    /// <summary>
    /// LINQ メソッドのインターセプト本体を生成する入口。<br/>
    /// 融合 → インライン展開 → コールスルーの優先順で変換方針を決定する。
    /// </summary>
    /// <example><code>
    /// // 変換前 (ユーザーコード):
    /// source.Select(x => x * 2)
    ///
    /// // 融合が成立する場合   → LINQ呼び出し融合ボディ翻訳試行 が生成するブロック本体
    /// // インライン展開が成立 → LINQ呼び出しインラインボディ翻訳試行 が生成する展開済み本体
    /// // どちらも不可の場合  → Seamless.Linq.Enumerable.Select(source, selector) のコールスルー式
    /// </code></example>
    private static TranslatedMethodBody? LINQ呼び出しボディ翻訳試行(IMethodSymbol method,bool hasLinqInvocationAncestor) {
        var fusionBody = LINQ呼び出し融合ボディ翻訳試行(method,hasLinqInvocationAncestor);
        if(fusionBody is not null) {
            return fusionBody;
        }

        var inlineBody = LINQ呼び出しインラインボディ翻訳試行(method);
        if(inlineBody is not null) {
            return new TranslatedMethodBody(inlineBody,false);
        }

        var callThroughExpression = LINQ呼び出しコールスルー式翻訳試行(method);
        return callThroughExpression is null
            ? null
            : new TranslatedMethodBody(callThroughExpression,true);
    }

    private static bool CanDeferLinqInvocationForFusion(IMethodSymbol method) {
        return 遅延LINQ融合式翻訳試行(method) is not null;
    }

    /// <summary>
    /// LINQ メソッドが上位の LINQ 呼び出し内に入れ子の場合は遅延式を返し、<br/>
    /// そうでなければ選択融合またはパススルー融合のブロック本体を返す。
    /// </summary>
    /// <example><code>
    /// // 変換前 (内側の Select):
    /// source.Where(x => x > 0).Select(x => x * 2)
    ///
    /// // hasLinqInvocationAncestor == true (入れ子) の場合:
    /// //   → 遅延LINQ融合式翻訳試行 の結果:
    /// //     global::...LinqInvocationFusionSupport.Create(source).選択追加(selector)
    ///
    /// // hasLinqInvocationAncestor == false (最外側) の場合:
    /// //   → LINQ呼び出し選択融合ボディ翻訳試行 の結果:
    /// //     { if (source is I融合ソース&lt;T&gt; 既存融合ソース) return 既存融合ソース.選択実体化(selector); ... }
    /// </code></example>
    private static TranslatedMethodBody? LINQ呼び出し融合ボディ翻訳試行(IMethodSymbol method,bool hasLinqInvocationAncestor) {
        var deferredExpression = 遅延LINQ融合式翻訳試行(method);
        if(hasLinqInvocationAncestor) {
            return deferredExpression is null ? null : new TranslatedMethodBody(deferredExpression,true);
        }

        var selectBody = LINQ呼び出し選択融合ボディ翻訳試行(method);
        if(selectBody is not null) {
            return new TranslatedMethodBody(selectBody,false);
        }

        var passThroughBody = LINQ呼び出しパススルー融合ボディ翻訳試行(method);
        if(passThroughBody is not null) {
            return new TranslatedMethodBody(passThroughBody,false);
        }

        return null;
    }

    /// <summary>
    /// 入れ子の Select / Where / Take を融合ソースへの操作追加式に変換する。<br/>
    /// 実体化は外側の実体化境界で行うため、ここでは列挙しない。
    /// </summary>
    /// <example><code>
    /// // 変換前:
    /// source.Select(x => x * 2)
    /// // 変換後:
    /// global::Seamless.SourceGenerator.Generated.LinqInvocationFusionSupport.Create(source).選択追加(selector)
    ///
    /// // 変換前:
    /// source.Where(x => x > 0)
    /// // 変換後:
    /// global::Seamless.SourceGenerator.Generated.LinqInvocationFusionSupport.Create(source).絞込追加(predicate)
    ///
    /// // 変換前:
    /// source.Take(5)
    /// // 変換後:
    /// global::Seamless.SourceGenerator.Generated.LinqInvocationFusionSupport.Create(source).先頭取得追加(count)
    /// </code></example>
    private static string? 遅延LINQ融合式翻訳試行(IMethodSymbol method) {
        var definition = GetLinqInvocationDefinition(method);
        if(TryGetLinqInvocationTargetType(method) is null) {
            return null;
        }

        // 入れ子 LINQ の内側 Select/Where/Take はここで列挙せず、融合ソースに操作だけを積む。
        // 外側の実体化側が I融合ソースを検出して、元の source を 1 回だけ列挙する。
        // static で生成先へ安全に移動できる lambda は、後段で生成 strategy struct に差し替えて delegate 呼び出しを避ける。
        // クロージャや生成先から見えない型・メンバーを含む lambda は、従来どおり Func 引数の delegate 経路にフォールバックする。
        if(IsNonIndexedSelectMethod(definition)) {
            // 内側の Select: 融合ソースに選択操作を積んで返す (実体化は外側の実体化境界で行う)。
            return "global::Seamless.SourceGenerator.Generated.LinqInvocationFusionSupport.Create(source).選択追加(selector)";
        }
        if(IsNonIndexedWhereMethod(definition)) {
            // 内側の Where: 融合ソースに絞込操作を積んで返す。
            return "global::Seamless.SourceGenerator.Generated.LinqInvocationFusionSupport.Create(source).絞込追加(predicate)";
        }
        if(IsCountTakeMethod(definition)) {
            // 内側の Take: 融合ソースに先頭取得操作を積んで返す。
            return "global::Seamless.SourceGenerator.Generated.LinqInvocationFusionSupport.Create(source).先頭取得追加(count)";
        }
        return null;
    }

    /// <summary>
    /// Select が最外側の実体化境界になる場合のブロック本体を生成する。<br/>
    /// source が <c>I融合ソース&lt;T&gt;</c> であれば蓄積済みパイプラインを通じて実体化し、
    /// そうでなければ通常の <c>Seamless.Linq.Enumerable.Select</c> にフォールバックする。
    /// </summary>
    /// <example><code>
    /// // 変換前:
    /// source.Where(x => x > 0).Select(x => x * 2)
    ///
    /// // 変換後 (生成される interceptor 本体):
    /// {
    ///     global::System.ArgumentNullException.ThrowIfNull(source);
    ///     global::System.ArgumentNullException.ThrowIfNull(selector);
    ///     if (source is global::...I融合ソース&lt;TSource&gt; 既存融合ソース)
    ///         return 既存融合ソース.選択実体化(selector);
    ///     return global::Seamless.Linq.Enumerable.Select&lt;TSource, TResult&gt;(source, selector);
    /// }
    /// </code></example>
    private static string? LINQ呼び出し選択融合ボディ翻訳試行(IMethodSymbol method) {
        var definition = GetLinqInvocationDefinition(method);
        if(!IsNonIndexedSelectMethod(definition)) {
            return null;
        }
        var targetType = TryGetLinqInvocationTargetType(method);
        if(targetType is null) {
            return null;
        }
        return $$"""
{
    global::System.ArgumentNullException.ThrowIfNull(source);
    global::System.ArgumentNullException.ThrowIfNull(selector);
    // 内側の Select/Where/Take が融合ソースを返していた場合、ここが実体化境界になる。
    // 蓄積済みプロジェクタを通して元ソースを 1 回だけ列挙し、最後の Select を適用して List にする。
    // 最後の selector も static で生成先へ安全に移動できる場合は、後段で生成 strategy struct に差し替える。
    // source が I融合ソース<TSource> であれば蓄積済み pipeline を通じて実体化する (ソースの二重列挙を回避)。
    if(source is global::Seamless.SourceGenerator.Generated.LinqInvocationFusionSupport.I融合ソース<TSource> 既存融合ソース) {
        return 既存融合ソース.選択実体化(selector);
    }
    return {{targetType}}.Select<TSource, TResult>(source,selector);
}
""";
    }

    /// <summary>
    /// Where / Take が最外側で呼ばれた場合のブロック本体を生成する。<br/>
    /// source が <c>I融合ソース&lt;T&gt;</c> なら操作を積んで融合ソースを返す (実体化を後回し)。
    /// そうでなければ <c>Seamless.Linq.Enumerable</c> の同名メソッドにフォールバックする。
    /// </summary>
    /// <example><code>
    /// // 変換前:
    /// source.Where(x => x > 0)
    ///
    /// // 変換後 (Where の interceptor 本体):
    /// {
    ///     global::System.ArgumentNullException.ThrowIfNull(source);
    ///     global::System.ArgumentNullException.ThrowIfNull(predicate);
    ///     if (source is global::...I融合ソース&lt;TSource&gt; 既存融合ソース)
    ///         return 既存融合ソース.絞込追加(predicate);
    ///     return global::Seamless.Linq.Enumerable.Where&lt;TSource&gt;(source, predicate);
    /// }
    /// </code></example>
    private static string? LINQ呼び出しパススルー融合ボディ翻訳試行(IMethodSymbol method) {
        var definition = GetLinqInvocationDefinition(method);
        var targetType = TryGetLinqInvocationTargetType(method);
        if(targetType is null) {
            return null;
        }

        if(IsNonIndexedWhereMethod(definition)) {
            return $$"""
{
    global::System.ArgumentNullException.ThrowIfNull(source);
    global::System.ArgumentNullException.ThrowIfNull(predicate);
    // source が I融合ソース<TSource> であれば Where 操作を融合ソースの pipeline に積んで返す (実体化は後回し)。
    if(source is global::Seamless.SourceGenerator.Generated.LinqInvocationFusionSupport.I融合ソース<TSource> 既存融合ソース) {
        return 既存融合ソース.絞込追加(predicate);
    }
    return {{targetType}}.Where<TSource>(source,predicate);
}
""";
        }

        if(IsCountTakeMethod(definition)) {
            return $$"""
{
    global::System.ArgumentNullException.ThrowIfNull(source);
    // source が I融合ソース<TSource> であれば Take 操作を融合ソースの pipeline に積んで返す (実体化は後回し)。
    if(source is global::Seamless.SourceGenerator.Generated.LinqInvocationFusionSupport.I融合ソース<TSource> 既存融合ソース) {
        return 既存融合ソース.先頭取得追加(count);
    }
    return {{targetType}}.Take<TSource>(source,count);
}
""";
        }

        return null;
    }

    private static bool IsNonIndexedSelectMethod(IMethodSymbol definition) {
        return definition.Name=="Select"
            &&definition.TypeParameters.Length==2
            &&definition.Parameters.Length==2
            &&IsFuncType(definition.Parameters[1].Type,2);
    }

    private static bool IsNonIndexedWhereMethod(IMethodSymbol definition) {
        return definition.Name=="Where"
            &&definition.TypeParameters.Length==1
            &&definition.Parameters.Length==2
            &&IsFuncType(definition.Parameters[1].Type,2);
    }

    private static bool IsCountTakeMethod(IMethodSymbol definition) {
        return definition.Name=="Take"
            &&definition.TypeParameters.Length==1
            &&definition.Parameters.Length==2
            &&definition.Parameters[1].Type.SpecialType==SpecialType.System_Int32;
    }

    private static bool IsFuncType(ITypeSymbol type,int typeArgumentCount) {
        return type is INamedTypeSymbol namedType
            &&namedType.Name=="Func"
            &&namedType.ContainingNamespace.ToDisplayString()=="System"
            &&namedType.TypeArguments.Length==typeArgumentCount;
    }

    /// <summary>
    /// Aggregate / Select / ToList 等の埋め込み実装ソース (<c>Seamless.Linq.Enumerable.cs</c>) を読み込み、<br/>
    /// 該当メソッドの実装をそのまま interceptor 本体としてインライン展開する。
    /// </summary>
    /// <example><code>
    /// // 変換前:
    /// source.ToList()
    ///
    /// // 変換後 (Seamless.Linq.Enumerable.cs の ToList 実装を展開):
    /// {
    ///     global::System.ArgumentNullException.ThrowIfNull(source);
    ///     var result = new global::System.Collections.Generic.List&lt;TSource&gt;();
    ///     foreach (var item in source) result.Add(item);
    ///     return result;
    /// }
    /// </code></example>
    private static string? LINQ呼び出しインラインボディ翻訳試行(IMethodSymbol method) {
        var definition = GetLinqInvocationDefinition(method);
        if(!CanInlineLinqInvocationDefinition(definition)) {
            return null;
        }
        var implementation = TryGetLinqImplementationSource(definition);
        if(implementation is null) {
            return null;
        }

        var implementationMethod = FindImplementationMethod(implementation,definition);
        if(implementationMethod?.Body is null) {
            return null;
        }

        var targetTypeName = TryGetLinqInvocationTargetType(method);
        if(targetTypeName is null) {
            return null;
        }

        var body = TryBuildInlineImplementationBody(implementation,implementationMethod,targetTypeName);
        return body is null ? null : FormatEmbeddedImplementationBody(body);
    }

    private static bool CanInlineLinqInvocationDefinition(IMethodSymbol definition) {
        return definition.Name=="Aggregate"
            ||definition.Name=="Select"
            ||definition.Name=="Shuffle"
            ||definition.Name=="Take"
            ||definition.Name=="ToArray"
            ||definition.Name=="ToList"
            ||definition.Name=="Where";
    }

    private static LinqImplementationSource? TryGetLinqImplementationSource(IMethodSymbol definition) {
        var containingType = definition.ContainingType?.ToDisplayString();
        return containingType switch {
            "System.Linq.Enumerable" => EnumerableImplementation.Value,
            "Seamless.Linq.Enumerable" => EnumerableImplementation.Value,
            "Seamless.Linq.HashSet" => HashSetImplementation.Value,
            _ => null,
        };
    }

    private static MethodDeclarationSyntax? FindImplementationMethod(LinqImplementationSource implementation,IMethodSymbol definition) {
        var candidates = implementation.Methods
            .Where(candidate => candidate.Identifier.ValueText==definition.Name
                &&(candidate.TypeParameterList?.Parameters.Count??0)==definition.TypeParameters.Length
                &&candidate.ParameterList.Parameters.Count==definition.Parameters.Length)
            .ToArray();
        if(candidates.Length==0) {
            return null;
        }

        var exactCandidates = candidates.Where(candidate => ParameterTypesMatch(candidate,definition)).ToArray();
        if(exactCandidates.Length==1) {
            return exactCandidates[0];
        }
        return candidates.Length==1 ? candidates[0] : null;
    }

    private static bool ParameterTypesMatch(MethodDeclarationSyntax candidate,IMethodSymbol definition) {
        for(var index = 0; index<definition.Parameters.Length; index++) {
            var candidateType = candidate.ParameterList.Parameters[index].Type?.ToString()??string.Empty;
            var symbolType = definition.Parameters[index].Type.ToDisplayString(FullyQualifiedFormat);
            if(NormalizeImplementationTypeText(candidateType)!=NormalizeImplementationTypeText(symbolType)) {
                return false;
            }
        }
        return true;
    }

    private static string NormalizeImplementationTypeText(string typeText) {
        return typeText
            .Replace("global::",string.Empty)
            .Replace("System.Collections.Generic.",string.Empty)
            .Replace("System.",string.Empty)
            .Replace("Collections.",string.Empty)
            .Replace("Seamless.Collections.",string.Empty)
            .Replace(" ",string.Empty);
    }

    private static BlockSyntax? TryBuildInlineImplementationBody(LinqImplementationSource implementation,MethodDeclarationSyntax method,string targetTypeName) {
        if(method.Body is null) {
            return null;
        }

        var body = method.Identifier.ValueText=="ToArray"&&method.ParameterList.Parameters.Count==1
            ? TryBuildToArrayInlineBody(implementation,targetTypeName)
            : TryInlineSingleReturnHelper(implementation,method,targetTypeName)??method.Body;
        var rewriter = new LinqImplementationBodyRewriter(
            new Dictionary<string,string>(StringComparer.Ordinal),
            implementation.NoElementsExpression,
            implementation,
            targetTypeName);
        return rewriter.Visit(body) as BlockSyntax;
    }

    private static BlockSyntax? TryBuildToArrayInlineBody(LinqImplementationSource implementation,string targetTypeName) {
        var toList = implementation.Methods.FirstOrDefault(static candidate =>
            candidate.Identifier.ValueText=="ToList"
            &&(candidate.TypeParameterList?.Parameters.Count??0)==1
            &&candidate.ParameterList.Parameters.Count==1);
        if(toList?.Body is null) {
            return null;
        }

        var toListBody = TryInlineSingleReturnHelper(implementation,toList,targetTypeName)??toList.Body;
        return new ToArrayReturnRewriter().Visit(toListBody) as BlockSyntax;
    }

    private static BlockSyntax? TryInlineSingleReturnHelper(LinqImplementationSource implementation,MethodDeclarationSyntax method,string targetTypeName) {
        if(method.Body is not { } methodBody||methodBody.Statements.Count==0) {
            return null;
        }
        if(methodBody.Statements[methodBody.Statements.Count-1] is not ReturnStatementSyntax { Expression: InvocationExpressionSyntax invocation }) {
            return null;
        }
        if(!TryGetInvocationSimpleName(invocation,out var helperName)) {
            return null;
        }
        if(helperName==method.Identifier.ValueText) {
            return null;
        }

        var helperCandidates = implementation.Methods
            .Where(candidate => candidate.Identifier.ValueText==helperName
                &&candidate.ParameterList.Parameters.Count==invocation.ArgumentList.Arguments.Count)
            .ToArray();
        var helper = helperCandidates.FirstOrDefault(candidate => HelperParameterNamesMatch(candidate,invocation))
            ??(helperCandidates.Length==1 ? helperCandidates[0] : null);
        if(helper?.Body is null) {
            return null;
        }

        var replacements = new Dictionary<string,string>(StringComparer.Ordinal);
        for(var index = 0; index<helper.ParameterList.Parameters.Count; index++) {
            replacements[helper.ParameterList.Parameters[index].Identifier.ValueText]=NormalizeExpression(invocation.ArgumentList.Arguments[index].Expression);
        }

        var rewriter = new LinqImplementationBodyRewriter(replacements,implementation.NoElementsExpression,implementation,targetTypeName);
        if(rewriter.Visit(helper.Body) is not BlockSyntax rewrittenHelperBody) {
            return null;
        }

        var statements = new List<StatementSyntax>();
        statements.AddRange(methodBody.Statements.Take(methodBody.Statements.Count-1));
        statements.AddRange(rewrittenHelperBody.Statements);
        return SyntaxFactory.Block(statements);
    }

    private static bool HelperParameterNamesMatch(MethodDeclarationSyntax helper,InvocationExpressionSyntax invocation) {
        for(var index = 0; index<helper.ParameterList.Parameters.Count; index++) {
            if(invocation.ArgumentList.Arguments[index].Expression is not IdentifierNameSyntax identifier) {
                continue;
            }
            if(helper.ParameterList.Parameters[index].Identifier.ValueText!=identifier.Identifier.ValueText) {
                return false;
            }
        }
        return true;
    }

    private static bool TryGetInvocationSimpleName(InvocationExpressionSyntax invocation,out string name) {
        name=string.Empty;
        var expression = invocation.Expression;
        if(expression is IdentifierNameSyntax identifier) {
            name=identifier.Identifier.ValueText;
            return !string.IsNullOrWhiteSpace(name);
        }
        if(expression is GenericNameSyntax genericName) {
            name=genericName.Identifier.ValueText;
            return !string.IsNullOrWhiteSpace(name);
        }
        return false;
    }

    private static string FormatEmbeddedImplementationBody(BlockSyntax body) {
        var lines = body.ToFullString().Replace("\r\n","\n").Split('\n').ToList();
        while(lines.Count>0&&string.IsNullOrWhiteSpace(lines[0])) {
            lines.RemoveAt(0);
        }
        while(lines.Count>0&&string.IsNullOrWhiteSpace(lines[lines.Count-1])) {
            lines.RemoveAt(lines.Count-1);
        }
        if(lines.Count==0) {
            return "{}";
        }

        var innerLines = lines.Skip(1).Take(Math.Max(0,lines.Count-2)).ToArray();
        var commonIndent = innerLines
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .Select(CountLeadingWhitespace)
            .Where(static indent => indent>0)
            .DefaultIfEmpty(0)
            .Min();
        var formatted = new List<string>(lines.Count) { lines[0].Trim() };
        foreach(var line in innerLines) {
            if(string.IsNullOrWhiteSpace(line)) {
                formatted.Add(string.Empty);
                continue;
            }
            var unindented = line.Length>=commonIndent ? line.Substring(commonIndent) : line.TrimStart();
            formatted.Add("    "+unindented.TrimEnd());
        }
        if(lines.Count>1) {
            formatted.Add(lines[lines.Count-1].Trim());
        }
        return string.Join("\n",formatted);
    }

    private static int CountLeadingWhitespace(string line) {
        var count = 0;
        while(count<line.Length&&char.IsWhiteSpace(line[count])) {
            count++;
        }
        return count;
    }

    private static LinqImplementationSource? LoadLinqImplementationSource(string resourceName) {
        var assembly = typeof(SeamlessIncrementalGenerator).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if(stream is null) {
            return null;
        }
        using var reader = new System.IO.StreamReader(stream,Encoding.UTF8,true);
        var source = reader.ReadToEnd();
        var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().ToArray();
        var noElementsExpression = methods
            .Where(static candidate => candidate.Identifier.ValueText=="NoElements"&&candidate.ParameterList.Parameters.Count==0)
            .Select(static candidate => TryGetSingleReturnExpression(candidate))
            .FirstOrDefault(static expression => expression is not null)
            ??SyntaxFactory.ParseExpression("new global::System.InvalidOperationException(\"Sequence contains no elements.\")");
        return new LinqImplementationSource(methods,noElementsExpression);
    }

    private static ExpressionSyntax? TryGetSingleReturnExpression(MethodDeclarationSyntax method) {
        if(method.Body?.Statements.Count!=1) {
            return null;
        }
        if(method.Body.Statements[0] is not ReturnStatementSyntax returnStatement) {
            return null;
        }
        return returnStatement.Expression;
    }

    /// <summary>
    /// 融合もインライン展開もできない LINQ メソッドに対し、<br/>
    /// <c>System.Linq.Enumerable</c> 呼び出しを <c>Seamless.Linq.Enumerable</c> 呼び出しへ差し替える式を返す。
    /// </summary>
    /// <example><code>
    /// // 変換前:
    /// source.OrderBy(x => x.Key)
    ///
    /// // 変換後 (コールスルー式):
    /// global::Seamless.Linq.Enumerable.OrderBy&lt;TSource, TKey&gt;(source, x => x.Key)
    /// </code></example>
    private static string? LINQ呼び出しコールスルー式翻訳試行(IMethodSymbol method) {
        var targetType = TryGetLinqInvocationTargetType(method);
        if(targetType is null) {
            return null;
        }

        var definition = GetLinqInvocationDefinition(method);
        var arguments = BuildLinqInvocationCallArguments(method);
        return $"{targetType}.{definition.Name}{BuildMethodTypeArgumentList(method)}({string.Join(",",arguments)})";
    }

    private static string? TryGetLinqInvocationTargetType(IMethodSymbol method) {
        var definition = GetLinqInvocationDefinition(method);
        var containingType = definition.ContainingType.ToDisplayString();
        return containingType switch {
            "System.Linq.Enumerable" => "global::Seamless.Linq.Enumerable",
            "Seamless.Linq.Enumerable" => "global::Seamless.Linq.Enumerable",
            "Seamless.Linq.HashSet" => "global::Seamless.Linq.HashSet",
            _ => null
        };
    }

    private static string BuildMethodTypeArgumentList(IMethodSymbol method) {
        var definition = GetLinqInvocationDefinition(method);
        if(definition.TypeParameters.Length==0) {
            return string.Empty;
        }
        return $"<{string.Join(", ",definition.TypeParameters.Select(static typeParameter => typeParameter.Name))}>";
    }

    private static string[] BuildLinqInvocationCallArguments(IMethodSymbol method) {
        var definition = GetLinqInvocationDefinition(method);
        return definition.Parameters.Select(BuildArgumentExpression).ToArray();
    }

    private static IMethodSymbol GetLinqInvocationDefinition(IMethodSymbol method) {
        return method.ReducedFrom??method.OriginalDefinition;
    }

    private static bool HasLinqInvocationAncestor(InvocationExpressionSyntax invocation,SemanticModel semanticModel,System.Threading.CancellationToken cancellationToken) {
        for(var current = invocation.Parent; current is not null; current=current.Parent) {
            if(current is StatementSyntax or LambdaExpressionSyntax) {
                return false;
            }
            if(current is InvocationExpressionSyntax parentInvocation) {
                return semanticModel.GetSymbolInfo(parentInvocation,cancellationToken).Symbol is IMethodSymbol parentMethod&&IsSupportedLinqMethod(parentMethod);
            }
        }
        return false;
    }

    private static bool TryBuildLinqInvocationParameterList(InvocationExpressionSyntax invocation,IMethodSymbol method,SemanticModel semanticModel,out string parameterList) {
        var declarations = new List<string>();
        var definition = GetLinqInvocationDefinition(method);
        if(method.ReducedFrom is not null) {
            if(invocation.Expression is not MemberAccessExpressionSyntax memberAccess) {
                parameterList=string.Empty;
                return false;
            }
            _=semanticModel.GetTypeInfo(memberAccess.Expression);
            declarations.Add($"this {BuildGeneratedTypeName(definition.Parameters[0].Type)} {definition.Parameters[0].Name}");
        }
        var parameterStart = method.ReducedFrom is null ? 0 : 1;
        foreach(var parameter in definition.Parameters.Skip(parameterStart)) {
            declarations.Add(BuildConcreteParameterDeclaration(parameter));
        }
        parameterList=string.Join(", ",declarations);
        return true;
    }

    private static bool TryBuildConcreteLinqInvocationParameterList(InvocationExpressionSyntax invocation,IMethodSymbol method,out string parameterList) {
        var declarations = new List<string>();
        var definition = GetLinqInvocationDefinition(method);
        if(method.ReducedFrom is not null) {
            if(invocation.Expression is not MemberAccessExpressionSyntax||method.ReceiverType is null) {
                parameterList=string.Empty;
                return false;
            }
            declarations.Add($"this {BuildGeneratedTypeName(method.ReceiverType)} {definition.Parameters[0].Name}");
        }
        foreach(var parameter in method.Parameters) {
            declarations.Add(BuildConcreteParameterDeclaration(parameter));
        }
        parameterList=string.Join(", ",declarations);
        return true;
    }

    private static bool CanUseConcreteLinqInvocationInterceptor(IMethodSymbol method) {
        if(ContainsTypeParameter(method.ReturnType)||!CanReferenceTypeFromGeneratedSource(method.ReturnType)) {
            return false;
        }
        if(method.ReducedFrom is not null&&(method.ReceiverType is null||ContainsTypeParameter(method.ReceiverType)||!CanReferenceTypeFromGeneratedSource(method.ReceiverType))) {
            return false;
        }
        foreach(var parameter in method.Parameters) {
            if(ContainsTypeParameter(parameter.Type)||!CanReferenceTypeFromGeneratedSource(parameter.Type)) {
                return false;
            }
        }
        return true;
    }

    private static bool CanReferenceTypeFromGeneratedSource(ITypeSymbol type) {
        if(type is IArrayTypeSymbol arrayType) {
            return CanReferenceTypeFromGeneratedSource(arrayType.ElementType);
        }
        if(type is IPointerTypeSymbol pointerType) {
            return CanReferenceTypeFromGeneratedSource(pointerType.PointedAtType);
        }
        if(type is INamedTypeSymbol namedType) {
            if(!CanReferenceNamedTypeFromGeneratedSource(namedType)) {
                return false;
            }
            foreach(var typeArgument in namedType.TypeArguments) {
                if(!CanReferenceTypeFromGeneratedSource(typeArgument)) {
                    return false;
                }
            }
            if(namedType.IsTupleType) {
                foreach(var element in namedType.TupleElements) {
                    if(!CanReferenceTypeFromGeneratedSource(element.Type)) {
                        return false;
                    }
                }
            }
        }
        return true;
    }

    private static bool CanReferenceNamedTypeFromGeneratedSource(INamedTypeSymbol type) {
        for(var current = type; current is not null; current=current.ContainingType) {
            if(current.DeclaredAccessibility is Accessibility.Private or Accessibility.Protected or Accessibility.ProtectedAndInternal) {
                return false;
            }
        }
        return true;
    }

    private static bool ContainsTypeParameter(ITypeSymbol type) {
        if(type is ITypeParameterSymbol) {
            return true;
        }
        if(type is IArrayTypeSymbol arrayType) {
            return ContainsTypeParameter(arrayType.ElementType);
        }
        if(type is INamedTypeSymbol namedType) {
            foreach(var typeArgument in namedType.TypeArguments) {
                if(ContainsTypeParameter(typeArgument)) {
                    return true;
                }
            }
            if(namedType.IsTupleType) {
                foreach(var element in namedType.TupleElements) {
                    if(ContainsTypeParameter(element.Type)) {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    private static string LINQ呼び出し型引数具体化(string body,IMethodSymbol method) {
        var definition = GetLinqInvocationDefinition(method);
        var updatedBody = body;
        for(var index = 0; index<definition.TypeParameters.Length&&index<method.TypeArguments.Length; index++) {
            updatedBody=updatedBody.Replace(definition.TypeParameters[index].Name,BuildGeneratedTypeName(method.TypeArguments[index]));
        }
        return updatedBody;
    }

    private static bool TryBuildStaticLinqLambdaStrategy(InvocationExpressionSyntax invocation,IMethodSymbol method,string identifier,out LinqInvocationLambdaStrategy strategy) {
        strategy=null!;
        var definition = GetLinqInvocationDefinition(method);
        if(definition.Name!="Where"&&definition.Name!="Select") {
            return false;
        }
        var operationArgumentIndex = LINQ呼び出し操作引数位置取得(method);
        if(invocation.ArgumentList.Arguments.Count<=operationArgumentIndex
            ||invocation.ArgumentList.Arguments[operationArgumentIndex].Expression is not LambdaExpressionSyntax lambda
            ||!IsStaticLambda(lambda)
            ||lambda.Body is not ExpressionSyntax lambdaBody) {
            return false;
        }
        if(!TryGetSingleLambdaParameterName(lambda,out var parameterName)) {
            return false;
        }
        if(!CanMoveStaticLambdaBody(lambdaBody,parameterName)) {
            return false;
        }
        if(method.TypeArguments.Length==0||method.TypeArguments.Any(ContainsTypeParameter)) {
            return false;
        }
        var sourceTypeName = BuildGeneratedTypeName(method.TypeArguments[0]);
        var lambdaBodyText = NormalizeExpression(lambdaBody);
        var lambdaExpressionText = NormalizeExpression(lambda);
        if(definition.Name=="Where") {
            var typeName = $"__絞込ラムダ_{identifier}";
            var source = $$"""
        private readonly struct {{typeName}}:global::Seamless.SourceGenerator.Generated.LinqInvocationFusionSupport.I融合絞込<{{sourceTypeName}}> {
            public static bool Invoke({{sourceTypeName}} {{parameterName}}) => {{lambdaBodyText}};
        }
""";
            strategy=new LinqInvocationLambdaStrategy("Where",typeName,"",lambdaExpressionText,source);
            return true;
        }
        if(method.TypeArguments.Length<2) {
            return false;
        }
        var resultTypeName = BuildGeneratedTypeName(method.TypeArguments[1]);
        var selectorTypeName = $"__選択ラムダ_{identifier}";
        var selectorSource = $$"""
        private readonly struct {{selectorTypeName}}:global::Seamless.SourceGenerator.Generated.LinqInvocationFusionSupport.I融合選択<{{sourceTypeName}},{{resultTypeName}}> {
            public static {{resultTypeName}} Invoke({{sourceTypeName}} {{parameterName}}) => {{lambdaBodyText}};
        }
""";
        strategy=new LinqInvocationLambdaStrategy("Select",selectorTypeName,resultTypeName,lambdaExpressionText,selectorSource);
        return true;
    }

    private static bool CanMoveStaticLambdaBody(ExpressionSyntax lambdaBody,string parameterName) {
        var checker = new MovableStaticLambdaBodyChecker(parameterName);
        checker.Visit(lambdaBody);
        return checker.CanMove;
    }

    private static bool IsStaticLambda(LambdaExpressionSyntax lambda) {
        return lambda.Modifiers.Any(SyntaxKind.StaticKeyword);
    }

    private static bool TryGetSingleLambdaParameterName(LambdaExpressionSyntax lambda,out string parameterName) {
        parameterName=lambda switch {
            SimpleLambdaExpressionSyntax simpleLambda => simpleLambda.Parameter.Identifier.ValueText,
            ParenthesizedLambdaExpressionSyntax parenthesizedLambda when parenthesizedLambda.ParameterList.Parameters.Count==1 => parenthesizedLambda.ParameterList.Parameters[0].Identifier.ValueText,
            _ => string.Empty
        };
        return !string.IsNullOrWhiteSpace(parameterName);
    }

    private static string LINQ呼び出し静的ラムダ適用(string body,LinqInvocationLambdaStrategy strategy) {
        var literal = ToLiteral(strategy.ExpressionText);
        return strategy.Kind switch {
            "Where" => body
                .Replace($".絞込追加(predicate,{literal})",$".絞込追加<{strategy.TypeName}>({literal})")
                .Replace(".絞込追加(predicate)",$".絞込追加<{strategy.TypeName}>()"),
            "Select" => body
                .Replace($".選択追加(selector,{literal})",$".選択追加<{strategy.ResultTypeName},{strategy.TypeName}>({literal})")
                .Replace(".選択追加(selector)",$".選択追加<{strategy.ResultTypeName},{strategy.TypeName}>()")
                .Replace(".選択実体化(selector)",$".選択実体化<{strategy.ResultTypeName},{strategy.TypeName}>()"),
            _ => body
        };
    }

    private static string LINQ呼び出しデバッグ式埋込(string body,InvocationExpressionSyntax invocation,IMethodSymbol method) {
        var sourceExpression = LINQ呼び出しソース式取得(invocation,method);
        if(sourceExpression is null) {
            return body;
        }

        var updatedBody = body.Replace(
            "global::Seamless.SourceGenerator.Generated.LinqInvocationFusionSupport.Create(source)",
            $"global::Seamless.SourceGenerator.Generated.LinqInvocationFusionSupport.Create(source,{ToLiteral(sourceExpression)})");

        var definition = GetLinqInvocationDefinition(method);
        var operationArgumentIndex = LINQ呼び出し操作引数位置取得(method);
        if(definition.Name=="Select"&&invocation.ArgumentList.Arguments.Count>operationArgumentIndex) {
            var selectorExpression = NormalizeExpression(invocation.ArgumentList.Arguments[operationArgumentIndex].Expression);
            updatedBody=updatedBody.Replace(
                ".選択追加(selector)",
                $".選択追加(selector,{ToLiteral(selectorExpression)})");
        }
        else if(definition.Name=="Where"&&invocation.ArgumentList.Arguments.Count>operationArgumentIndex) {
            var predicateExpression = NormalizeExpression(invocation.ArgumentList.Arguments[operationArgumentIndex].Expression);
            updatedBody=updatedBody.Replace(
                ".絞込追加(predicate)",
                $".絞込追加(predicate,{ToLiteral(predicateExpression)})");
        }
        else if(definition.Name=="Take"&&invocation.ArgumentList.Arguments.Count>operationArgumentIndex) {
            var countExpression = NormalizeExpression(invocation.ArgumentList.Arguments[operationArgumentIndex].Expression);
            updatedBody=updatedBody.Replace(
                ".先頭取得追加(count)",
                $".先頭取得追加(count,{ToLiteral(countExpression)})");
        }

        return updatedBody;
    }

    private static string? LINQ呼び出しソース式取得(InvocationExpressionSyntax invocation,IMethodSymbol method) {
        if(method.ReducedFrom is not null) {
            return invocation.Expression is MemberAccessExpressionSyntax memberAccess
                ? NormalizeExpression(memberAccess.Expression)
                : null;
        }
        return invocation.ArgumentList.Arguments.Count>0
            ? NormalizeExpression(invocation.ArgumentList.Arguments[0].Expression)
            : null;
    }

    private static int LINQ呼び出し操作引数位置取得(IMethodSymbol method) {
        return method.ReducedFrom is null ? 1 : 0;
    }

    private static string BuildConcreteParameterDeclaration(IParameterSymbol parameter) {
        var builder = new StringBuilder();
        if(parameter.IsParams) {
            builder.Append("params ");
        }
        switch(parameter.RefKind) {
            case RefKind.Ref:
                builder.Append("ref ");
                break;
            case RefKind.Out:
                builder.Append("out ");
                break;
            case RefKind.In:
                builder.Append("in ");
                break;
        }
        builder.Append(BuildGeneratedTypeName(parameter.Type));
        builder.Append(' ');
        builder.Append(parameter.Name);
        return builder.ToString();
    }

    private static bool IsSupportedLinqMethod(IMethodSymbol method) {
        var definition = method.ReducedFrom??method;
        if(definition.MethodKind!=MethodKind.Ordinary||definition.ContainingType is null) {
            return false;
        }
        var containingType = definition.ContainingType.ToDisplayString();
        return containingType=="System.Linq.Enumerable"
            ||containingType=="Seamless.Linq.Enumerable"
            ||containingType=="Seamless.Linq.HashSet";
    }

    private static bool IsContainerMethodInterceptCandidate(IMethodSymbol method) {
        if(method.MethodKind!=MethodKind.Ordinary
            ||method.IsStatic
            ||method.IsVirtual
            ||method.IsAbstract
            ||method.ContainingType.TypeKind!=TypeKind.Class
            ||method.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal)
            ||method.DeclaringSyntaxReferences.Length==0) {
            return false;
        }
        if(UsesInvocationLinqInterceptMode(method)) {
            return false;
        }
        return true;
    }

    private static Location GetInvocationMethodLocation(InvocationExpressionSyntax invocation) {
        if(invocation.Expression is MemberAccessExpressionSyntax memberAccess) {
            return memberAccess.Name.GetLocation();
        }
        return invocation.Expression.GetLocation();
    }

    private static bool TryBuildContainingMethodCref(InvocationExpressionSyntax invocation,SemanticModel semanticModel,System.Threading.CancellationToken cancellationToken,out string cref) {
        cref=string.Empty;
        if(invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>() is not { } containingMethodSyntax) {
            return false;
        }
        var containingMethod = semanticModel.GetDeclaredSymbol(containingMethodSyntax,cancellationToken);
        if(containingMethod is null) {
            return false;
        }
        cref=BuildMethodCref(containingMethod);
        return true;
    }

    private static string BuildMethodCref(IMethodSymbol method) {
        var containingType = method.ContainingType.ToDisplayString(FullyQualifiedFormat);
        return method.Parameters.Length==0
            ? $"{containingType}.{method.Name}()"
            : $"{containingType}.{method.Name}";
    }

    private static string RewriteInstanceReceiverReferences(string body) {
        return body
            .Replace("this.","self.")
            .Replace("EqualityComparer<","global::System.Collections.Generic.EqualityComparer<")
            .Replace(" Comparer<"," global::System.Collections.Generic.Comparer<")
            .Replace("(Comparer<","(global::System.Collections.Generic.Comparer<")
            .Replace(",Comparer<",",global::System.Collections.Generic.Comparer<");
    }

    private static bool TryGetWherePredicateInfo(IMethodSymbol reducedMethod,out bool usesIndex) {
        usesIndex=false;
        if(reducedMethod.Parameters.Length!=2
            ||reducedMethod.Parameters[1].Type is not INamedTypeSymbol predicateType
            ||predicateType.Name!="Func"
            ||predicateType.ContainingNamespace.ToDisplayString()!="System") {
            return false;
        }
        if(predicateType.TypeArguments.Length==2
            &&predicateType.TypeArguments[1].SpecialType==SpecialType.System_Boolean) {
            return true;
        }
        if(predicateType.TypeArguments.Length==3
            &&predicateType.TypeArguments[1].SpecialType==SpecialType.System_Int32
            &&predicateType.TypeArguments[2].SpecialType==SpecialType.System_Boolean) {
            usesIndex=true;
            return true;
        }
        return false;
    }

    private static bool TryGetWhereCallInfo(InvocationExpressionSyntax invocation,MemberAccessExpressionSyntax memberAccess,IMethodSymbol method,out ExpressionSyntax sourceExpression,out ExpressionSyntax predicateExpression) {
        sourceExpression=null!;
        predicateExpression=null!;

        if(method.ReducedFrom is not null) {
            if(invocation.ArgumentList.Arguments.Count!=1) {
                return false;
            }
            sourceExpression=memberAccess.Expression;
            predicateExpression=invocation.ArgumentList.Arguments[0].Expression;
            return true;
        }

        if(invocation.ArgumentList.Arguments.Count!=2) {
            return false;
        }
        sourceExpression=invocation.ArgumentList.Arguments[0].Expression;
        predicateExpression=invocation.ArgumentList.Arguments[1].Expression;
        return true;
    }

    private static bool IsListWhereInterceptorCandidate(InvocationExpressionSyntax invocation,IMethodSymbol method,SemanticModel semanticModel,System.Threading.CancellationToken cancellationToken) {
        if(invocation.Expression is not MemberAccessExpressionSyntax memberAccess) {
            return false;
        }
        if(method.Name!="Where"||!method.IsExtensionMethod) {
            return false;
        }
        var reducedMethod = method.ReducedFrom??method;
        if(reducedMethod.ContainingType.ToDisplayString()!="System.Linq.Enumerable") {
            return false;
        }
        if(!TryGetWherePredicateInfo(reducedMethod,out _)) {
            return false;
        }
        if(!TryGetWhereCallInfo(invocation,memberAccess,method,out var sourceExpression,out _)) {
            return false;
        }
        return TryGetListElementType(semanticModel.GetTypeInfo(sourceExpression,cancellationToken).Type,out _);
    }

    private static bool TryGetListElementType(ITypeSymbol? type,out ITypeSymbol elementType) {
        for(var current = type; current is not null; current=current.BaseType) {
            if(current is INamedTypeSymbol namedType
                &&namedType.Name=="List"
                &&namedType.ContainingNamespace.ToDisplayString()=="System.Collections.Generic"
                &&namedType.TypeArguments.Length==1) {
                elementType=namedType.TypeArguments[0];
                return true;
            }
        }

        elementType=null!;
        return false;
    }

    private static bool TryGetPredicateInfo(SemanticModel semanticModel,ExpressionSyntax predicateExpression,System.Threading.CancellationToken cancellationToken,bool usesIndex,out string parameterName,out string? indexParameterName,out string predicateText,out bool usePredicateDelegate) {
        parameterName="candidate";
        indexParameterName=usesIndex ? "candidateIndex" : null;
        predicateText=predicateExpression.ToString();
        usePredicateDelegate=true;

        if(predicateExpression is not LambdaExpressionSyntax lambda) {
            return true;
        }

        if(lambda switch {
            SimpleLambdaExpressionSyntax => usesIndex,
            ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.ParameterList.Parameters.Count!=(usesIndex ? 2 : 1),
            _ => true,
        }) {
            return false;
        }

        parameterName=lambda switch {
            SimpleLambdaExpressionSyntax simple => simple.Parameter.Identifier.ValueText,
            ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.ParameterList.Parameters[0].Identifier.ValueText,
            _ => string.Empty,
        };
        if(string.IsNullOrWhiteSpace(parameterName)) {
            return false;
        }
        if(usesIndex&&lambda is ParenthesizedLambdaExpressionSyntax indexedLambda) {
            indexParameterName=indexedLambda.ParameterList.Parameters[1].Identifier.ValueText;
            if(string.IsNullOrWhiteSpace(indexParameterName)) {
                return false;
            }
        }

        predicateText=lambda.Body.ToString();
        if(lambda.Body is not ExpressionSyntax expressionBody) {
            return true;
        }

        predicateText=expressionBody.ToString();
        var lambdaParameters = new HashSet<string>(StringComparer.Ordinal) { parameterName };
        if(indexParameterName is not null) {
            lambdaParameters.Add(indexParameterName);
        }
        if(CanInlinePredicateExpression(semanticModel,expressionBody,cancellationToken,lambdaParameters)) {
            usePredicateDelegate=false;
        }
        return true;
    }

    private static bool CanInlinePredicateExpression(SemanticModel semanticModel,ExpressionSyntax expressionBody,System.Threading.CancellationToken cancellationToken,HashSet<string> lambdaParameters) {
        foreach(var identifier in expressionBody.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()) {
            var symbol = semanticModel.GetSymbolInfo(identifier,cancellationToken).Symbol;
            if(symbol is null) {
                continue;
            }
            if(lambdaParameters.Contains(identifier.Identifier.ValueText)) {
                continue;
            }
            if(symbol is ILocalSymbol||symbol is IParameterSymbol) {
                return false;
            }
        }
        return true;
    }

    private static TypeTarget? CreateTypeTarget(GeneratorSyntaxContext context) {
        if(context.Node is not TypeDeclarationSyntax declaration) {
            return null;
        }
        var symbol = context.SemanticModel.GetDeclaredSymbol(declaration);
        if(symbol is null) {
            return null;
        }
        var isMarker = symbol.Name=="DatabaseGenerator";
        var isPartial = declaration.Modifiers.Any(SyntaxKind.PartialKeyword);
        var isContainerCandidate = symbol.TypeKind==TypeKind.Class;
        if(!isMarker&&!isPartial&&!isContainerCandidate) {
            return null;
        }
        return new TypeTarget(symbol,isPartial);
    }

    private static OptimizeMethodTarget? CreateOptimizeMethodTarget(GeneratorSyntaxContext context) {
        if(context.Node is not MethodDeclarationSyntax syntax) {
            return null;
        }
        var symbol = context.SemanticModel.GetDeclaredSymbol(syntax);
        if(symbol is null) {
            return null;
        }
        if(!HasOptimizeAttribute(symbol)&&!HasOptimizeAttributeSyntax(syntax.AttributeLists)) {
            return null;
        }
        return new OptimizeMethodTarget(symbol,syntax.Identifier.GetLocation());
    }

    private static void ReportOptimizeDiagnostics(SourceProductionContext context,ImmutableArray<TypeTarget> typeTargets,ImmutableArray<OptimizeMethodTarget> optimizeMethodTargets) {
        foreach(var classSymbol in GetDistinctTypes(typeTargets.Select(static target => target.Symbol)).Where(HasOptimizeAttribute)) {
            if(classSymbol.TypeKind!=TypeKind.Class||classSymbol.IsSealed) {
                context.ReportDiagnostic(Diagnostic.Create(
                    OptimizeClassMustBeNonSealed,
                    classSymbol.Locations.FirstOrDefault(),
                    classSymbol.Name));
            }
        }

        foreach(var target in optimizeMethodTargets.Distinct(OptimizeMethodTargetComparer.Instance)) {
            var method = target.Symbol;
            if(!HasOptimizeAttribute(method.ContainingType)) {
                context.ReportDiagnostic(Diagnostic.Create(
                    OptimizeMethodRequiresOptimizeClass,
                    target.Location,
                    method.Name,
                    method.ContainingType.Name));
            }
            if(!IsOptimizeMethodSemanticallyValid(method)) {
                context.ReportDiagnostic(Diagnostic.Create(
                    OptimizeMethodMustBeVirtual,
                    target.Location,
                    method.Name));
            }
        }
    }

    private static bool HasOptimizeAttribute(ISymbol symbol) {
        foreach(var attribute in symbol.GetAttributes()) {
            var attributeClass = attribute.AttributeClass;
            if(attributeClass is null) {
                continue;
            }
            if(attributeClass.Name=="OptimizeAttribute"&&attributeClass.ContainingNamespace.ToDisplayString()=="Seamless") {
                return true;
            }
            if(attributeClass.Name=="Optimize"&&attributeClass.ContainingNamespace.ToDisplayString()=="Seamless") {
                return true;
            }
        }
        return false;
    }

    private static bool UsesInvocationLinqInterceptMode(IMethodSymbol method) {
        if(TryGetLinqInterceptMode(method,out var methodMode)) {
            return methodMode=="Invocation";
        }
        if(TryGetLinqInterceptMode(method.ContainingType,out var typeMode)) {
            return typeMode=="Invocation";
        }
        return false;
    }

    private static bool TryGetLinqInterceptMode(ISymbol symbol,out string mode) {
        foreach(var attribute in symbol.GetAttributes()) {
            var attributeClass = attribute.AttributeClass;
            if(attributeClass is null||attributeClass.Name!="LinqInterceptAttribute"||attributeClass.ContainingNamespace.ToDisplayString()!="Seamless") {
                continue;
            }
            if(attribute.ConstructorArguments.Length==1) {
                var value = attribute.ConstructorArguments[0].Value;
                mode=value is int intValue
                    ? (intValue==1 ? "Invocation" : "ContainerMethod")
                    : value?.ToString()??string.Empty;
                return !string.IsNullOrWhiteSpace(mode);
            }
        }
        mode=string.Empty;
        return false;
    }

    private static bool HasOptimizeAttributeSyntax(SyntaxList<AttributeListSyntax> attributeLists) {
        foreach(var attribute in attributeLists.SelectMany(static list => list.Attributes)) {
            var name = attribute.Name.ToString();
            if(name=="Optimize"||name=="OptimizeAttribute"||name.EndsWith(".Optimize",StringComparison.Ordinal)||name.EndsWith(".OptimizeAttribute",StringComparison.Ordinal)) {
                return true;
            }
        }
        return false;
    }

    private static bool IsOptimizeMethodSemanticallyValid(IMethodSymbol method) {
        return method.MethodKind==MethodKind.Ordinary
            &&method.IsVirtual
            &&!method.IsStatic
            &&!method.IsAbstract
            &&!method.IsSealed
            &&method.DeclaringSyntaxReferences.Length>0
            &&method.DeclaredAccessibility is Accessibility.Public
                or Accessibility.Internal
                or Accessibility.Protected
                or Accessibility.ProtectedOrInternal
                or Accessibility.ProtectedAndInternal;
    }

    private static IEnumerable<INamedTypeSymbol> GetDistinctTypes(IEnumerable<INamedTypeSymbol> symbols) {
        return symbols
            .GroupBy(static symbol => symbol.ToDisplayString(FullyQualifiedFormat),StringComparer.Ordinal)
            .Select(static group => group.First());
    }

    private static string GetNamespaceText(INamespaceSymbol namespaceSymbol) {
        return namespaceSymbol.IsGlobalNamespace ? string.Empty : namespaceSymbol.ToDisplayString();
    }

    // Where interceptor が使う reusable enumerable / query helper を出力する。
    private static string BuildOptimizedCodeSource(ImmutableArray<WhereInterceptTarget> whereTargets) {
        if(whereTargets.IsDefaultOrEmpty) {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("#nullable enable");
        builder.AppendLine("// Generated: query-syntax helper extensions so List<T>.Where can stay on the optimized path.");
        AppendListWhereQueryHelperSource(builder);
        return builder.ToString();
    }

    // InterceptsLocation 付き entry point を出力する。
    private static string BuildInterceptorSource(ImmutableArray<WhereInterceptTarget> whereTargets,ImmutableArray<ContainerMethodInterceptTarget> containerMethodTargets,ImmutableArray<LinqInvocationInterceptTarget> linqInvocationTargets) {
        if(whereTargets.IsDefaultOrEmpty&&containerMethodTargets.IsDefaultOrEmpty&&linqInvocationTargets.IsDefaultOrEmpty) {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("#nullable enable");
        builder.AppendLine("#pragma warning disable CS8603, CS8619, CS8620, CS9159");
        builder.AppendLine("// Generated: Roslyn interceptors resolve the preview attribute by metadata name.");
        builder.AppendLine("// The target framework does not provide it, so this file-local definition is required by the generated [InterceptsLocation] entry points below.");
        builder.AppendLine("namespace System.Runtime.CompilerServices {");
        builder.AppendLine("    [global::System.AttributeUsage(global::System.AttributeTargets.Method,AllowMultiple=true)]");
        builder.AppendLine("    file sealed class InterceptsLocationAttribute:global::System.Attribute {");
        builder.AppendLine("        public InterceptsLocationAttribute(int version,string data) {");
        builder.AppendLine("            this.Version=version;");
        builder.AppendLine("            this.Data=data;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        public int Version { get; }");
        builder.AppendLine("        public string Data { get; }");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("// Generated: interceptor entry points.");
        builder.AppendLine("namespace Seamless.SourceGenerator.Generated {");
        if(!whereTargets.IsDefaultOrEmpty) {
            builder.AppendLine("    internal static partial class ListWhereInterceptors {");

            foreach(var target in whereTargets.Distinct(WhereInterceptTargetComparer.Instance)) {
                var elementTypeName = target.ElementTypeName;
                var predicateTypeName = target.UsesIndex
                    ? $"global::System.Func<{elementTypeName},int,bool>"
                    : $"global::System.Func<{elementTypeName},bool>";
                AppendInterceptedCallComment(builder,target.ContainingMethodCref,"        ");
                builder.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                builder.AppendLine($"        {target.InterceptsLocationAttributeText}");
                builder.AppendLine($"        public static global::System.Collections.Generic.IEnumerable<{elementTypeName}> {target.MethodName}(this global::System.Collections.Generic.IEnumerable<{elementTypeName}> source,{predicateTypeName} predicate) {{");
                builder.AppendLine($"            global::System.Diagnostics.Debug.WriteLine(\"Seamless interceptor: {target.MethodName} ({target.Line}:{target.Character}) predicate=\" + {ToLiteral(target.PredicateText)});");
                builder.AppendLine($"            global::System.Console.WriteLine(\"Seamless interceptor: {target.MethodName} ({target.Line}:{target.Character})\");");
                builder.AppendLine($"            return new {target.EnumerableTypeName}((global::System.Collections.Generic.List<{elementTypeName}>)source,predicate);");
                builder.AppendLine("        }");
                builder.AppendLine();
                builder.AppendLine($"        public readonly struct {target.EnumerableTypeName}:global::System.Collections.Generic.IEnumerable<{elementTypeName}> {{");
                builder.AppendLine($"            private readonly global::System.Collections.Generic.List<{elementTypeName}> source;");
                builder.AppendLine($"            private readonly {predicateTypeName} predicate;");
                builder.AppendLine();
                builder.AppendLine($"            public {target.EnumerableTypeName}(global::System.Collections.Generic.List<{elementTypeName}> source,{predicateTypeName} predicate) {{");
                builder.AppendLine("                this.source=source ?? throw new global::System.ArgumentNullException(nameof(source));");
                builder.AppendLine("                this.predicate=predicate ?? throw new global::System.ArgumentNullException(nameof(predicate));");
                builder.AppendLine("            }");
                builder.AppendLine();
                builder.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                builder.AppendLine("            public Enumerator GetEnumerator() => new(this.source,this.predicate);");
                builder.AppendLine();
                builder.AppendLine($"            global::System.Collections.Generic.IEnumerator<{elementTypeName}> global::System.Collections.Generic.IEnumerable<{elementTypeName}>.GetEnumerator() {{");
                builder.AppendLine("                var enumerator = this.GetEnumerator();");
                builder.AppendLine("                while(enumerator.MoveNext()) {");
                builder.AppendLine("                    yield return enumerator.Current;");
                builder.AppendLine("                }");
                builder.AppendLine("            }");
                builder.AppendLine();
                builder.AppendLine($"            global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => ((global::System.Collections.Generic.IEnumerable<{elementTypeName}>)this).GetEnumerator();");
                builder.AppendLine();
                builder.AppendLine("            public struct Enumerator {");
                builder.AppendLine($"                private readonly global::System.Collections.Generic.List<{elementTypeName}> source;");
                builder.AppendLine($"                private readonly {predicateTypeName} predicate;");
                builder.AppendLine("                private int index;");
                builder.AppendLine();
                builder.AppendLine($"                public Enumerator(global::System.Collections.Generic.List<{elementTypeName}> source,{predicateTypeName} predicate) {{");
                builder.AppendLine("                    this.source=source;");
                builder.AppendLine("                    this.predicate=predicate;");
                builder.AppendLine("                    this.index=-1;");
                builder.AppendLine("                    this.Current=default!;");
                builder.AppendLine("                }");
                builder.AppendLine();
                builder.AppendLine($"                public {elementTypeName} Current {{ get; private set; }}");
                builder.AppendLine();
                builder.AppendLine("                [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                builder.AppendLine("                public bool MoveNext() {");
                builder.AppendLine("                    while(true) {");
                builder.AppendLine("                        var next = this.index+1;");
                builder.AppendLine("                        if(next>=this.source.Count) {");
                builder.AppendLine("                            return false;");
                builder.AppendLine("                        }");
                builder.AppendLine("                        this.index=next;");
                if(target.UsePredicateDelegate) {
                    builder.AppendLine("                        var candidate = this.source[next];");
                    builder.AppendLine(target.UsesIndex
                        ? "                        if(!this.predicate(candidate,next)) {"
                        : "                        if(!this.predicate(candidate)) {");
                    builder.AppendLine("                            continue;");
                    builder.AppendLine("                        }");
                    builder.AppendLine("                        this.Current=candidate;");
                }
                else {
                    builder.AppendLine($"                        var {target.ParameterName} = this.source[next];");
                    if(target.IndexParameterName is not null) {
                        builder.AppendLine($"                        var {target.IndexParameterName} = next;");
                    }
                    builder.AppendLine($"                        if(!({target.PredicateText})) {{");
                    builder.AppendLine("                            continue;");
                    builder.AppendLine("                        }");
                    builder.AppendLine($"                        this.Current={target.ParameterName};");
                }
                builder.AppendLine("                        return true;");
                builder.AppendLine("                    }");
                builder.AppendLine("                }");
                builder.AppendLine("            }");
                builder.AppendLine("        }");
                builder.AppendLine();
            }

            builder.AppendLine("    }");
            builder.AppendLine();
        }

        AppendContainerMethodInterceptorSource(builder,containerMethodTargets);
        AppendLinqInvocationInterceptorSource(builder,linqInvocationTargets);
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void AppendLinqInvocationInterceptorSource(StringBuilder builder,ImmutableArray<LinqInvocationInterceptTarget> linqInvocationTargets) {
        if(linqInvocationTargets.IsDefaultOrEmpty) {
            return;
        }

        builder.AppendLine("    internal static partial class LinqInvocationInterceptors {");
        foreach(var target in linqInvocationTargets.Distinct(LinqInvocationInterceptTargetComparer.Instance)) {
            AppendInterceptedCallComment(builder,target.ContainingMethodCref,"        ");
            builder.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            builder.AppendLine($"        {target.InterceptsLocationAttributeText}");
            var methodDeclaration = $"public static {target.ReturnTypeName} {target.MethodName}{target.TypeParameterList}({target.ParameterList}){target.TypeParameterConstraintList}";
            AppendIndented(builder,BuildTranslatedMemberSource(methodDeclaration,target.Body,target.IsExpressionBody),"        ");
            if(!string.IsNullOrWhiteSpace(target.StaticLambdaStrategySource)) {
                builder.AppendLine(target.StaticLambdaStrategySource);
            }
            builder.AppendLine();
        }
        builder.AppendLine("    }");
    }


    private static void AppendContainerMethodInterceptorSource(StringBuilder builder,ImmutableArray<ContainerMethodInterceptTarget> containerMethodTargets) {
        if(containerMethodTargets.IsDefaultOrEmpty) {
            return;
        }

        builder.AppendLine("    internal static partial class ContainerMethodInterceptors {");
        foreach(var target in containerMethodTargets.Distinct(ContainerMethodInterceptTargetComparer.Instance)) {
            var parameterList = string.IsNullOrWhiteSpace(target.ParameterList)
                ? $"this {target.ReceiverTypeName} self"
                : $"this {target.ReceiverTypeName} self, {target.ParameterList}";
            AppendInterceptedCallComment(builder,target.ContainingMethodCref,"        ");
            builder.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            builder.AppendLine($"        {target.InterceptsLocationAttributeText}");
            var methodDeclaration = $"public static {target.ReturnTypeName} {target.MethodName}{target.TypeParameterList}({parameterList}){target.TypeParameterConstraintList}";
            AppendIndented(builder,BuildTranslatedMemberSource(methodDeclaration,target.Body,target.IsExpressionBody),"        ");
            builder.AppendLine();
        }
        builder.AppendLine("    }");
    }

    private static void AppendInterceptedCallComment(StringBuilder builder,string containingMethodCref,string indent) {
        builder.AppendLine($"{indent}/// <summary>");
        builder.AppendLine($"{indent}/// Intercepts a call inside <see cref=\"{EscapeXmlAttribute(containingMethodCref)}\"/>.");
        builder.AppendLine($"{indent}/// </summary>");
    }

    private static string EscapeXmlAttribute(string value) {
        return value
            .Replace("&","&amp;")
            .Replace("\"","&quot;")
            .Replace("<","&lt;")
            .Replace(">","&gt;");
    }

    private static void AppendListWhereQueryHelperSource(StringBuilder builder) {
        builder.AppendLine("namespace System.Linq {");
        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// Query syntax binds to these helpers before it falls back to Enumerable.Where. See <see cref=\"Test.Seamless.Diagnostics.InterceptorSmokeTests.ListWhere_Interceptor_Smoke\"/>.");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine("    internal static class ListWhereQueryExtensions {");
        var enumerableBuilder = new StringBuilder();
        AppendReusableListWhereEnumerableSource(enumerableBuilder);
        AppendIndented(builder,enumerableBuilder.ToString(),"    ");
        builder.AppendLine();
        builder.AppendLine("        /// <summary>");
        builder.AppendLine("        /// Replaces List&lt;T&gt;.Where(predicate) query binding with the optimized enumerable used by <see cref=\"Test.Seamless.Diagnostics.InterceptorSmokeTests.ListWhere_Interceptor_Smoke\"/>.");
        builder.AppendLine("        /// </summary>");
        builder.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        builder.AppendLine("        public static OptimizedListWhereEnumerable<TSource> Where<TSource>(this global::System.Collections.Generic.List<TSource> source,global::System.Func<TSource,bool> predicate) => new(source,predicate);");
        builder.AppendLine();
        builder.AppendLine("        /// <summary>");
        builder.AppendLine("        /// Replaces List&lt;T&gt;.Where((value, index) =&gt; ...) query binding with the optimized enumerable used by <see cref=\"Test.Seamless.Diagnostics.InterceptorSmokeTests.ListWhere_Index_Interceptor_Smoke\"/>.");
        builder.AppendLine("        /// </summary>");
        builder.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        builder.AppendLine("        public static OptimizedListWhereIndexedEnumerable<TSource> Where<TSource>(this global::System.Collections.Generic.List<TSource> source,global::System.Func<TSource,int,bool> predicate) => new(source,predicate);");
        builder.AppendLine("    }");
        builder.AppendLine("}");
    }

    private static void AppendReusableListWhereEnumerableSource(StringBuilder builder) {
        builder.AppendLine("    // Generated: allocation-light enumerable for List<T>.Where(predicate).");
        builder.AppendLine("    internal readonly struct OptimizedListWhereEnumerable<TSource>:global::System.Collections.Generic.IEnumerable<TSource> {");
        builder.AppendLine("        private readonly global::System.Collections.Generic.List<TSource> source;");
        builder.AppendLine("        private readonly global::System.Func<TSource,bool> predicate;");
        builder.AppendLine();
        builder.AppendLine("        public OptimizedListWhereEnumerable(global::System.Collections.Generic.List<TSource> source,global::System.Func<TSource,bool> predicate) {");
        builder.AppendLine("            this.source=source ?? throw new global::System.ArgumentNullException(nameof(source));");
        builder.AppendLine("            this.predicate=predicate ?? throw new global::System.ArgumentNullException(nameof(predicate));");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        builder.AppendLine("        public Enumerator GetEnumerator() => new(this.source,this.predicate);");
        builder.AppendLine();
        builder.AppendLine("        global::System.Collections.Generic.IEnumerator<TSource> global::System.Collections.Generic.IEnumerable<TSource>.GetEnumerator() {");
        builder.AppendLine("            var enumerator = this.GetEnumerator();");
        builder.AppendLine("            while(enumerator.MoveNext()) {");
        builder.AppendLine("                yield return enumerator.Current;");
        builder.AppendLine("            }");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => ((global::System.Collections.Generic.IEnumerable<TSource>)this).GetEnumerator();");
        builder.AppendLine();
        builder.AppendLine("        public struct Enumerator {");
        builder.AppendLine("            private readonly global::System.Collections.Generic.List<TSource> source;");
        builder.AppendLine("            private readonly global::System.Func<TSource,bool> predicate;");
        builder.AppendLine("            private int index;");
        builder.AppendLine();
        builder.AppendLine("            public Enumerator(global::System.Collections.Generic.List<TSource> source,global::System.Func<TSource,bool> predicate) {");
        builder.AppendLine("                this.source=source;");
        builder.AppendLine("                this.predicate=predicate;");
        builder.AppendLine("                this.index=-1;");
        builder.AppendLine("                this.Current=default!;");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine("            public TSource Current { get; private set; }");
        builder.AppendLine();
        builder.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        builder.AppendLine("            public bool MoveNext() {");
        builder.AppendLine("                while(true) {");
        builder.AppendLine("                    var next = this.index+1;");
        builder.AppendLine("                    if(next>=this.source.Count) {");
        builder.AppendLine("                        return false;");
        builder.AppendLine("                    }");
        builder.AppendLine("                    this.index=next;");
        builder.AppendLine("                    var candidate = this.source[next];");
        builder.AppendLine("                    if(!this.predicate(candidate)) {");
        builder.AppendLine("                        continue;");
        builder.AppendLine("                    }");
        builder.AppendLine("                    this.Current=candidate;");
        builder.AppendLine("                    return true;");
        builder.AppendLine("                }");
        builder.AppendLine("            }");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    // Generated: allocation-light enumerable for List<T>.Where((value, index) => ...).");
        builder.AppendLine("    internal readonly struct OptimizedListWhereIndexedEnumerable<TSource>:global::System.Collections.Generic.IEnumerable<TSource> {");
        builder.AppendLine("        private readonly global::System.Collections.Generic.List<TSource> source;");
        builder.AppendLine("        private readonly global::System.Func<TSource,int,bool> predicate;");
        builder.AppendLine();
        builder.AppendLine("        public OptimizedListWhereIndexedEnumerable(global::System.Collections.Generic.List<TSource> source,global::System.Func<TSource,int,bool> predicate) {");
        builder.AppendLine("            this.source=source ?? throw new global::System.ArgumentNullException(nameof(source));");
        builder.AppendLine("            this.predicate=predicate ?? throw new global::System.ArgumentNullException(nameof(predicate));");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        builder.AppendLine("        public Enumerator GetEnumerator() => new(this.source,this.predicate);");
        builder.AppendLine();
        builder.AppendLine("        global::System.Collections.Generic.IEnumerator<TSource> global::System.Collections.Generic.IEnumerable<TSource>.GetEnumerator() {");
        builder.AppendLine("            var enumerator = this.GetEnumerator();");
        builder.AppendLine("            while(enumerator.MoveNext()) {");
        builder.AppendLine("                yield return enumerator.Current;");
        builder.AppendLine("            }");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => ((global::System.Collections.Generic.IEnumerable<TSource>)this).GetEnumerator();");
        builder.AppendLine();
        builder.AppendLine("        public struct Enumerator {");
        builder.AppendLine("            private readonly global::System.Collections.Generic.List<TSource> source;");
        builder.AppendLine("            private readonly global::System.Func<TSource,int,bool> predicate;");
        builder.AppendLine("            private int index;");
        builder.AppendLine();
        builder.AppendLine("            public Enumerator(global::System.Collections.Generic.List<TSource> source,global::System.Func<TSource,int,bool> predicate) {");
        builder.AppendLine("                this.source=source;");
        builder.AppendLine("                this.predicate=predicate;");
        builder.AppendLine("                this.index=-1;");
        builder.AppendLine("                this.Current=default!;");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine("            public TSource Current { get; private set; }");
        builder.AppendLine();
        builder.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        builder.AppendLine("            public bool MoveNext() {");
        builder.AppendLine("                while(true) {");
        builder.AppendLine("                    var next = this.index+1;");
        builder.AppendLine("                    if(next>=this.source.Count) {");
        builder.AppendLine("                        return false;");
        builder.AppendLine("                    }");
        builder.AppendLine("                    this.index=next;");
        builder.AppendLine("                    var candidate = this.source[next];");
        builder.AppendLine("                    if(!this.predicate(candidate,next)) {");
        builder.AppendLine("                        continue;");
        builder.AppendLine("                    }");
        builder.AppendLine("                    this.Current=candidate;");
        builder.AppendLine("                    return true;");
        builder.AppendLine("                }");
        builder.AppendLine("            }");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine();
    }

    // DatabaseGenerator を起点にして schema struct を生成する。
    private static string BuildDatabaseSource(ImmutableArray<TypeTarget> typeTargets) {
        var builder = new StringBuilder();
        var rootNamespaces = typeTargets
            .Where(static target => target.Symbol.Name=="DatabaseGenerator")
            .Select(static target => GetNamespaceText(target.Symbol.ContainingNamespace))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if(rootNamespaces.Length==0) {
            return string.Empty;
        }
        builder.AppendLine("#pragma warning disable CS0164, CS8601");

        foreach(var rootNamespace in rootNamespaces) {
            var schemaRecords = typeTargets
                .Where(target => target.Symbol.Name=="DatabaseGenerator"&&GetNamespaceText(target.Symbol.ContainingNamespace)==rootNamespace)
                .SelectMany(target => GetSchemaRecordDefinitions(target.Symbol,rootNamespace))
                .GroupBy(static record => record.FullTypeName,StringComparer.Ordinal)
                .Select(static group => group.First())
                .ToArray();
            var schemaRecordTypeNames = new HashSet<string>(schemaRecords.Select(static record => record.FullTypeName),StringComparer.Ordinal);
            var keys = typeTargets
                .Where(target => target.IsPartial
                    &&target.Symbol.TypeKind==TypeKind.Struct
                    &&NamespaceEquals(target.Symbol.ContainingNamespace.ToDisplayString(),rootNamespace,"Keys")
                    &&!schemaRecordTypeNames.Contains(target.Symbol.ToDisplayString(FullyQualifiedFormat)))
                .Select(target => target.Symbol)
                .GroupBy(static symbol => symbol.ToDisplayString(FullyQualifiedFormat),StringComparer.Ordinal)
                .Select(static group => group.First())
                .OrderBy(static symbol => symbol.Name,StringComparer.Ordinal)
                .ToArray();
            var types = typeTargets
                .Where(target => target.IsPartial
                    &&target.Symbol.TypeKind==TypeKind.Struct
                    &&NamespaceEquals(target.Symbol.ContainingNamespace.ToDisplayString(),rootNamespace,"Records")
                    &&!schemaRecordTypeNames.Contains(target.Symbol.ToDisplayString(FullyQualifiedFormat)))
                .Select(target => target.Symbol)
                .GroupBy(static symbol => symbol.ToDisplayString(FullyQualifiedFormat),StringComparer.Ordinal)
                .Select(static group => group.First())
                .OrderBy(static symbol => symbol.Name,StringComparer.Ordinal)
                .ToArray();
            var generatedKeys = schemaRecords
                .Where(static record => string.Equals(record.NamespaceName,"Keys",StringComparison.OrdinalIgnoreCase))
                .OrderBy(static record => record.Name,StringComparer.Ordinal)
                .ToArray();
            var generatedTypes = schemaRecords
                .Where(static record => string.Equals(record.NamespaceName,"Records",StringComparison.OrdinalIgnoreCase))
                .OrderBy(static record => record.Name,StringComparer.Ordinal)
                .ToArray();
            var hasRootNs = !string.IsNullOrEmpty(rootNamespace);
            var rootNsIndent = hasRootNs ? "    " : "";
            var innerIndent = hasRootNs ? "        " : "    ";
            if(hasRootNs) {
                builder.AppendLine($"namespace {rootNamespace} {{");
            }

            if(generatedKeys.Length>0||keys.Length>0) {
                builder.AppendLine($"{rootNsIndent}// Generated: schema key records declared under {rootNamespace}.Keys.");
                builder.AppendLine($"{rootNsIndent}namespace Keys {{");
                foreach(var key in generatedKeys) {
                    AppendSchemaRecordSourceBody(builder,key,innerIndent);
                }
                foreach(var key in keys) {
                    AppendRecordSourceBody(builder,key,innerIndent);
                }
                builder.AppendLine($"{rootNsIndent}}}");
                builder.AppendLine();
            }

            if(generatedTypes.Length>0||types.Length>0) {
                builder.AppendLine($"{rootNsIndent}// Generated: schema row/value records declared under {rootNamespace}.Records.");
                builder.AppendLine($"{rootNsIndent}namespace Records {{");
                foreach(var type in generatedTypes) {
                    AppendSchemaRecordSourceBody(builder,type,innerIndent);
                }
                foreach(var type in types) {
                    AppendRecordSourceBody(builder,type,innerIndent);
                }
                builder.AppendLine($"{rootNsIndent}}}");
                builder.AppendLine();
            }

            if(hasRootNs) {
                builder.AppendLine("}");
            }
        }

        return builder.ToString();
    }

    // Key / Record の partial struct を、MemoryPack で保存できる完全な schema record にする。
    // unmanaged フィールドは _u、managed フィールドは _m に寄せ、backend から扱いやすい形へ整える。
    private static void AppendRecordSourceBody(StringBuilder builder,INamedTypeSymbol symbol,string indent) {
        var properties = GetGeneratableProperties(symbol);
        if(properties.Length==0) {
            return;
        }

        var symbolTypeName = symbol.ToDisplayString(FullyQualifiedFormat);

        var unmanagedProperties = properties.Where(static property => property.Type.IsUnmanagedType).ToImmutableArray();
        var managedProperties = properties.Where(static property => !property.Type.IsUnmanagedType).ToImmutableArray();

        var innerBuilder = new StringBuilder();
        {
            var indentedBuilder = innerBuilder;
            indentedBuilder.AppendLine($"// Generated: completed schema record for {symbolTypeName}. unmanaged data lives in _u and managed data lives in _m.");
            indentedBuilder.AppendLine($"partial struct {symbol.Name}:global::MemoryPack.IMemoryPackable<{symbolTypeName}> {{");
            indentedBuilder.AppendLine($"    public {symbol.Name}({string.Join(", ",properties.Select(static property => $"{property.Type.ToDisplayString(FullyQualifiedFormat)} {GetParameterName(property)}"))}) {{");
            if(unmanagedProperties.Length>0) {
                indentedBuilder.AppendLine($"        this._u=new({string.Join(", ",unmanagedProperties.Select(static property => GetParameterName(property)))});");
            }
            else {
                indentedBuilder.AppendLine("        this._u=new();");
            }
            if(managedProperties.Length>0) {
                indentedBuilder.AppendLine($"        this._m=new({string.Join(", ",managedProperties.Select(static property => GetParameterName(property)))});");
            }
            else {
                indentedBuilder.AppendLine("        this._m=new();");
            }
            indentedBuilder.AppendLine("    }");
            indentedBuilder.AppendLine();
            AppendUnmanagedRecordSource(indentedBuilder,unmanagedProperties);
            indentedBuilder.AppendLine("    private _U _u;");
            indentedBuilder.AppendLine();
            AppendManagedRecordSource(indentedBuilder,managedProperties);
            indentedBuilder.AppendLine("    private _M _m;");
            indentedBuilder.AppendLine();
            foreach(var property in properties) {
                indentedBuilder.AppendLine($"    public partial {property.Type.ToDisplayString(FullyQualifiedFormat)} {property.Name} => this.{GetStorageName(property)}.{property.Name};");
            }
            indentedBuilder.AppendLine();
            indentedBuilder.AppendLine($"    static {symbol.Name}() {{");
            indentedBuilder.AppendLine($"        global::MemoryPack.MemoryPackFormatterProvider.Register<{symbolTypeName}>();");
            indentedBuilder.AppendLine("    }");
            indentedBuilder.AppendLine();
            indentedBuilder.AppendLine("    static void global::MemoryPack.IMemoryPackFormatterRegister.RegisterFormatter() {");
            indentedBuilder.AppendLine($"        if(!global::MemoryPack.MemoryPackFormatterProvider.IsRegistered<{symbolTypeName}>()) {{");
            indentedBuilder.AppendLine($"            global::MemoryPack.MemoryPackFormatterProvider.Register(new global::MemoryPack.Formatters.MemoryPackableFormatter<{symbolTypeName}>());");
            indentedBuilder.AppendLine("        }");
            indentedBuilder.AppendLine($"        if(!global::MemoryPack.MemoryPackFormatterProvider.IsRegistered<{symbolTypeName}[]>()) {{");
            indentedBuilder.AppendLine($"            global::MemoryPack.MemoryPackFormatterProvider.Register(new global::MemoryPack.Formatters.ArrayFormatter<{symbolTypeName}>());");
            indentedBuilder.AppendLine("        }");
            indentedBuilder.AppendLine("    }");
            indentedBuilder.AppendLine();
            indentedBuilder.AppendLine($"    static void global::MemoryPack.IMemoryPackable<{symbolTypeName}>.Serialize<TBufferWriter>(ref global::MemoryPack.MemoryPackWriter<TBufferWriter> writer,scoped ref {symbolTypeName} value) {{");
            indentedBuilder.AppendLine("        writer.WriteUnmanaged(value._u);");
            indentedBuilder.AppendLine("        writer.WritePackable(value._m);");
            indentedBuilder.AppendLine("    }");
            indentedBuilder.AppendLine();
            indentedBuilder.AppendLine($"    static void global::MemoryPack.IMemoryPackable<{symbolTypeName}>.Deserialize(ref global::MemoryPack.MemoryPackReader reader,scoped ref {symbolTypeName} value) {{");
            indentedBuilder.AppendLine("        reader.ReadUnmanaged(out value._u);");
            indentedBuilder.AppendLine("        reader.ReadPackable(ref value._m);");
            indentedBuilder.AppendLine("    }");
            indentedBuilder.AppendLine("}");
        }
        AppendIndented(builder,innerBuilder.ToString(),indent);
        builder.AppendLine();
    }

    private static ImmutableArray<SchemaRecordDefinition> GetSchemaRecordDefinitions(INamedTypeSymbol marker,string rootNamespace) {
        var builder = ImmutableArray.CreateBuilder<SchemaRecordDefinition>();
        foreach(var attribute in marker.GetAttributes()) {
            if(attribute.AttributeClass?.Name!="DatabaseSchemaAttribute"||attribute.ConstructorArguments.Length!=2) {
                continue;
            }
            if(attribute.ConstructorArguments[0].Value is not string namespaceName||attribute.ConstructorArguments[1].Value is not string definitions) {
                continue;
            }
            foreach(var record in ParseSchemaDefinitions(rootNamespace,namespaceName,definitions)) {
                builder.Add(record);
            }
        }
        return builder.ToImmutable();
    }

    private static IEnumerable<SchemaRecordDefinition> ParseSchemaDefinitions(string rootNamespace,string namespaceName,string definitions) {
        var normalizedNamespaceName = namespaceName.Trim();
        if(normalizedNamespaceName.Length==0) {
            yield break;
        }
        foreach(var rawLine in definitions.Replace("\r\n","\n").Split('\n')) {
            var line = rawLine.Trim();
            if(line.Length==0||line.StartsWith("#",StringComparison.Ordinal)||line.StartsWith("//",StringComparison.Ordinal)) {
                continue;
            }
            var separatorIndex = line.IndexOf(':');
            if(separatorIndex<=0||separatorIndex==line.Length-1) {
                continue;
            }
            var recordName = line.Substring(0,separatorIndex).Trim();
            var propertyText = line.Substring(separatorIndex+1);
            var properties = ImmutableArray.CreateBuilder<SchemaPropertyDefinition>();
            foreach(var rawProperty in propertyText.Split(',')) {
                var propertyParts = rawProperty.Trim().Split(new[] { ' ', '\t' },StringSplitOptions.RemoveEmptyEntries);
                if(propertyParts.Length!=2) {
                    continue;
                }
                properties.Add(new SchemaPropertyDefinition(NormalizeSchemaTypeName(propertyParts[0]),propertyParts[1]));
            }
            if(recordName.Length>0&&properties.Count>0) {
                yield return new SchemaRecordDefinition(rootNamespace,normalizedNamespaceName,recordName,properties.ToImmutable());
            }
        }
    }

    private static string NormalizeSchemaTypeName(string typeName) {
        return typeName switch {
            "bool" or "byte" or "sbyte" or "short" or "ushort" or "int" or "uint" or "long" or "ulong" or "float" or "double" or "decimal" or "char" or "string" => typeName,
            "Boolean" => "bool",
            "Byte" => "byte",
            "SByte" => "sbyte",
            "Int16" => "short",
            "UInt16" => "ushort",
            "Int32" => "int",
            "UInt32" => "uint",
            "Int64" => "long",
            "UInt64" => "ulong",
            "Single" => "float",
            "Double" => "double",
            "Decimal" => "decimal",
            "Char" => "char",
            "String" => "string",
            _ => typeName,
        };
    }

    private static void AppendSchemaRecordSourceBody(StringBuilder builder,SchemaRecordDefinition record,string indent) {
        if(record.Properties.Length==0) {
            return;
        }

        var unmanagedProperties = record.Properties.Where(static property => property.IsUnmanaged).ToImmutableArray();
        var managedProperties = record.Properties.Where(static property => !property.IsUnmanaged).ToImmutableArray();

        var innerBuilder = new StringBuilder();
        innerBuilder.AppendLine($"// Generated: completed schema record for {record.FullTypeName}. unmanaged data lives in _u and managed data lives in _m.");
        innerBuilder.AppendLine($"public partial struct {record.Name}:global::MemoryPack.IMemoryPackable<{record.FullTypeName}> {{");
        innerBuilder.AppendLine($"    public {record.Name}({string.Join(", ",record.Properties.Select(static property => $"{property.TypeName} {GetParameterName(property.Name)}"))}) {{");
        if(unmanagedProperties.Length>0) {
            innerBuilder.AppendLine($"        this._u=new({string.Join(", ",unmanagedProperties.Select(static property => GetParameterName(property.Name)))});");
        }
        else {
            innerBuilder.AppendLine("        this._u=new();");
        }
        if(managedProperties.Length>0) {
            innerBuilder.AppendLine($"        this._m=new({string.Join(", ",managedProperties.Select(static property => GetParameterName(property.Name)))});");
        }
        else {
            innerBuilder.AppendLine("        this._m=new();");
        }
        innerBuilder.AppendLine("    }");
        innerBuilder.AppendLine();
        AppendSchemaUnmanagedRecordSource(innerBuilder,unmanagedProperties);
        innerBuilder.AppendLine("    private _U _u;");
        innerBuilder.AppendLine();
        AppendSchemaManagedRecordSource(innerBuilder,managedProperties);
        innerBuilder.AppendLine("    private _M _m;");
        innerBuilder.AppendLine();
        foreach(var property in record.Properties) {
            innerBuilder.AppendLine($"    public {property.TypeName} {property.Name} => this.{(property.IsUnmanaged ? "_u" : "_m")}.{property.Name};");
        }
        innerBuilder.AppendLine();
        innerBuilder.AppendLine($"    static {record.Name}() {{");
        innerBuilder.AppendLine($"        global::MemoryPack.MemoryPackFormatterProvider.Register<{record.FullTypeName}>();");
        innerBuilder.AppendLine("    }");
        innerBuilder.AppendLine();
        innerBuilder.AppendLine("    static void global::MemoryPack.IMemoryPackFormatterRegister.RegisterFormatter() {");
        innerBuilder.AppendLine($"        if(!global::MemoryPack.MemoryPackFormatterProvider.IsRegistered<{record.FullTypeName}>()) {{");
        innerBuilder.AppendLine($"            global::MemoryPack.MemoryPackFormatterProvider.Register(new global::MemoryPack.Formatters.MemoryPackableFormatter<{record.FullTypeName}>());");
        innerBuilder.AppendLine("        }");
        innerBuilder.AppendLine($"        if(!global::MemoryPack.MemoryPackFormatterProvider.IsRegistered<{record.FullTypeName}[]>()) {{");
        innerBuilder.AppendLine($"            global::MemoryPack.MemoryPackFormatterProvider.Register(new global::MemoryPack.Formatters.ArrayFormatter<{record.FullTypeName}>());");
        innerBuilder.AppendLine("        }");
        innerBuilder.AppendLine("    }");
        innerBuilder.AppendLine();
        innerBuilder.AppendLine($"    static void global::MemoryPack.IMemoryPackable<{record.FullTypeName}>.Serialize<TBufferWriter>(ref global::MemoryPack.MemoryPackWriter<TBufferWriter> writer,scoped ref {record.FullTypeName} value) {{");
        innerBuilder.AppendLine("        writer.WriteUnmanaged(value._u);");
        innerBuilder.AppendLine("        writer.WritePackable(value._m);");
        innerBuilder.AppendLine("    }");
        innerBuilder.AppendLine();
        innerBuilder.AppendLine($"    static void global::MemoryPack.IMemoryPackable<{record.FullTypeName}>.Deserialize(ref global::MemoryPack.MemoryPackReader reader,scoped ref {record.FullTypeName} value) {{");
        innerBuilder.AppendLine("        reader.ReadUnmanaged(out value._u);");
        innerBuilder.AppendLine("        reader.ReadPackable(ref value._m);");
        innerBuilder.AppendLine("    }");
        innerBuilder.AppendLine("}");
        AppendIndented(builder,innerBuilder.ToString(),indent);
        builder.AppendLine();
    }

    // [Optimize] class と [Optimize] virtual method の組だけを optimized subclass へ出力する。
    // 制約違反は ReportOptimizeDiagnostics で MemoryPack 風の generator diagnostic として報告する。
    private static string BuildOptimizedContainerSource(Compilation compilation,ImmutableArray<TypeTarget> typeTargets) {
        var optimizedClasses = GetDistinctTypes(typeTargets.Select(static target => target.Symbol))
            .Where(static symbol => symbol.TypeKind==TypeKind.Class)
            .Where(HasOptimizeAttribute)
            .Where(static symbol => !symbol.IsSealed)
            .OrderBy(static symbol => GetNamespaceText(symbol.ContainingNamespace),StringComparer.Ordinal)
            .ThenBy(static symbol => symbol.Name,StringComparer.Ordinal)
            .ToArray();
        if(optimizedClasses.Length==0) {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("#nullable enable");
        foreach(var namespaceGroup in optimizedClasses.GroupBy(static symbol => GetNamespaceText(symbol.ContainingNamespace),StringComparer.Ordinal)) {
            var namespaceName = namespaceGroup.Key;
            var hasNamespace = !string.IsNullOrEmpty(namespaceName);
            var rootIndent = hasNamespace ? "    " : "";
            var memberIndent = hasNamespace ? "        " : "    ";
            if(hasNamespace) {
                builder.AppendLine($"namespace {namespaceName} {{");
            }
            foreach(var container in namespaceGroup) {
                var optimizedMembers = BuildOptimizedOverrideMemberLines(compilation,container);
                if(optimizedMembers.Count==0) {
                    continue;
                }
                var optimizedTypeName = $"Optimized{container.Name}";
                var forwardingConstructors = BuildForwardingConstructorLines(container,optimizedTypeName);
                builder.AppendLine($"{rootIndent}// Generated: [Optimize] virtual methods are optimized through normal override dispatch.");
                builder.AppendLine($"{rootIndent}{GetGeneratedTypeAccessibilityText(container)} class {optimizedTypeName}:{container.ToDisplayString(FullyQualifiedFormat)} {{");
                foreach(var constructorLine in forwardingConstructors) {
                    AppendIndented(builder,constructorLine,memberIndent);
                }
                if(forwardingConstructors.Count>0) {
                    builder.AppendLine();
                }
                foreach(var memberLine in optimizedMembers) {
                    AppendIndented(builder,memberLine,memberIndent);
                }
                builder.AppendLine($"{rootIndent}}}");
                builder.AppendLine();
            }
            if(hasNamespace) {
                builder.AppendLine("}");
            }
        }
        return builder.ToString();
    }

    // virtual method は interceptor ではなく派生クラスの override に変換する。
    // reflection で元 method を呼ぶテストは base 実装、通常の virtual dispatch は Optimized{Container} 実装を通る。
    private static List<string> BuildOptimizedOverrideMemberLines(Compilation compilation,INamedTypeSymbol container) {
        var メンバー行 = new List<string>();
        foreach(var method in container.GetMembers().OfType<IMethodSymbol>()) {
            if(!HasOptimizeAttribute(method)) {
                continue;
            }
            if(!IsOptimizedOverrideCandidate(method)) {
                continue;
            }
            var translatedBody = メソッド翻訳試行(compilation,method);
            if(translatedBody is null) {
                continue;
            }

            var parameterList = BuildOptimizedParameterDeclarationList(method.Parameters);
            var typeParameterList = BuildTypeParameterList(method.TypeParameters);
            var returnType = BuildGeneratedTypeName(method.ReturnType);
            var accessibility = GetMemberAccessibilityText(method.DeclaredAccessibility);
            var body = translatedBody.Value.Body.Replace("self.","this.");
            var methodDeclaration = $"{accessibility} override {returnType} {method.Name}{typeParameterList}({parameterList})";
            メンバー行.Add(BuildTranslatedMemberSource(methodDeclaration,body,translatedBody.Value.IsExpressionBody));
        }
        return メンバー行;
    }

    // 翻訳済み body は `{` から始まる複数行文字列なので、member 宣言の下に相対インデントを保って結合する。
    // ここで整形しておくと interceptor と optimized override の生成コードが同じ見た目になる。
    private static string BuildTranslatedMemberSource(string methodDeclaration,string body,bool isExpressionBody) {
        if(isExpressionBody) {
            return $"{methodDeclaration} => {body};";
        }

        var 本文行 = body.Replace("\r\n","\n").Split('\n');
        if(本文行.Length==0) {
            return $"{methodDeclaration} {{ }}";
        }

        var 結果 = new StringBuilder();
        結果.Append(methodDeclaration);
        結果.Append(' ');
        結果.AppendLine(本文行[0].TrimStart());
        for(var 行番号 = 1; 行番号<本文行.Length; 行番号++) {
            結果.AppendLine(本文行[行番号]);
        }
        return 結果.ToString().TrimEnd('\r','\n');
    }

    private static bool IsOptimizedOverrideCandidate(IMethodSymbol method) {
        return IsOptimizeMethodSemanticallyValid(method);
    }

    // base container に明示 constructor がある場合でも Optimized{Container} を作れるように転送する。
    private static List<string> BuildForwardingConstructorLines(INamedTypeSymbol container,string optimizedTypeName) {
        var constructorLines = new List<string>();
        foreach(var constructor in container.InstanceConstructors.Where(static constructor => !constructor.IsImplicitlyDeclared&&constructor.DeclaredAccessibility!=Accessibility.Private)) {
            var parameterList = BuildParameterDeclarationList(constructor.Parameters);
            var argumentList = BuildArgumentExpressionList(constructor.Parameters);
            constructorLines.Add($"{GetMemberAccessibilityText(constructor.DeclaredAccessibility)} {optimizedTypeName}({parameterList}) : base({argumentList}) {{ }}");
        }
        return constructorLines;
    }

    private static string BuildArgumentExpressionList(ImmutableArray<IParameterSymbol> parameters) {
        return string.Join(", ",parameters.Select(BuildArgumentExpression));
    }

    private static string BuildArgumentExpression(IParameterSymbol parameter) {
        return parameter.RefKind switch {
            RefKind.Ref => $"ref {parameter.Name}",
            RefKind.Out => $"out {parameter.Name}",
            RefKind.In => $"in {parameter.Name}",
            _ => parameter.Name,
        };
    }

    private static string GetGeneratedTypeAccessibilityText(INamedTypeSymbol symbol) {
        return symbol.DeclaredAccessibility==Accessibility.Public ? "public" : "internal";
    }

    private static string GetMemberAccessibilityText(Accessibility accessibility) {
        return accessibility switch {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            _ => "private",
        };
    }

    private readonly struct TranslatedMethodBody {
        public TranslatedMethodBody(string body,bool isExpressionBody) {
            Body=body;
            IsExpressionBody=isExpressionBody;
        }

        public string Body { get; }
        public bool IsExpressionBody { get; }
    }

    private static string BuildTypeParameterList(ImmutableArray<ITypeParameterSymbol> typeParameters) {
        if(typeParameters.Length==0) {
            return string.Empty;
        }
        return $"<{string.Join(", ",typeParameters.Select(static typeParameter => typeParameter.Name))}>";
    }

    private static string BuildTypeParameterConstraintList(ImmutableArray<ITypeParameterSymbol> typeParameters) {
        var constraints = new List<string>();
        foreach(var typeParameter in typeParameters) {
            var parts = new List<string>();
            if(typeParameter.HasUnmanagedTypeConstraint) {
                parts.Add("unmanaged");
            }
            else if(typeParameter.HasValueTypeConstraint) {
                parts.Add("struct");
            }
            else if(typeParameter.HasReferenceTypeConstraint) {
                parts.Add("class");
            }
            else if(typeParameter.HasNotNullConstraint) {
                parts.Add("notnull");
            }
            foreach(var constraintType in typeParameter.ConstraintTypes) {
                parts.Add(constraintType.ToDisplayString(FullyQualifiedFormat));
            }
            if(typeParameter.HasConstructorConstraint) {
                parts.Add("new()");
            }
            if(parts.Count>0) {
                constraints.Add($"where {typeParameter.Name} : {string.Join(", ",parts)}");
            }
        }
        return constraints.Count==0 ? string.Empty : " "+string.Join(" ",constraints);
    }

    private static string BuildParameterDeclarationList(ImmutableArray<IParameterSymbol> parameters) {
        return string.Join(", ",parameters.Select(BuildParameterDeclaration));
    }

    private static string BuildOptimizedParameterDeclarationList(ImmutableArray<IParameterSymbol> parameters) {
        return string.Join(", ",parameters.Select(BuildOptimizedParameterDeclaration));
    }

    private static string BuildOptimizedParameterDeclaration(IParameterSymbol parameter) {
        var builder = new StringBuilder();
        if(parameter.IsParams) {
            builder.Append("params ");
        }
        switch(parameter.RefKind) {
            case RefKind.Ref:
                builder.Append("ref ");
                break;
            case RefKind.Out:
                builder.Append("out ");
                break;
            case RefKind.In:
                builder.Append("in ");
                break;
        }
        builder.Append(BuildGeneratedTypeName(parameter.Type));
        builder.Append(' ');
        builder.Append(parameter.Name);
        return builder.ToString();
    }

    private static string BuildParameterDeclaration(IParameterSymbol parameter) {
        var builder = new StringBuilder();
        if(parameter.IsParams) {
            builder.Append("params ");
        }
        switch(parameter.RefKind) {
            case RefKind.Ref:
                builder.Append("ref ");
                break;
            case RefKind.Out:
                builder.Append("out ");
                break;
            case RefKind.In:
                builder.Append("in ");
                break;
        }
        builder.Append(parameter.Type.ToDisplayString(FullyQualifiedFormat));
        builder.Append(' ');
        builder.Append(parameter.Name);
        return builder.ToString();
    }

    private static string BuildGeneratedTypeName(ITypeSymbol type) {
        if(type.SpecialType==SpecialType.System_Void) {
            return "void";
        }
        return type.ToDisplayString(FullyQualifiedFormat);
    }

    /// <summary>
    /// メソッドシンボルから構文を取得し、本体の形 (クエリ式・ラムダ・呼び出し文・呼び出し式) に応じて<br/>
    /// 適切な翻訳を試みる最上位入口。Where pushdown 最適化を掴けてから翻訳する。
    /// </summary>
    /// <example><code>
    /// // 変換前 (ユーザーが定義したメソッド):
    /// IEnumerable&lt;int&gt; GetPositive(IEnumerable&lt;int&gt; source)
    ///     => source.Where(x => x > 0).Select(x => x * 2);
    ///
    /// // 翻訳後 (interceptor に挙入される本体):
    /// => global::Seamless.Linq.Enumerable.Select&lt;int, int&gt;(
    ///        global::Seamless.Linq.Enumerable.Where&lt;int&gt;(source, x => x > 0),
    ///        x => x * 2)
    /// </code></example>
    private static TranslatedMethodBody? メソッド翻訳試行(Compilation compilation,IMethodSymbol method) {
        foreach(var syntaxReference in method.DeclaringSyntaxReferences) {
            if(syntaxReference.GetSyntax() is not MethodDeclarationSyntax syntax) {
                continue;
            }
            var semanticModel = compilation.GetSemanticModel(syntax.SyntaxTree);
            if(syntax.ExpressionBody?.Expression is QueryExpressionSyntax expressionQuery) {
                var translatedQuery = クエリ式翻訳試行(expressionQuery,semanticModel);
                return translatedQuery is null ? null : new TranslatedMethodBody(translatedQuery,true);
            }
            if(syntax.ExpressionBody?.Expression is LambdaExpressionSyntax expressionLambda) {
                var translatedLambda = ラムダ式翻訳試行(expressionLambda,semanticModel);
                return translatedLambda is null ? null : new TranslatedMethodBody(translatedLambda,true);
            }
            if(method.ReturnsVoid&&syntax.ExpressionBody?.Expression is InvocationExpressionSyntax expressionStatementInvocation) {
                var translatedStatement = 呼び出し引数シーケンスボディ翻訳試行(expressionStatementInvocation,semanticModel);
                if(translatedStatement is not null) {
                    return new TranslatedMethodBody(translatedStatement,false);
                }
            }
            if(syntax.ExpressionBody?.Expression is InvocationExpressionSyntax expressionInvocation) {
                return 呼び出しボディ翻訳試行(method,expressionInvocation,(InvocationExpressionSyntax)OptimizeVisitor.Optimize(expressionInvocation),semanticModel);
            }
            var expressionWrapperInvocation = TryGetSingleArgumentInvocation(syntax.ExpressionBody?.Expression);
            if(expressionWrapperInvocation is not null) {
                return 呼び出しボディ翻訳試行(method,expressionWrapperInvocation,(InvocationExpressionSyntax)OptimizeVisitor.Optimize(expressionWrapperInvocation),semanticModel);
            }
            if(syntax.Body is null||syntax.Body.Statements.Count!=1) {
                continue;
            }
            if(method.ReturnsVoid&&syntax.Body.Statements[0] is ExpressionStatementSyntax expressionStatement&&expressionStatement.Expression is InvocationExpressionSyntax statementInvocation) {
                var translatedStatement = 呼び出し引数シーケンスボディ翻訳試行(statementInvocation,semanticModel);
                if(translatedStatement is not null) {
                    return new TranslatedMethodBody(translatedStatement,false);
                }
            }
            if(syntax.Body.Statements[0] is ReturnStatementSyntax returnStatement&&returnStatement.Expression is QueryExpressionSyntax blockQuery) {
                var translatedQuery = クエリ式翻訳試行(blockQuery,semanticModel);
                return translatedQuery is null ? null : new TranslatedMethodBody(translatedQuery,true);
            }
            if(syntax.Body.Statements[0] is ReturnStatementSyntax lambdaReturn&&lambdaReturn.Expression is LambdaExpressionSyntax blockLambda) {
                var translatedLambda = ラムダ式翻訳試行(blockLambda,semanticModel);
                return translatedLambda is null ? null : new TranslatedMethodBody(translatedLambda,true);
            }
            if(syntax.Body.Statements[0] is ReturnStatementSyntax invocationReturn&&invocationReturn.Expression is InvocationExpressionSyntax blockInvocation) {
                return 呼び出しボディ翻訳試行(method,blockInvocation,(InvocationExpressionSyntax)OptimizeVisitor.Optimize(blockInvocation),semanticModel);
            }
            if(syntax.Body.Statements[0] is ReturnStatementSyntax wrapperReturn) {
                var blockWrapperInvocation = TryGetSingleArgumentInvocation(wrapperReturn.Expression);
                if(blockWrapperInvocation is not null) {
                    return 呼び出しボディ翻訳試行(method,blockWrapperInvocation,(InvocationExpressionSyntax)OptimizeVisitor.Optimize(blockWrapperInvocation),semanticModel);
                }
            }
        }
        return null;
    }

    private static InvocationExpressionSyntax? TryGetSingleArgumentInvocation(ExpressionSyntax? expression) {
        return expression switch {
            ObjectCreationExpressionSyntax objectCreation when objectCreation.ArgumentList?.Arguments.Count==1
                => objectCreation.ArgumentList.Arguments[0].Expression as InvocationExpressionSyntax,
            ImplicitObjectCreationExpressionSyntax implicitObjectCreation when implicitObjectCreation.ArgumentList.Arguments.Count==1
                => implicitObjectCreation.ArgumentList.Arguments[0].Expression as InvocationExpressionSyntax,
            _ => null,
        };
    }

    /// <summary>
    /// メソッド引数として渡される LINQ 式を事前に一時リストへ実体化し、<br/>
    /// その結果を引数として元の呼び出しに渡すブロック本体を生成する。
    /// </summary>
    /// <example><code>
    /// // 変換前:
    /// ProcessItems(source.Where(x => x > 0))
    ///
    /// // 変換後 (引数を先に実体化してから呼び出す):
    /// {
    ///     // source.Where(x => x > 0) を一時リストに実体化
    ///     var result = ...; // TemporaryResultList
    ///     ProcessItems(result);
    /// }
    /// </code></example>
    private static string? 呼び出し引数シーケンスボディ翻訳試行(InvocationExpressionSyntax invocation,SemanticModel semanticModel) {
        var materializedArguments = new Dictionary<int,string>();
        var materializationBlocks = new List<string>();
        for(var argumentIndex = 0; argumentIndex<invocation.ArgumentList.Arguments.Count; argumentIndex++) {
            var argument = invocation.ArgumentList.Arguments[argumentIndex];
            if(argument.RefKindKeyword.IsKind(SyntaxKind.None)
                &&RemoveParentheses(argument.Expression) is InvocationExpressionSyntax argumentInvocation
                &&TryCreateSequenceMaterializationPipeline(argumentInvocation,semanticModel,out var pipeline,out var resultElementType)) {
                var resultName = materializedArguments.Count==0 ? "result" : $"result{materializedArguments.Count}";
                materializedArguments.Add(argumentIndex,resultName);
                materializationBlocks.Add(new SequenceMaterializationEmitter(pipeline,BuildGeneratedTypeName(resultElementType),resultName,CanUseUnmanagedTemporaryResultList(resultElementType)).BuildStatements());
            }
        }
        if(materializedArguments.Count==0) {
            return null;
        }

        var invocationText = 呼び出し文翻訳(invocation,semanticModel,materializedArguments);
        if(invocationText is null) {
            return null;
        }

        var builder = new StringBuilder();
        builder.AppendLine("{");
        foreach(var block in materializationBlocks) {
            foreach(var line in block.Replace("\r\n","\n").Split('\n')) {
                if(line.Length==0) {
                    continue;
                }
                builder.Append("    ");
                builder.AppendLine(line);
            }
        }
        builder.Append("    ");
        builder.Append(invocationText);
        builder.AppendLine(";");
        builder.AppendLine("}");
        return builder.ToString();
    }

    /// <summary>
    /// 呼び出し式を引数リストが一部実体化済みの形式で完全修飾名式の呼び出し文字列に変換する。
    /// </summary>
    /// <example><code>
    /// // 変換前 (invocation 構文):
    /// ProcessItems(source.Where(x => x > 0))
    ///
    /// // materializedArguments[0] = "result" となっている場合の変換後:
    /// global::Example.ProcessItems(result)
    /// </code></example>
    private static string? 呼び出し文翻訳(InvocationExpressionSyntax invocation,SemanticModel semanticModel,IReadOnlyDictionary<int,string> materializedArguments) {
        var arguments = new List<string>(invocation.ArgumentList.Arguments.Count);
        for(var argumentIndex = 0; argumentIndex<invocation.ArgumentList.Arguments.Count; argumentIndex++) {
            if(materializedArguments.TryGetValue(argumentIndex,out var materializedArgument)) {
                arguments.Add(materializedArgument);
                continue;
            }
            arguments.Add(入れ子引数翻訳(invocation.ArgumentList.Arguments[argumentIndex],semanticModel));
        }

        var argumentList = string.Join(",",arguments);
        if(invocation.Expression is SimpleNameSyntax simpleName) {
            var typeArgumentListText = GetTypeArgumentListText(simpleName);
            if(TryGetInvocationMethodSymbol(invocation,semanticModel,out var methodSymbol)) {
                var definition = methodSymbol.ReducedFrom??methodSymbol;
                if(definition.IsStatic) {
                    return $"{definition.ContainingType.ToDisplayString(FullyQualifiedFormat)}.{definition.Name}{typeArgumentListText}({argumentList})";
                }
                return $"this.{definition.Name}{typeArgumentListText}({argumentList})";
            }
            return $"{simpleName.Identifier.ValueText}{typeArgumentListText}({argumentList})";
        }

        if(invocation.Expression is MemberAccessExpressionSyntax memberAccess) {
            var receiver = 入れ子式翻訳(memberAccess.Expression,semanticModel);
            var typeArgumentListText = GetTypeArgumentListText(memberAccess.Name);
            return $"{receiver}.{memberAccess.Name.Identifier.ValueText}{typeArgumentListText}({argumentList})";
        }
        return null;
    }

    // Expression<Func<...>> などの戻り値では、ラムダ本体の query / invocation だけを書き換えて
    // ラムダ自体の形とパラメータはそのまま維持する。
    /// <summary>
    /// <c>Expression&lt;Func&lt;...&gt;&gt;</c> などラムダ式の本体部分だけを翻訳し、<br/>
    /// ラムダのパラメータ宣言や形はそのまま維持して返す。
    /// </summary>
    /// <example><code>
    /// // 変換前:
    /// x => x.Items.Where(i => i > 0)
    ///
    /// // 変換後:
    /// x => global::Seamless.Linq.Enumerable.Where&lt;int&gt;(x.Items, i => i > 0)
    /// </code></example>
    private static string? ラムダ式翻訳試行(LambdaExpressionSyntax lambda,SemanticModel semanticModel) {
        if(lambda.Body is not ExpressionSyntax lambdaBody) {
            return null;
        }
        while(lambdaBody is ParenthesizedExpressionSyntax parenthesizedExpression) {
            lambdaBody=parenthesizedExpression.Expression;
        }

        string? translatedBody = lambdaBody switch {
            QueryExpressionSyntax queryExpression => クエリ式翻訳試行(queryExpression,semanticModel),
            InvocationExpressionSyntax invocationExpression => 呼び出し式翻訳試行((InvocationExpressionSyntax)OptimizeVisitor.Optimize(invocationExpression),semanticModel),
            _ => null,
        };
        if(translatedBody is null) {
            return null;
        }

        var asyncPrefix = lambda.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword) ? "async " : string.Empty;
        return lambda switch {
            SimpleLambdaExpressionSyntax simpleLambda => $"{asyncPrefix}{simpleLambda.Parameter} => {translatedBody}",
            ParenthesizedLambdaExpressionSyntax parenthesizedLambda => $"{asyncPrefix}{parenthesizedLambda.ParameterList} => {translatedBody}",
            _ => null,
        };
    }

    // invocation のうち先行評価・scalar fusion・Aggregate は block body に展開し、他は expression body のまま吐く。
    /// <summary>
    /// 呼び出し式を、シーケンス実体化・スカラー融合・Aggregate・式変換の順で試みる分岐入口。<br/>
    /// 最初に成立した変換結果を返し、いずれも不可の場合は null を返す。
    /// </summary>
    /// <example><code>
    /// // source.Where(x => x > 0).ToList()
    /// //   → シーケンス融合ループボディ翻訳試行 (ToList + Where を 1 ループに融合)
    ///
    /// // source.Where(x => x > 0).Sum(x => x)
    /// //   → スカラー融合ループボディ翻訳試行 (Sum + Where を 1 ループに融合)
    ///
    /// // source.Aggregate((a, b) => a + b)
    /// //   → 集約ボディ翻訳試行 (明示的な列挙器ループに展開)
    ///
    /// // source.OrderBy(x => x)
    /// //   → 呼び出し式翻訳試行 (Seamless.Linq へのコールスルー式)
    /// </code></example>
    private static TranslatedMethodBody? 呼び出しボディ翻訳試行(IMethodSymbol method,InvocationExpressionSyntax originalInvocation,InvocationExpressionSyntax invocation,SemanticModel semanticModel) {
        var 列挙結果Body = シーケンス融合ループボディ翻訳試行(method,originalInvocation,semanticModel);
        if(列挙結果Body is not null) {
            return new TranslatedMethodBody(列挙結果Body,false);
        }
        var スカラー結果Body = スカラー融合ループボディ翻訳試行(method,originalInvocation,invocation,semanticModel);
        if(スカラー結果Body is not null) {
            return new TranslatedMethodBody(スカラー結果Body,false);
        }
        var 集約Body = 集約ボディ翻訳試行(method,invocation,semanticModel);
        if(集約Body is not null) {
            return new TranslatedMethodBody(集約Body,false);
        }
        var 式Body = 呼び出し式翻訳試行(invocation,semanticModel);
        return 式Body is null ? null : new TranslatedMethodBody(式Body,true);
    }

    // IEnumerable<T> を返す method-chain の先行評価候補。
    // StorageList を使うかどうかは空間と時間のトレードオフなので、generator は型から自動判断しない。
    // method / invocation / semanticModel は、明示的な Managed/Unmanaged 方針が呼び出し側から渡る形に戻すときの解析入力。
    /// <summary>
    /// <c>IEnumerable&lt;T&gt;</c> を返す Select / Where チェーンを一時リストへの単一 foreach ループに融合する。<br/>
    /// Where 条件フィルタと Select 変換をループ内で順に適用し、結果を一時ストレージに追加する。
    /// </summary>
    /// <example><code>
    /// // 変換前:
    /// source.Where(x => x > 0).Select(x => x * 2)
    ///
    /// // 変換後 (生成ブロック):
    /// {
    ///     var __seamlessSource = source;
    ///     ...
    ///     foreach (var __seamlessItem in __seamlessSource)
    ///     {
    ///         if (!(x > 0)) goto __seamlessContinue;
    ///         var __seamlessValue0 = __seamlessItem * 2;
    ///         __seamlessResultList.Add(__seamlessValue0);
    ///         __seamlessContinue:;
    ///     }
    ///     var result = ...; // 一時ストレージを所有権付きで返す
    ///     return result;
    /// }
    /// </code></example>
    private static string? シーケンス融合ループボディ翻訳試行(IMethodSymbol method,InvocationExpressionSyntax invocation,SemanticModel semanticModel) {
        if(TryCreateSequenceMaterializationPipeline(invocation,semanticModel,out var pipeline,out var resultElementType)) {
            return new SequenceMaterializationEmitter(pipeline,BuildGeneratedTypeName(resultElementType),"result",CanUseUnmanagedTemporaryResultList(resultElementType),TryGetSequenceMaterializationWrapperReturnType(method.ReturnType)).BuildReturnBody();
        }
        return 選択融合ループボディ翻訳試行(method,invocation,semanticModel);
    }

    private static string? TryGetSequenceMaterializationWrapperReturnType(ITypeSymbol returnType) {
        if(returnType is INamedTypeSymbol namedType
            &&namedType.Name=="IEnumerable"
            &&namedType.ContainingNamespace.ToDisplayString()=="Seamless.Collections.Generic") {
            return BuildGeneratedTypeName(returnType);
        }
        return null;
    }

    private static bool TryCreateSequenceMaterializationPipeline(InvocationExpressionSyntax invocation,SemanticModel semanticModel,out ScalarFusionPipeline pipeline,out ITypeSymbol resultElementType) {
        pipeline=null!;
        resultElementType=null!;
        var resultType = semanticModel.GetTypeInfo(invocation).Type;
        if(resultType is null||!TryGetEnumerableElementType(resultType,out resultElementType)) {
            return false;
        }

        var reversedOperations = new List<ScalarFusionOperation>();
        var currentInvocation = invocation;
        while(true) {
            if(!IsSequenceMaterializationLinqInvocation(currentInvocation,semanticModel)||!TryGetScalarFusionCallInfo(currentInvocation,out var call)) {
                return false;
            }
            if(!TryCreateSequenceMaterializationOperation(call,out var operation)) {
                return false;
            }
            reversedOperations.Add(operation);
            var source = RemoveParentheses(call.Source);
            if(source is InvocationExpressionSyntax sourceInvocation&&IsSequenceMaterializationOperationName(GetInvocationMethodName(sourceInvocation))) {
                currentInvocation=sourceInvocation;
                continue;
            }

            reversedOperations.Reverse();
            pipeline=new ScalarFusionPipeline(入れ子式翻訳(source,semanticModel),reversedOperations);
            return pipeline.Operations.Count>0;
        }
    }

    private static bool TryCreateSequenceMaterializationOperation(ScalarFusionCallInfo call,out ScalarFusionOperation operation) {
        operation=null!;
        if(!IsSequenceMaterializationOperationName(call.MethodName)) {
            return false;
        }
        if(call.Arguments.Length!=1||!TryCreateLambda(call.Arguments[0].Expression,out var lambda)||(lambda.Parameters.Length!=1&&lambda.Parameters.Length!=2)) {
            return false;
        }
        operation=ScalarFusionOperation.Create(call.MethodName,call.Arguments,call.TypeArguments,lambda,null);
        return true;
    }

    private static bool IsSequenceMaterializationLinqInvocation(InvocationExpressionSyntax invocation,SemanticModel semanticModel) {
        if(!TryGetInvocationMethodSymbol(invocation,semanticModel,out var methodSymbol)) {
            return false;
        }
        var definition = methodSymbol.ReducedFrom??methodSymbol;
        var containingType = definition.ContainingType.ToDisplayString();
        return containingType=="System.Linq.Enumerable"
            ||containingType=="Seamless.Linq.Enumerable"
            ||containingType=="Seamless.Linq.HashSet";
    }

    private static string GetInvocationMethodName(InvocationExpressionSyntax invocation) {
        if(invocation.Expression is MemberAccessExpressionSyntax memberAccess) {
            return memberAccess.Name is GenericNameSyntax genericName
                ? genericName.Identifier.ValueText
                : memberAccess.Name.Identifier.ValueText;
        }
        if(invocation.Expression is SimpleNameSyntax simpleName) {
            return simpleName.Identifier.ValueText;
        }
        return string.Empty;
    }

    private static bool IsSequenceMaterializationOperationName(string methodName) {
        return methodName=="Where"||methodName=="Select";
    }

    /// <summary>
    /// Select が最外側かつ source が通常 IEnumerable の場合に、<br/>
    /// 明示的な foreach + <c>List&lt;TResult&gt;</c> へ展開するブロック本体を生成する。
    /// </summary>
    /// <example><code>
    /// // 変換前:
    /// source.Select(x => x.ToString())
    ///
    /// // 変換後:
    /// {
    ///     var __selectSource = source;
    ///     Func&lt;int, string&gt; __selectSelector = x => x.ToString();
    ///     var result = new List&lt;string&gt;();
    ///     foreach (var item in __selectSource)
    ///         result.Add(Inline(item, __selectSelector));
    ///     return result;
    ///     static string Inline(int item, Func&lt;int, string&gt; selector) => selector(item);
    /// }
    /// </code></example>
    private static string? 選択融合ループボディ翻訳試行(IMethodSymbol method,InvocationExpressionSyntax invocation,SemanticModel semanticModel) {
        if(!TryGetScalarFusionCallInfo(invocation,out var selectCall)||selectCall.MethodName!="Select"||selectCall.Arguments.Length!=1) {
            return null;
        }
        if(!TryGetInvocationMethodSymbol(invocation,semanticModel,out var selectMethod)) {
            return null;
        }
        var definition = selectMethod.ReducedFrom??selectMethod;
        var containingType = definition.ContainingType.ToDisplayString();
        if(containingType!="System.Linq.Enumerable"&&containingType!="Seamless.Linq.Enumerable") {
            return null;
        }
        if(definition.Parameters.Length==0
            ||definition.Parameters[definition.Parameters.Length-1].Type is not INamedTypeSymbol selectorType
            ||selectorType.Name!="Func"
            ||selectorType.ContainingNamespace.ToDisplayString()!="System"
            ||selectorType.TypeArguments.Length!=2) {
            return null;
        }
        if(!TryGetEnumerableElementType(method.ReturnType,out var resultElementType)) {
            return null;
        }
        var sourceType = semanticModel.GetTypeInfo(selectCall.Source).Type;
        if(sourceType is null||!TryGetEnumerableElementType(sourceType,out var sourceElementType)) {
            return null;
        }
        if(ContainsAnonymousType(sourceElementType)||ContainsAnonymousType(resultElementType)) {
            return null;
        }

        var sourceElementTypeName = BuildGeneratedTypeName(sourceElementType);
        var resultElementTypeName = BuildGeneratedTypeName(resultElementType);
        var sourceExpression = 入れ子式翻訳(selectCall.Source,semanticModel);
        var selectorExpression = 入れ子式翻訳(selectCall.Arguments[0].Expression,semanticModel);

        var builder = new StringBuilder();
        builder.AppendLine("{");
        builder.AppendLine($"    var __selectSource = {sourceExpression};");
        builder.AppendLine($"    global::System.Func<{sourceElementTypeName},{resultElementTypeName}> __selectSelector = {selectorExpression};");
        builder.AppendLine("    global::System.ArgumentNullException.ThrowIfNull(__selectSource);");
        builder.AppendLine("    global::System.ArgumentNullException.ThrowIfNull(__selectSelector);");
        builder.AppendLine($"    var result = new global::System.Collections.Generic.List<{resultElementTypeName}>();");
        builder.AppendLine("    foreach(var item in __selectSource) {");
        builder.AppendLine("        result.Add(Inline(item,__selectSelector));");
        builder.AppendLine("    }");
        builder.AppendLine("    return result;");
        builder.AppendLine($"    static {resultElementTypeName} Inline({sourceElementTypeName} item,global::System.Func<{sourceElementTypeName},{resultElementTypeName}> selector) {{");
        builder.AppendLine("        return selector(item);");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static bool ContainsAnonymousType(ITypeSymbol type) {
        if(type is IArrayTypeSymbol arrayType) {
            return ContainsAnonymousType(arrayType.ElementType);
        }
        if(type is INamedTypeSymbol namedType) {
            if(namedType.IsAnonymousType) {
                return true;
            }
            if(namedType.IsTupleType) {
                return namedType.TupleElements.Any(static element => ContainsAnonymousType(element.Type));
            }
            return namedType.TypeArguments.Any(ContainsAnonymousType);
        }
        return false;
    }

    private static bool ContainsErrorType(ITypeSymbol? type) {
        if(type is null) {
            return false;
        }
        if(type.TypeKind==TypeKind.Error) {
            return true;
        }
        if(type is IArrayTypeSymbol arrayType) {
            return ContainsErrorType(arrayType.ElementType);
        }
        if(type is INamedTypeSymbol namedType) {
            if(namedType.IsTupleType&&namedType.TupleElements.Any(static element => ContainsErrorType(element.Type))) {
                return true;
            }
            return namedType.TypeArguments.Any(ContainsErrorType);
        }
        return false;
    }

    private static bool ContainsErrorTypeInExpression(ExpressionSyntax expression,SemanticModel semanticModel,System.Threading.CancellationToken cancellationToken) {
        foreach(var childExpression in expression.DescendantNodesAndSelf().OfType<ExpressionSyntax>()) {
            var typeInfo = semanticModel.GetTypeInfo(childExpression,cancellationToken);
            if(ContainsErrorType(typeInfo.Type)||ContainsErrorType(typeInfo.ConvertedType)) {
                return true;
            }
            if(semanticModel.GetSymbolInfo(childExpression,cancellationToken).Symbol is IMethodSymbol method
                &&(ContainsErrorType(method.ReturnType)
                    ||method.TypeArguments.Any(ContainsErrorType)
                    ||method.Parameters.Any(static parameter => ContainsErrorType(parameter.Type)))) {
                return true;
            }
        }
        return false;
    }

    private static bool CanUseUnmanagedTemporaryResultList(ITypeSymbol type) {
        if(type is ITypeParameterSymbol typeParameter) {
            return typeParameter.HasUnmanagedTypeConstraint;
        }
        if(!type.IsUnmanagedType) {
            return false;
        }
        return !ContainsUnconstrainedTypeParameter(type);
    }

    private static bool ContainsUnconstrainedTypeParameter(ITypeSymbol type) {
        if(type is ITypeParameterSymbol typeParameter) {
            return !typeParameter.HasUnmanagedTypeConstraint;
        }
        if(type is IArrayTypeSymbol arrayType) {
            return ContainsUnconstrainedTypeParameter(arrayType.ElementType);
        }
        if(type is INamedTypeSymbol namedType) {
            foreach(var typeArgument in namedType.TypeArguments) {
                if(ContainsUnconstrainedTypeParameter(typeArgument)) {
                    return true;
                }
            }
            if(namedType.IsTupleType) {
                foreach(var element in namedType.TupleElements) {
                    if(ContainsUnconstrainedTypeParameter(element.Type)) {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    // Sum / Average 終端の method-chain を、遅延中間操作を挟まず 1 つの列挙ループへ落とす。
    /// <summary>
    /// Sum / Average を終端とするメソッドチェーンを、中間 Where / Select を挟んだまま<br/>
    /// 単一の foreach ループへ融合するブロック本体を生成する。
    /// </summary>
    /// <example><code>
    /// // 変換前:
    /// source.Where(x => x > 0).Select(x => (double)x).Sum()
    ///
    /// // 変換後 (ScalarFusionEmitter が生成):
    /// {
    ///     var __seamlessSource = source;
    ///     double __seamlessSum = default;
    ///     foreach (var __seamlessItem in __seamlessSource)
    ///     {
    ///         if (!(x > 0)) goto __seamlessContinue0;
    ///         var __seamlessValue0 = (double)__seamlessItem;
    ///         __seamlessSum += __seamlessValue0;
    ///         __seamlessContinue0:;
    ///     }
    ///     return __seamlessSum;
    /// }
    /// </code></example>
    private static string? スカラー融合ループボディ翻訳試行(IMethodSymbol method,InvocationExpressionSyntax originalInvocation,InvocationExpressionSyntax invocation,SemanticModel semanticModel) {
        if(!TryCreateScalarFusionPipeline(invocation,semanticModel,out var pipeline)) {
            return null;
        }
        if(semanticModel.GetSymbolInfo(originalInvocation).Symbol is not IMethodSymbol terminalMethodSymbol) {
            return null;
        }
        if(!TryCreateScalarTerminalInfo(method.ReturnType,terminalMethodSymbol,out var terminalInfo)) {
            return null;
        }
        return new ScalarFusionEmitter(pipeline,terminalInfo).Build();
    }

    private static bool TryCreateScalarFusionPipeline(InvocationExpressionSyntax invocation,SemanticModel semanticModel,out ScalarFusionPipeline pipeline) {
        pipeline=null!;
        if(!TryGetScalarFusionCallInfo(invocation,out var terminalCall)) {
            return false;
        }
        if(terminalCall.MethodName!="Sum"&&terminalCall.MethodName!="Average") {
            return false;
        }
        if(terminalCall.Arguments.Length>1) {
            return false;
        }

        var reversedOperations = new List<ScalarFusionOperation>();
        if(terminalCall.Arguments.Length==1) {
            if(!TryCreateLambda(terminalCall.Arguments[0].Expression,out var selector)||selector.Parameters.Length!=1) {
                return false;
            }
            reversedOperations.Add(ScalarFusionOperation.Create("Select",terminalCall.Arguments,terminalCall.TypeArguments,selector,null));
        }

        var source = RemoveParentheses(terminalCall.Source);
        while(source is InvocationExpressionSyntax sourceInvocation&&TryGetScalarFusionCallInfo(sourceInvocation,out var sourceCall)) {
            if(!IsScalarFusionIntermediate(sourceCall.MethodName)) {
                break;
            }
            if(!TryCreateScalarFusionOperation(sourceCall,out var operation)) {
                return false;
            }
            reversedOperations.Add(operation);
            source=RemoveParentheses(sourceCall.Source);
        }

        reversedOperations.Reverse();
        pipeline=new ScalarFusionPipeline(入れ子式翻訳(source,semanticModel),reversedOperations);
        return true;
    }

    private static bool TryCreateScalarFusionOperation(ScalarFusionCallInfo call,out ScalarFusionOperation operation) {
        operation=null!;
        switch(call.MethodName) {
            case "Where":
            case "Select":
            case "TakeWhile":
            case "SkipWhile":
                if(call.Arguments.Length!=1||!TryCreateLambda(call.Arguments[0].Expression,out var lambda)||(lambda.Parameters.Length!=1&&lambda.Parameters.Length!=2)) {
                    return false;
                }
                operation=ScalarFusionOperation.Create(call.MethodName,call.Arguments,call.TypeArguments,lambda,null);
                return true;

            case "Cast":
            case "OfType":
                if(call.Arguments.Length!=0||call.TypeArguments.Length!=1) {
                    return false;
                }
                operation=ScalarFusionOperation.Create(call.MethodName,call.Arguments,call.TypeArguments,null,null);
                return true;

            case "Take":
            case "Skip":
                if(call.Arguments.Length!=1) {
                    return false;
                }
                operation=ScalarFusionOperation.Create(call.MethodName,call.Arguments,call.TypeArguments,null,null);
                return true;

            case "Distinct":
                if(call.Arguments.Length!=0) {
                    return false;
                }
                operation=ScalarFusionOperation.Create(call.MethodName,call.Arguments,call.TypeArguments,null,null);
                return true;

            case "SelectMany":
                if(call.Arguments.Length!=1&&call.Arguments.Length!=2) {
                    return false;
                }
                if(!TryCreateLambda(call.Arguments[0].Expression,out var collectionSelector)||(collectionSelector.Parameters.Length!=1&&collectionSelector.Parameters.Length!=2)) {
                    return false;
                }
                FusionLambda? resultSelector = null;
                if(call.Arguments.Length==2&&(!TryCreateLambda(call.Arguments[1].Expression,out resultSelector)||resultSelector.Parameters.Length!=2)) {
                    return false;
                }
                operation=ScalarFusionOperation.Create(call.MethodName,call.Arguments,call.TypeArguments,collectionSelector,resultSelector);
                return true;

            default:
                return false;
        }
    }

    private static bool IsScalarFusionIntermediate(string methodName) {
        return methodName=="Where"
            ||methodName=="Select"
            ||methodName=="Cast"
            ||methodName=="OfType"
            ||methodName=="Take"
            ||methodName=="TakeWhile"
            ||methodName=="Skip"
            ||methodName=="SkipWhile"
            ||methodName=="Distinct"
            ||methodName=="SelectMany";
    }

    private static bool TryGetScalarFusionCallInfo(InvocationExpressionSyntax invocation,out ScalarFusionCallInfo call) {
        call=default;
        if(invocation.Expression is not MemberAccessExpressionSyntax memberAccess) {
            return false;
        }

        var methodName = memberAccess.Name is GenericNameSyntax genericName
            ? genericName.Identifier.ValueText
            : memberAccess.Name.Identifier.ValueText;
        if(string.IsNullOrWhiteSpace(methodName)) {
            return false;
        }
        var typeArguments = memberAccess.Name is GenericNameSyntax generic
            ? generic.TypeArgumentList.Arguments.Select(static argument => argument.ToString()).ToArray()
            : Array.Empty<string>();
        var receiverExpression = NormalizeExpression(RemoveParentheses(memberAccess.Expression));
        var isStaticLinqCall = IsStaticEnumerableReceiver(receiverExpression)||IsStaticHashSetReceiver(receiverExpression);
        if(isStaticLinqCall) {
            if(invocation.ArgumentList.Arguments.Count==0) {
                return false;
            }
            call=new ScalarFusionCallInfo(methodName,RemoveParentheses(invocation.ArgumentList.Arguments[0].Expression),invocation.ArgumentList.Arguments.Skip(1).ToArray(),typeArguments);
            return true;
        }

        call=new ScalarFusionCallInfo(methodName,RemoveParentheses(memberAccess.Expression),invocation.ArgumentList.Arguments.ToArray(),typeArguments);
        return true;
    }

    private static bool TryCreateLambda(ExpressionSyntax expression,out FusionLambda lambda) {
        lambda=null!;
        if(expression is SimpleLambdaExpressionSyntax simpleLambda&&simpleLambda.Body is ExpressionSyntax simpleBody) {
            lambda=new FusionLambda(new[] { simpleLambda.Parameter.Identifier.ValueText },simpleBody);
            return !string.IsNullOrWhiteSpace(lambda.Parameters[0]);
        }
        if(expression is ParenthesizedLambdaExpressionSyntax parenthesizedLambda&&parenthesizedLambda.Body is ExpressionSyntax parenthesizedBody) {
            var parameters = parenthesizedLambda.ParameterList.Parameters.Select(static parameter => parameter.Identifier.ValueText).ToArray();
            if(parameters.Length==0||parameters.Any(static parameter => string.IsNullOrWhiteSpace(parameter))) {
                return false;
            }
            lambda=new FusionLambda(parameters,parenthesizedBody);
            return true;
        }
        return false;
    }

    private static bool TryCreateScalarTerminalInfo(ITypeSymbol methodReturnType,IMethodSymbol terminalMethodSymbol,out ScalarTerminalInfo terminalInfo) {
        terminalInfo=default;
        var terminalMethod = terminalMethodSymbol.ReducedFrom??terminalMethodSymbol;
        var methodName = terminalMethod.Name;
        if(methodName!="Sum"&&methodName!="Average") {
            return false;
        }
        if(!TryGetTerminalValueType(terminalMethod,out var terminalValueType)) {
            return false;
        }

        var returnTypeIsNullable = TryGetNullableUnderlyingType(methodReturnType,out var underlyingReturnType);
        var terminalValueIsNullable = TryGetNullableUnderlyingType(terminalValueType,out var underlyingTerminalValueType);
        var effectiveReturnType = returnTypeIsNullable ? underlyingReturnType : methodReturnType;
        var effectiveTerminalValueType = terminalValueIsNullable ? underlyingTerminalValueType : terminalValueType;
        var accumulatorTypeName = methodName=="Average"
            ? GetAverageAccumulatorTypeName(effectiveTerminalValueType,effectiveReturnType)
            : effectiveReturnType.ToDisplayString(FullyQualifiedFormat);
        terminalInfo=new ScalarTerminalInfo(
            methodName,
            methodReturnType.ToDisplayString(FullyQualifiedFormat),
            accumulatorTypeName,
            returnTypeIsNullable,
            terminalValueIsNullable,
            ShouldUseCheckedAccumulator(effectiveTerminalValueType,accumulatorTypeName),
            methodName=="Average"&&string.Equals(accumulatorTypeName,"long",StringComparison.Ordinal));
        return true;
    }

    private static bool TryGetTerminalValueType(IMethodSymbol terminalMethod,out ITypeSymbol valueType) {
        valueType=null!;
        if(terminalMethod.Parameters.Length==0) {
            return false;
        }
        if(terminalMethod.Parameters.Length>=2
            &&terminalMethod.Parameters[terminalMethod.Parameters.Length-1].Type is INamedTypeSymbol selectorType
            &&selectorType.Name=="Func"
            &&selectorType.ContainingNamespace.ToDisplayString()=="System"
            &&selectorType.TypeArguments.Length>=2) {
            valueType=selectorType.TypeArguments[selectorType.TypeArguments.Length-1];
            return true;
        }
        return TryGetEnumerableElementType(terminalMethod.Parameters[0].Type,out valueType);
    }

    private static bool TryGetEnumerableElementType(ITypeSymbol type,out ITypeSymbol elementType) {
        if(type is INamedTypeSymbol namedType
            &&namedType.Name=="IEnumerable"
            &&namedType.ContainingNamespace.ToDisplayString()=="System.Collections.Generic"
            &&namedType.TypeArguments.Length==1) {
            elementType=namedType.TypeArguments[0];
            return true;
        }
        foreach(var interfaceType in type.AllInterfaces) {
            if(interfaceType.Name=="IEnumerable"
                &&interfaceType.ContainingNamespace.ToDisplayString()=="System.Collections.Generic"
                &&interfaceType.TypeArguments.Length==1) {
                elementType=interfaceType.TypeArguments[0];
                return true;
            }
        }
        elementType=null!;
        return false;
    }

    private static bool TryGetNullableUnderlyingType(ITypeSymbol type,out ITypeSymbol underlyingType) {
        if(type is INamedTypeSymbol namedType&&namedType.OriginalDefinition.SpecialType==SpecialType.System_Nullable_T&&namedType.TypeArguments.Length==1) {
            underlyingType=namedType.TypeArguments[0];
            return true;
        }
        underlyingType=null!;
        return false;
    }

    private static string GetAverageAccumulatorTypeName(ITypeSymbol terminalValueType,ITypeSymbol returnType) {
        return terminalValueType.SpecialType switch {
            SpecialType.System_Int32 => "long",
            SpecialType.System_Int64 => "decimal",
            SpecialType.System_Single => "double",
            _ => returnType.ToDisplayString(FullyQualifiedFormat),
        };
    }

    private static bool ShouldUseCheckedAccumulator(ITypeSymbol terminalValueType,string accumulatorTypeName) {
        return terminalValueType.SpecialType==SpecialType.System_Int32
            ||terminalValueType.SpecialType==SpecialType.System_Int64
            ||string.Equals(accumulatorTypeName,"decimal",StringComparison.Ordinal)
            ||string.Equals(accumulatorTypeName,"global::System.Decimal",StringComparison.Ordinal);
    }

    private sealed class SequenceMaterializationEmitter {
        private readonly ScalarFusionPipeline pipeline;
        private readonly string resultElementTypeName;
        private readonly string resultName;
        private readonly bool useUnmanagedStorage;
        private readonly string? returnWrapperTypeName;
        private readonly string suffix;
        private readonly List<string> lines = new();
        private int indent;
        private int valueIndex;

        public SequenceMaterializationEmitter(ScalarFusionPipeline pipeline,string resultElementTypeName,string resultName,bool useUnmanagedStorage,string? returnWrapperTypeName = null) {
            this.pipeline=pipeline;
            this.resultElementTypeName=resultElementTypeName;
            this.resultName=resultName;
            this.useUnmanagedStorage=useUnmanagedStorage;
            this.returnWrapperTypeName=returnWrapperTypeName;
            this.suffix=resultName=="result" ? string.Empty : resultName.Substring("result".Length);
        }

        public string BuildReturnBody() {
            this.Add("{");
            this.indent++;
            this.EmitMaterialization();
            if(this.returnWrapperTypeName is null) {
                this.Add($"return {this.resultName};");
            }
            else {
                this.Add($"return new {this.returnWrapperTypeName}({this.resultName});");
            }
            this.indent--;
            this.Add("}");
            return string.Join("\n",this.lines);
        }

        public string BuildStatements() {
            this.EmitMaterialization();
            return string.Join("\n",this.lines);
        }

        private void EmitMaterialization() {
            var sourceName = this.Name("__seamlessSource");
            var itemName = this.Name("__seamlessItem");
            var continueLabel = this.Name("__seamlessContinue");
            var resultPathName = this.Name("__seamlessResultPath");
            var resultListName = this.Name("__seamlessResultList");
            this.Add($"var {sourceName} = {this.pipeline.SourceExpression};");
            this.Add($"global::System.ArgumentNullException.ThrowIfNull({sourceName});");
            this.DeclareOperationState();
            this.Add($"var {resultPathName} = global::Seamless.Linq.Enumerable.CreateTemporaryResultListPath(\"intercept-chain-{this.StorageKindName()}\");");
            this.Add($"var {resultListName} = {this.CreateTemporaryResultListExpression(resultPathName)};");
            this.Add("try {");
            this.indent++;
            this.Add($"foreach(var {itemName} in {sourceName}) {{");
            this.indent++;
            this.EmitOperations(0,itemName,continueLabel,resultListName);
            if(this.UsesContinueLabel()) {
                this.Add($"{continueLabel}:;");
            }
            this.indent--;
            this.Add("}");
            this.indent--;
            this.Add("}");
            this.Add("catch {");
            this.indent++;
            this.Add($"{resultListName}.Dispose();");
            this.Add($"global::Seamless.Linq.Enumerable.DeleteTemporaryResultListPath({resultPathName});");
            this.Add("throw;");
            this.indent--;
            this.Add("}");
            this.Add($"var {this.resultName} = {this.OwnTemporaryResultListExpression(resultListName,resultPathName)};");
        }

        private void DeclareOperationState() {
            for(var index = 0; index<this.pipeline.Operations.Count; index++) {
                var operation = this.pipeline.Operations[index];
                if(operation.Lambda is not null&&operation.Lambda.Parameters.Length==2) {
                    this.Add($"var {this.IndexName(index)} = -1;");
                }
            }
        }

        private bool UsesContinueLabel() {
            foreach(var operation in this.pipeline.Operations) {
                if(operation.MethodName=="Where") {
                    return true;
                }
            }
            return false;
        }

        private void EmitOperations(int operationIndex,string valueExpression,string continueLabel,string resultListName) {
            if(operationIndex>=this.pipeline.Operations.Count) {
                this.Add($"{resultListName}.Add({valueExpression});");
                return;
            }

            var operation = this.pipeline.Operations[operationIndex];
            switch(operation.MethodName) {
                case "Where":
                    this.EmitIndexedLambdaAdvance(operationIndex,operation);
                    this.Add($"if(!({this.InlineLambda(operation.Lambda!,valueExpression,this.GetIndexArgument(operationIndex,operation))})) {{");
                    this.indent++;
                    this.Add($"goto {continueLabel};");
                    this.indent--;
                    this.Add("}");
                    this.EmitOperations(operationIndex+1,valueExpression,continueLabel,resultListName);
                    break;

                case "Select":
                    this.EmitIndexedLambdaAdvance(operationIndex,operation);
                    var selectedValue = this.NextValueName();
                    this.Add($"var {selectedValue} = {this.InlineLambda(operation.Lambda!,valueExpression,this.GetIndexArgument(operationIndex,operation))};");
                    this.EmitOperations(operationIndex+1,selectedValue,continueLabel,resultListName);
                    break;
            }
        }

        private string StorageKindName() {
            if(this.useUnmanagedStorage) {
                return "unmanaged";
            }
            return "managed";
        }

        private string CreateTemporaryResultListExpression(string pathName) {
            if(this.useUnmanagedStorage) {
                return $"global::Seamless.Linq.UnmanagedEnumerable.CreateTemporaryResultList<{this.resultElementTypeName}>({pathName})";
            }
            return $"global::Seamless.Linq.ManagedEnumerable.CreateTemporaryResultList<{this.resultElementTypeName}>({pathName})";
        }

        private string OwnTemporaryResultListExpression(string resultListName,string pathName) {
            if(this.useUnmanagedStorage) {
                return $"global::Seamless.Linq.UnmanagedEnumerable.OwnTemporaryResultList({resultListName},{pathName})";
            }
            return $"global::Seamless.Linq.ManagedEnumerable.OwnTemporaryResultList({resultListName},{pathName})";
        }

        private void EmitIndexedLambdaAdvance(int operationIndex,ScalarFusionOperation operation) {
            if(operation.Lambda is null||operation.Lambda.Parameters.Length!=2) {
                return;
            }
            this.Add("checked {");
            this.indent++;
            this.Add($"{this.IndexName(operationIndex)}++;");
            this.indent--;
            this.Add("}");
        }

        private string? GetIndexArgument(int operationIndex,ScalarFusionOperation operation) {
            return operation.Lambda is not null&&operation.Lambda.Parameters.Length==2
                ? this.IndexName(operationIndex)
                : null;
        }

        private string InlineLambda(FusionLambda lambda,string firstArgument,string? secondArgument) {
            var replacements = new Dictionary<string,ExpressionSyntax>(StringComparer.Ordinal) {
                [lambda.Parameters[0]]=SyntaxFactory.ParseExpression(firstArgument),
            };
            if(secondArgument is not null&&lambda.Parameters.Length>1) {
                replacements[lambda.Parameters[1]]=SyntaxFactory.ParseExpression(secondArgument);
            }
            var rewritten = new LambdaParameterInlineRewriter(replacements).Visit(lambda.Body) as ExpressionSyntax??lambda.Body;
            return NormalizeExpression(rewritten);
        }

        private string IndexName(int operationIndex) {
            return this.Name($"__seamlessIndex{operationIndex}");
        }

        private string NextValueName() {
            return this.Name($"__seamlessValue{this.valueIndex++}");
        }

        private string Name(string baseName) {
            return string.IsNullOrEmpty(this.suffix) ? baseName : $"{baseName}{this.suffix}";
        }

        private void Add(string line) {
            this.lines.Add(new string(' ',this.indent*4)+line);
        }
    }

    private sealed class ScalarFusionEmitter {
        private const string DoneLabel = "__seamlessDone";
        private readonly ScalarFusionPipeline pipeline;
        private readonly ScalarTerminalInfo terminalInfo;
        private readonly List<string> lines = new();
        private int indent;
        private int valueIndex;
        private int loopIndex;

        public ScalarFusionEmitter(ScalarFusionPipeline pipeline,ScalarTerminalInfo terminalInfo) {
            this.pipeline=pipeline;
            this.terminalInfo=terminalInfo;
        }

        public string Build() {
            this.Add("{");
            this.indent++;
            this.Add($"var __seamlessSource = {this.pipeline.SourceExpression};");
            this.Add("global::System.ArgumentNullException.ThrowIfNull(__seamlessSource);");
            this.DeclareOperationState();
            this.DeclareAccumulator();
            this.EmitTakeEmptyShortCircuit();
            var rootContext = this.CreateLoopContext();
            this.Add("foreach(var __seamlessItem in __seamlessSource) {");
            this.indent++;
            this.EmitOperations(0,"__seamlessItem",rootContext);
            this.EmitLoopContinue(rootContext);
            this.indent--;
            this.Add("}");
            this.Add($"{DoneLabel}:;");
            this.EmitReturn();
            this.indent--;
            this.Add("}");
            return string.Join("\n",this.lines);
        }

        private void DeclareOperationState() {
            for(var index = 0; index<this.pipeline.Operations.Count; index++) {
                var operation = this.pipeline.Operations[index];
                switch(operation.MethodName) {
                    case "Where":
                    case "Select":
                    case "TakeWhile":
                    case "SkipWhile":
                        if(operation.Lambda is not null&&operation.Lambda.Parameters.Length==2) {
                            this.Add($"var __seamlessIndex{index} = -1;");
                        }
                        if(operation.MethodName=="SkipWhile") {
                            this.Add($"var __seamlessSkipWhileYielding{index} = false;");
                        }
                        break;

                    case "Skip":
                        this.Add($"var __seamlessSkip{index} = {NormalizeExpression(operation.Arguments[0].Expression)};");
                        break;

                    case "Take":
                        this.Add($"var __seamlessTake{index} = {NormalizeExpression(operation.Arguments[0].Expression)};");
                        break;

                    case "Distinct":
                        this.Add($"var __seamlessDistinct{index} = new global::System.Collections.Generic.HashSet<object>();");
                        break;

                    case "SelectMany":
                        if(operation.Lambda is not null&&operation.Lambda.Parameters.Length==2) {
                            this.Add($"var __seamlessIndex{index} = -1;");
                        }
                        break;
                }
            }
        }

        private void DeclareAccumulator() {
            this.Add($"{this.terminalInfo.AccumulatorTypeName} __seamlessSum = default;");
            if(this.terminalInfo.MethodName=="Average") {
                this.Add("long __seamlessCount = 0;");
            }
        }

        private void EmitTakeEmptyShortCircuit() {
            for(var index = 0; index<this.pipeline.Operations.Count; index++) {
                if(this.pipeline.Operations[index].MethodName!="Take") {
                    continue;
                }
                this.Add($"if(__seamlessTake{index}<=0) {{");
                this.indent++;
                this.Add($"goto {DoneLabel};");
                this.indent--;
                this.Add("}");
            }
        }

        private FusionLoopContext CreateLoopContext() {
            var index = this.loopIndex++;
            var context = new FusionLoopContext($"__seamlessContinue{index}",$"__seamlessStop{index}");
            this.Add($"var {context.StopFlag} = false;");
            return context;
        }

        private void EmitLoopContinue(FusionLoopContext context) {
            this.Add($"{context.ContinueLabel}:;");
            this.Add($"if({context.StopFlag}) {{");
            this.indent++;
            this.Add($"goto {DoneLabel};");
            this.indent--;
            this.Add("}");
        }

        private void EmitOperations(int operationIndex,string valueExpression,FusionLoopContext context) {
            if(operationIndex>=this.pipeline.Operations.Count) {
                this.EmitTerminal(valueExpression,context);
                return;
            }

            var operation = this.pipeline.Operations[operationIndex];
            switch(operation.MethodName) {
                case "Where":
                    this.EmitIndexedLambdaAdvance(operationIndex,operation);
                    this.Add($"if(!({this.InlineLambda(operation.Lambda!,valueExpression,this.GetIndexArgument(operationIndex,operation))})) {{");
                    this.indent++;
                    this.Add($"goto {context.ContinueLabel};");
                    this.indent--;
                    this.Add("}");
                    this.EmitOperations(operationIndex+1,valueExpression,context);
                    break;

                case "Select":
                    this.EmitIndexedLambdaAdvance(operationIndex,operation);
                    var selectedValue = this.NextValueName();
                    this.Add($"var {selectedValue} = {this.InlineLambda(operation.Lambda!,valueExpression,this.GetIndexArgument(operationIndex,operation))};");
                    this.EmitOperations(operationIndex+1,selectedValue,context);
                    break;

                case "Cast":
                    var castedValue = this.NextValueName();
                    this.Add($"var {castedValue} = ({operation.TypeArguments[0]}){valueExpression}!;");
                    this.EmitOperations(operationIndex+1,castedValue,context);
                    break;

                case "OfType":
                    var typedValue = this.NextValueName();
                    this.Add($"if({valueExpression} is not {operation.TypeArguments[0]} {typedValue}) {{");
                    this.indent++;
                    this.Add($"goto {context.ContinueLabel};");
                    this.indent--;
                    this.Add("}");
                    this.EmitOperations(operationIndex+1,typedValue,context);
                    break;

                case "Skip":
                    this.Add($"if(__seamlessSkip{operationIndex}>0) {{");
                    this.indent++;
                    this.Add($"__seamlessSkip{operationIndex}--;");
                    this.Add($"goto {context.ContinueLabel};");
                    this.indent--;
                    this.Add("}");
                    this.EmitOperations(operationIndex+1,valueExpression,context);
                    break;

                case "Take":
                    this.Add($"if(__seamlessTake{operationIndex}<=0) {{");
                    this.indent++;
                    this.Add($"{context.StopFlag}=true;");
                    this.Add($"goto {context.ContinueLabel};");
                    this.indent--;
                    this.Add("}");
                    this.Add($"__seamlessTake{operationIndex}--;");
                    this.Add($"if(__seamlessTake{operationIndex}<=0) {{");
                    this.indent++;
                    this.Add($"{context.StopFlag}=true;");
                    this.indent--;
                    this.Add("}");
                    this.EmitOperations(operationIndex+1,valueExpression,context);
                    break;

                case "SkipWhile":
                    this.Add($"if(!__seamlessSkipWhileYielding{operationIndex}) {{");
                    this.indent++;
                    this.EmitIndexedLambdaAdvance(operationIndex,operation);
                    this.Add($"if({this.InlineLambda(operation.Lambda!,valueExpression,this.GetIndexArgument(operationIndex,operation))}) {{");
                    this.indent++;
                    this.Add($"goto {context.ContinueLabel};");
                    this.indent--;
                    this.Add("}");
                    this.Add($"__seamlessSkipWhileYielding{operationIndex}=true;");
                    this.indent--;
                    this.Add("}");
                    this.EmitOperations(operationIndex+1,valueExpression,context);
                    break;

                case "TakeWhile":
                    this.EmitIndexedLambdaAdvance(operationIndex,operation);
                    this.Add($"if(!({this.InlineLambda(operation.Lambda!,valueExpression,this.GetIndexArgument(operationIndex,operation))})) {{");
                    this.indent++;
                    this.Add($"{context.StopFlag}=true;");
                    this.Add($"goto {context.ContinueLabel};");
                    this.indent--;
                    this.Add("}");
                    this.EmitOperations(operationIndex+1,valueExpression,context);
                    break;

                case "Distinct":
                    this.Add($"if(!__seamlessDistinct{operationIndex}.Add({valueExpression})) {{");
                    this.indent++;
                    this.Add($"goto {context.ContinueLabel};");
                    this.indent--;
                    this.Add("}");
                    this.EmitOperations(operationIndex+1,valueExpression,context);
                    break;

                case "SelectMany":
                    this.EmitSelectMany(operationIndex,valueExpression);
                    break;
            }
        }

        private void EmitSelectMany(int operationIndex,string valueExpression) {
            var operation = this.pipeline.Operations[operationIndex];
            this.EmitIndexedLambdaAdvance(operationIndex,operation);
            var collectionValue = this.NextValueName();
            this.Add($"var {collectionValue} = {this.InlineLambda(operation.Lambda!,valueExpression,this.GetIndexArgument(operationIndex,operation))};");
            var innerItem = this.NextValueName();
            var innerContext = this.CreateLoopContext();
            this.Add($"foreach(var {innerItem} in {collectionValue}) {{");
            this.indent++;
            var downstreamValue = innerItem;
            if(operation.SecondLambda is not null) {
                downstreamValue=this.NextValueName();
                this.Add($"var {downstreamValue} = {this.InlineLambda(operation.SecondLambda,valueExpression,innerItem)};");
            }
            this.EmitOperations(operationIndex+1,downstreamValue,innerContext);
            this.EmitLoopContinue(innerContext);
            this.indent--;
            this.Add("}");
        }

        private void EmitIndexedLambdaAdvance(int operationIndex,ScalarFusionOperation operation) {
            if(operation.Lambda is null||operation.Lambda.Parameters.Length!=2) {
                return;
            }
            this.Add("checked {");
            this.indent++;
            this.Add($"__seamlessIndex{operationIndex}++;");
            this.indent--;
            this.Add("}");
        }

        private string? GetIndexArgument(int operationIndex,ScalarFusionOperation operation) {
            return operation.Lambda is not null&&operation.Lambda.Parameters.Length==2
                ? $"__seamlessIndex{operationIndex}"
                : null;
        }

        private void EmitTerminal(string valueExpression,FusionLoopContext context) {
            var terminalValue = valueExpression;
            if(this.terminalInfo.TerminalValueIsNullable) {
                terminalValue=this.NextValueName();
                this.Add($"var {terminalValue} = {valueExpression};");
                this.Add($"if(!{terminalValue}.HasValue) {{");
                this.indent++;
                this.Add($"goto {context.ContinueLabel};");
                this.indent--;
                this.Add("}");
                terminalValue=$"{terminalValue}.GetValueOrDefault()";
            }
            if(this.terminalInfo.UseCheckedAccumulator) {
                this.Add("checked {");
                this.indent++;
                this.Add($"__seamlessSum+={terminalValue};");
                if(this.terminalInfo.MethodName=="Average") {
                    this.Add("__seamlessCount++;");
                }
                this.indent--;
                this.Add("}");
            }
            else {
                this.Add($"__seamlessSum+={terminalValue};");
                if(this.terminalInfo.MethodName=="Average") {
                    this.Add("checked {");
                    this.indent++;
                    this.Add("__seamlessCount++;");
                    this.indent--;
                    this.Add("}");
                }
            }
        }

        private void EmitReturn() {
            if(this.terminalInfo.MethodName=="Sum") {
                this.Add("return __seamlessSum;");
                return;
            }
            this.Add("if(__seamlessCount==0) {");
            this.indent++;
            if(this.terminalInfo.ReturnTypeIsNullable) {
                this.Add("return default;");
            }
            else {
                this.Add("throw new global::System.InvalidOperationException(\"Sequence contains no elements.\");");
            }
            this.indent--;
            this.Add("}");
            var averageExpression = this.terminalInfo.CastAccumulatorToDoubleBeforeAverageDivide
                ? "(double)__seamlessSum/__seamlessCount"
                : "__seamlessSum/__seamlessCount";
            this.Add($"return ({this.terminalInfo.ReturnTypeName})({averageExpression});");
        }

        private string InlineLambda(FusionLambda lambda,string firstArgument,string? secondArgument) {
            var replacements = new Dictionary<string,ExpressionSyntax>(StringComparer.Ordinal) {
                [lambda.Parameters[0]]=SyntaxFactory.ParseExpression(firstArgument),
            };
            if(secondArgument is not null&&lambda.Parameters.Length>1) {
                replacements[lambda.Parameters[1]]=SyntaxFactory.ParseExpression(secondArgument);
            }
            var rewritten = new LambdaParameterInlineRewriter(replacements).Visit(lambda.Body) as ExpressionSyntax??lambda.Body;
            return NormalizeExpression(rewritten);
        }

        private string NextValueName() {
            return $"__seamlessValue{this.valueIndex++}";
        }

        private void Add(string line) {
            this.lines.Add(new string(' ',this.indent*4)+line);
        }
    }

    // Aggregate は call-through にすると空列時例外や列挙手順の表現が曖昧になるため、明示的な列挙器ループへ展開する。
    /// <summary>
    /// Aggregate を明示的な列挙器 foreach ループへ展開するブロック本体を生成する。
    /// </summary>
    /// <example><code>
    /// // 変換前:
    /// source.Aggregate((a, b) => a + b)
    ///
    /// // 変換後:
    /// {
    ///     var __aggregateSource = source;
    ///     Func&lt;T, T, T&gt; __aggregateFunc = (a, b) => a + b;
    ///     T __aggregateResult = default!;
    ///     var __aggregateHasValue = false;
    ///     foreach (var __aggregateCurrent in __aggregateSource)
    ///     {
    ///         if (!__aggregateHasValue)
    ///             { __aggregateResult = __aggregateCurrent; __aggregateHasValue = true; continue; }
    ///         __aggregateResult = __aggregateFunc(__aggregateResult, __aggregateCurrent);
    ///     }
    ///     if (!__aggregateHasValue) throw new InvalidOperationException("Sequence contains no elements.");
    ///     return __aggregateResult;
    /// }
    /// </code></example>
    private static string? 集約ボディ翻訳試行(IMethodSymbol method,InvocationExpressionSyntax invocation,SemanticModel semanticModel) {
        if(invocation.Expression is not MemberAccessExpressionSyntax memberAccess) {
            return null;
        }
        if(memberAccess.Name.Identifier.ValueText!="Aggregate") {
            return null;
        }
        var receiver = 入れ子式翻訳(memberAccess.Expression,semanticModel);
        var arguments = invocation.ArgumentList.Arguments
            .Select(argument => 入れ子式翻訳(argument.Expression,semanticModel))
            .ToArray();

        string sourceExpression;
        string funcExpression;
        if(IsStaticEnumerableReceiver(receiver)) {
            if(arguments.Length!=2) {
                return null;
            }
            sourceExpression=arguments[0];
            funcExpression=arguments[1];
        }
        else {
            if(arguments.Length!=1) {
                return null;
            }
            sourceExpression=receiver;
            funcExpression=arguments[0];
        }

        var returnType = method.ReturnType.ToDisplayString(FullyQualifiedFormat);
        return string.Join("\n",new[] {
            "{",
            $"    var __aggregateSource = {sourceExpression};",
            "    global::System.ArgumentNullException.ThrowIfNull(__aggregateSource);",
            $"    global::System.Func<{returnType}, {returnType}, {returnType}> __aggregateFunc = {funcExpression};",
            "    global::System.ArgumentNullException.ThrowIfNull(__aggregateFunc);",
            $"    {returnType} __aggregateResult = default!;",
            "    var __aggregateHasValue = false;",
            "    foreach(var __aggregateCurrent in __aggregateSource) {",
            "        if(!__aggregateHasValue) {",
            "            __aggregateResult = __aggregateCurrent;",
            "            __aggregateHasValue = true;",
            "            continue;",
            "        }",
            "        __aggregateResult = __aggregateFunc(__aggregateResult,__aggregateCurrent);",
            "    }",
            "    if(!__aggregateHasValue) {",
            "        throw new global::System.InvalidOperationException(\"Sequence contains no elements.\");",
            "    }",
            "    return __aggregateResult;",
            "}"
        });
    }

    // query syntax はまず extension-style の method chain に下ろし、
    // その後 OptimizeVisitor と既存の invocation translator に流す。
    // これで query continuation (`select ... into x where ...`) も invocation 側と同じ最適化経路に乗る。
    /// <summary>
    /// クエリ式構文をメソッドチェーンへ脱糖化 (Lower) し、<br/>
    /// OptimizeVisitor で最適化してから呼び出し式翻訳試行に流す。
    /// </summary>
    /// <example><code>
    /// // 変換前 (クエリ式):
    /// from x in source where x > 0 select x * 2
    ///
    /// // 脱糖化後:
    /// source.Where(x => x > 0).Select(x => x * 2)
    ///
    /// // さらに呼び出し式翻訳試行を経て:
    /// global::Seamless.Linq.Enumerable.Select&lt;int, int&gt;(
    ///     global::Seamless.Linq.Enumerable.Where&lt;int&gt;(source, x => x > 0), x => x * 2)
    /// </code></example>
    private static string? クエリ式翻訳試行(QueryExpressionSyntax query,SemanticModel? semanticModel = null) {
        if(TryLowerQueryExpressionToInvocation(query,semanticModel,out var loweredInvocation)) {
            var optimized = OptimizeVisitor.Optimize(loweredInvocation);
            return optimized is InvocationExpressionSyntax optimizedInvocation
                ? 呼び出し式翻訳試行(optimizedInvocation,semanticModel)??NormalizeExpression(optimized)
                : NormalizeExpression(optimized);
        }
        return null;
    }

    private static bool TryLowerQueryExpressionToInvocation(QueryExpressionSyntax query,SemanticModel? semanticModel,out InvocationExpressionSyntax invocation) {
        var temporaryIndex = 0;
        if(TryLowerQueryBody(
                入れ子式翻訳(query.FromClause.Expression,semanticModel),
                QueryRangeScope.CreateIdentity(query.FromClause.Identifier.ValueText),
                query.Body,
                semanticModel,
                ref temporaryIndex,
                out var loweredText)
            &&SyntaxFactory.ParseExpression(loweredText) is InvocationExpressionSyntax loweredInvocation) {
            invocation=loweredInvocation;
            return true;
        }

        invocation=null!;
        return false;
    }

    private static bool TryLowerQueryBody(string sourceExpression,QueryRangeScope scope,QueryBodySyntax body,SemanticModel? semanticModel,ref int temporaryIndex,out string loweredText) {
        var currentSource = sourceExpression;
        var currentScope = scope;

        foreach(var clause in body.Clauses) {
            switch(clause) {
                case FromClauseSyntax fromClause:
                    var collectionSelector = RewriteQueryRangeVariables(fromClause.Expression,currentScope,semanticModel);
                    var innerRangeVariable = fromClause.Identifier.ValueText;
                    currentSource=$"({currentSource}).SelectMany({currentScope.CurrentParameterName} => {collectionSelector},({currentScope.CurrentParameterName},{innerRangeVariable}) => {BuildCarrierCreationText(currentScope,(innerRangeVariable,innerRangeVariable))})";
                    currentScope=currentScope.CreateCarrierScope(CreateGeneratedQueryIdentifier(ref temporaryIndex),innerRangeVariable);
                    break;

                case LetClauseSyntax letClause:
                    var letRangeVariable = letClause.Identifier.ValueText;
                    var letExpression = RewriteQueryRangeVariables(letClause.Expression,currentScope,semanticModel);
                    currentSource=$"({currentSource}).Select({currentScope.CurrentParameterName} => {BuildCarrierCreationText(currentScope,(letRangeVariable,letExpression))})";
                    currentScope=currentScope.CreateCarrierScope(CreateGeneratedQueryIdentifier(ref temporaryIndex),letRangeVariable);
                    break;

                case WhereClauseSyntax whereClause:
                    currentSource=$"({currentSource}).Where({currentScope.CurrentParameterName} => {RewriteQueryRangeVariables(whereClause.Condition,currentScope,semanticModel)})";
                    break;

                case JoinClauseSyntax joinClause:
                    var joinSource = RewriteQueryRangeVariables(joinClause.InExpression,currentScope,semanticModel);
                    var outerParameter = currentScope.CurrentParameterName;
                    var innerParameter = joinClause.Identifier.ValueText;
                    var outerKey = RewriteQueryRangeVariables(joinClause.LeftExpression,currentScope,semanticModel);
                    var innerKey = RewriteQueryRangeVariables(joinClause.RightExpression,QueryRangeScope.CreateIdentity(innerParameter),semanticModel);
                    if(joinClause.Into is null) {
                        currentSource=$"({currentSource}).Join({joinSource},{outerParameter} => {outerKey},{innerParameter} => {innerKey},({outerParameter},{innerParameter}) => {BuildCarrierCreationText(currentScope,(innerParameter,innerParameter))})";
                        currentScope=currentScope.CreateCarrierScope(CreateGeneratedQueryIdentifier(ref temporaryIndex),innerParameter);
                    }
                    else {
                        var groupedRangeVariable = joinClause.Into.Identifier.ValueText;
                        currentSource=$"({currentSource}).GroupJoin({joinSource},{outerParameter} => {outerKey},{innerParameter} => {innerKey},({outerParameter},{groupedRangeVariable}) => {BuildCarrierCreationText(currentScope,(groupedRangeVariable,groupedRangeVariable))})";
                        currentScope=currentScope.CreateCarrierScope(CreateGeneratedQueryIdentifier(ref temporaryIndex),groupedRangeVariable);
                    }
                    break;

                case OrderByClauseSyntax orderByClause:
                    for(var index = 0; index<orderByClause.Orderings.Count; index++) {
                        var ordering = orderByClause.Orderings[index];
                        var keySelector = RewriteQueryRangeVariables(ordering.Expression,currentScope,semanticModel);
                        var isDescending = ordering.AscendingOrDescendingKeyword.IsKind(SyntaxKind.DescendingKeyword);
                        var methodName = index==0
                            ? (isDescending ? "OrderByDescending" : "OrderBy")
                            : (isDescending ? "ThenByDescending" : "ThenBy");
                        currentSource=$"({currentSource}).{methodName}({currentScope.CurrentParameterName} => {keySelector})";
                    }
                    break;

                default:
                    loweredText=string.Empty;
                    return false;
            }
        }

        switch(body.SelectOrGroup) {
            case SelectClauseSyntax selectClause:
                var selection = RewriteQueryRangeVariables(selectClause.Expression,currentScope,semanticModel);
                currentSource=$"({currentSource}).Select({currentScope.CurrentParameterName} => {selection})";
                if(body.Continuation is null) {
                    loweredText=currentSource;
                    return true;
                }
                return TryLowerQueryBody(currentSource,QueryRangeScope.CreateIdentity(body.Continuation.Identifier.ValueText),body.Continuation.Body,semanticModel,ref temporaryIndex,out loweredText);

            case GroupClauseSyntax groupClause:
                var groupedElement = RewriteQueryRangeVariables(groupClause.GroupExpression,currentScope,semanticModel);
                var groupedKey = RewriteQueryRangeVariables(groupClause.ByExpression,currentScope,semanticModel);
                currentSource=$"({currentSource}).GroupBy({currentScope.CurrentParameterName} => {groupedKey},{currentScope.CurrentParameterName} => {groupedElement})";
                if(body.Continuation is null) {
                    loweredText=currentSource;
                    return true;
                }
                return TryLowerQueryBody(currentSource,QueryRangeScope.CreateIdentity(body.Continuation.Identifier.ValueText),body.Continuation.Body,semanticModel,ref temporaryIndex,out loweredText);

            default:
                loweredText=string.Empty;
                return false;
        }
    }

    private static string RewriteQueryRangeVariables(ExpressionSyntax expression,QueryRangeScope scope,SemanticModel? semanticModel) {
        var rewritten = new QueryRangeVariableRewriter(scope.RangeExpressions).Visit(expression);
        return 入れ子式翻訳(rewritten as ExpressionSyntax??expression,semanticModel);
    }

    private static string BuildCarrierCreationText(QueryRangeScope scope,params (string Name,string ExpressionText)[] additionalFields) {
        var assignments = new List<string>(scope.VisibleVariables.Count+additionalFields.Length);
        foreach(var visibleVariable in scope.VisibleVariables) {
            assignments.Add($"{visibleVariable} = {scope.RangeExpressions[visibleVariable]}");
        }
        foreach(var additionalField in additionalFields) {
            assignments.Add($"{additionalField.Name} = {additionalField.ExpressionText}");
        }
        return $"new {{ {string.Join(", ",assignments)} }}";
    }

    private static string CreateGeneratedQueryIdentifier(ref int temporaryIndex) {
        return $"__q{temporaryIndex++}";
    }

    // method chain を再帰的に辿って Seamless LINQ 呼び出しへそろえる。
    // receiver だけでなく argument 側の nested invocation も変換するのが重要。
    private static readonly HashSet<string> EnumerableMethodNames = new(StringComparer.Ordinal) {
        "Aggregate",
        "AggregateBy",
        "All",
        "Any",
        "Append",
        "AsEnumerable",
        "Average",
        "Cast",
        "Chunk",
        "Concat",
        "Contains",
        "Count",
        "CountBy",
        "DefaultIfEmpty",
        "Distinct",
        "DistinctBy",
        "ElementAt",
        "ElementAtOrDefault",
        "Empty",
        "Except",
        "ExceptBy",
        "First",
        "FirstOrDefault",
        "GroupBy",
        "GroupJoin",
        "Index",
        "InfiniteSequence",
        "Intersect",
        "IntersectBy",
        "Join",
        "Last",
        "LastOrDefault",
        "LeftJoin",
        "LongCount",
        "Max",
        "MaxBy",
        "Min",
        "MinBy",
        "OfType",
        "Order",
        "OrderBy",
        "OrderByDescending",
        "OrderDescending",
        "Prepend",
        "Range",
        "Repeat",
        "Reverse",
        "RightJoin",
        "Select",
        "SelectMany",
        "Sequence",
        "SequenceEqual",
        "Shuffle",
        "Single",
        "SingleOrDefault",
        "Skip",
        "SkipLast",
        "SkipWhile",
        "Sum",
        "Take",
        "TakeLast",
        "TakeWhile",
        "ThenBy",
        "ThenByDescending",
        "ToArray",
        "ToDictionary",
        "ToHashSet",
        "ToList",
        "ToLookup",
        "TryGetNonEnumeratedCount",
        "Union",
        "UnionBy",
        "Where",
        "Zip"
    };

    private static readonly HashSet<string> HashSetMethodNames = new(StringComparer.Ordinal) {
        "Aggregate",
        "AggregateBy",
        "All",
        "Any",
        "Append",
        "AsEnumerable",
        "Average",
        "Chunk",
        "Concat",
        "Contains",
        "Count",
        "CountBy",
        "DefaultIfEmpty",
        "Distinct",
        "DistinctBy",
        "ElementAt",
        "ElementAtOrDefault",
        "Except",
        "ExceptBy",
        "First",
        "FirstOrDefault",
        "GroupBy",
        "GroupJoin",
        "Index",
        "Intersect",
        "IntersectBy",
        "Join",
        "Last",
        "LastOrDefault",
        "LeftJoin",
        "LongCount",
        "Max",
        "MaxBy",
        "Min",
        "MinBy",
        "Order",
        "OrderBy",
        "OrderByDescending",
        "OrderDescending",
        "Prepend",
        "Reverse",
        "RightJoin",
        "Select",
        "SelectMany",
        "SequenceEqual",
        "Shuffle",
        "Single",
        "SingleOrDefault",
        "Skip",
        "SkipLast",
        "SkipWhile",
        "Sum",
        "Take",
        "TakeLast",
        "TakeWhile",
        "ToArray",
        "ToDictionary",
        "ToHashSet",
        "ToList",
        "ToLookup",
        "TryGetNonEnumeratedCount",
        "Union",
        "UnionBy",
        "Where",
        "Zip"
    };

    // method chain を再帰的に辿って Seamless LINQ 呼び出しへそろえる。
    // semanticModel がある場合は呼び出し先の型で Enumerable / HashSet を正確に振り分ける。
    /// <summary>
    /// メソッドチェーン呼び出しを再帰的に辿り、<br/>
    /// <c>System.Linq.Enumerable</c> / <c>Seamless.Linq.HashSet</c> の呼び出しを<br/>
    /// <c>global::Seamless.Linq.Enumerable</c> / <c>HashSet</c> へ差し替えた式文字列を返す。
    /// </summary>
    /// <example><code>
    /// // 変換前:
    /// source.Where(x => x > 0).OrderBy(x => x)
    ///
    /// // 変換後:
    /// global::Seamless.Linq.Enumerable.OrderBy&lt;int, int&gt;(
    ///     global::Seamless.Linq.Enumerable.Where&lt;int&gt;(source, x => x > 0), x => x)
    /// </code></example>
    private static string? 呼び出し式翻訳試行(InvocationExpressionSyntax invocation,SemanticModel? semanticModel = null) {
        if(invocation.Expression is SimpleNameSyntax simpleName) {
            return 非修飾メンバー呼び出し翻訳試行(invocation,simpleName,semanticModel);
        }
        if(invocation.Expression is not MemberAccessExpressionSyntax memberAccess) {
            return null;
        }
        var methodName = memberAccess.Name.Identifier.ValueText;
        var typeArgumentListText = GetTypeArgumentListText(memberAccess.Name);
        var receiver = 入れ子式翻訳(memberAccess.Expression,semanticModel);
        var semanticTargetType = TryGetTranslatedLinqTarget(invocation,semanticModel,out var symbolResolved);
        var targetType = semanticTargetType
            ??(IsStaticHashSetReceiver(receiver) ? "HashSet" : null)
            ??(IsStaticEnumerableReceiver(receiver) ? "Enumerable" : null);
        if(targetType is null&&symbolResolved) {
            return null;
        }
        targetType??="Enumerable";
        var supportedNames = targetType=="HashSet" ? HashSetMethodNames : EnumerableMethodNames;
        if(!supportedNames.Contains(methodName)) {
            return null;
        }
        var arguments = invocation.ArgumentList.Arguments
            .Select(argument => 入れ子引数翻訳(argument,semanticModel))
            .ToArray();
        if(IsStaticEnumerableReceiver(receiver)||IsStaticHashSetReceiver(receiver)) {
            return arguments.Length==0
                ? $"global::Seamless.Linq.{targetType}.{methodName}{typeArgumentListText}()"
                : $"global::Seamless.Linq.{targetType}.{methodName}{typeArgumentListText}({string.Join(",",arguments)})";
        }
        return arguments.Length==0
            ? $"global::Seamless.Linq.{targetType}.{methodName}{typeArgumentListText}({receiver})"
            : $"global::Seamless.Linq.{targetType}.{methodName}{typeArgumentListText}({receiver},{string.Join(",",arguments)})";
    }

    /// <summary>
    /// レシーバーなし (非修飾) のメソッド呼び出しを、<br/>
    /// セマンティックモデルで解決した完全修飾名に差し替えた式文字列を返す。
    /// </summary>
    /// <example><code>
    /// // 変換前 (using static 等で非修飾):
    /// Where(source, x => x > 0)
    ///
    /// // 変換後 (完全修飾):
    /// global::Seamless.Linq.Enumerable.Where&lt;int&gt;(source, x => x > 0)
    /// </code></example>
    private static string? 非修飾メンバー呼び出し翻訳試行(InvocationExpressionSyntax invocation,SimpleNameSyntax simpleName,SemanticModel? semanticModel) {
        if(!TryGetInvocationMethodSymbol(invocation,semanticModel,out var methodSymbol)) {
            return null;
        }
        var definition = methodSymbol.ReducedFrom??methodSymbol;
        if(definition.MethodKind!=MethodKind.Ordinary||definition.ContainingType is null) {
            return null;
        }
        var arguments = invocation.ArgumentList.Arguments
            .Select(argument => 入れ子引数翻訳(argument,semanticModel))
            .ToArray();
        var typeArgumentListText = GetTypeArgumentListText(simpleName);
        if(definition.IsStatic) {
            return $"{definition.ContainingType.ToDisplayString(FullyQualifiedFormat)}.{definition.Name}{typeArgumentListText}({string.Join(",",arguments)})";
        }
        return $"self.{definition.Name}{typeArgumentListText}({string.Join(",",arguments)})";
    }

    private static string GetTypeArgumentListText(SimpleNameSyntax name) {
        return name is GenericNameSyntax genericName ? genericName.TypeArgumentList.ToString() : string.Empty;
    }

    private static string? TryGetTranslatedLinqTarget(InvocationExpressionSyntax invocation,SemanticModel? semanticModel,out bool symbolResolved) {
        symbolResolved=false;
        if(!TryGetInvocationMethodSymbol(invocation,semanticModel,out var methodSymbol)) {
            return null;
        }
        symbolResolved=true;
        var definition = methodSymbol.ReducedFrom??methodSymbol;
        var containingType = definition.ContainingType.ToDisplayString();
        return containingType switch {
            "System.Linq.Enumerable" => "Enumerable",
            "Seamless.Linq.Enumerable" => "Enumerable",
            "Seamless.Linq.HashSet" => "HashSet",
            _ => null
        };
    }

    private static bool TryGetInvocationMethodSymbol(InvocationExpressionSyntax invocation,SemanticModel? semanticModel,out IMethodSymbol methodSymbol) {
        methodSymbol=null!;
        if(semanticModel is null) {
            return false;
        }
        try {
            if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol symbol) {
                return false;
            }
            methodSymbol=symbol;
        }
        catch(ArgumentException) {
            return false;
        }
        return true;
    }

    /// <summary>
    /// 引数構文の式部分を入れ子式翻訳し、ref / out / in キーワードがあれば先頭に付加して返す。
    /// </summary>
    /// <example><code>
    /// // 変換前 (引数式):
    /// source.Where(x => x > 0)
    /// // 変換後:
    /// global::Seamless.Linq.Enumerable.Where&lt;int&gt;(source, x => x > 0)
    ///
    /// // ref キーワード付きの場合:
    /// ref myValue  =>  ref myValue  (式部分のみ翻訳、ref は維持)
    /// </code></example>
    private static string 入れ子引数翻訳(ArgumentSyntax argument,SemanticModel? semanticModel) {
        var expression = 入れ子式翻訳(argument.Expression,semanticModel);
        return argument.RefKindKeyword.IsKind(SyntaxKind.None)
            ? expression
            : $"{argument.RefKindKeyword.Text} {expression}";
    }

    /// <summary>
    /// 任意の式ノードを再帰的に追り、内部に含まれる LINQ 呼び出しを<br/>
    /// Seamless.Linq 名前空間の呼び出しへ差し替えた文字列を返す。
    /// </summary>
    /// <example><code>
    /// // 変換前:
    /// items.Where(x => x.Values.Select(v => v * 2).Sum() > 0)
    ///
    /// // 変換後:
    /// global::Seamless.Linq.Enumerable.Where&lt;Item&gt;(items,
    ///     x => global::Seamless.Linq.Enumerable.Sum(
    ///              global::Seamless.Linq.Enumerable.Select&lt;int, int&gt;(x.Values, v => v * 2)) > 0)
    /// </code></example>
    private static string 入れ子式翻訳(ExpressionSyntax expression,SemanticModel? semanticModel = null) {
        while(expression is ParenthesizedExpressionSyntax parenthesizedExpression) {
            expression=parenthesizedExpression.Expression;
        }
        if(expression is InvocationExpressionSyntax invocationExpression) {
            return 呼び出し式翻訳試行(invocationExpression,semanticModel)??NormalizeExpression(invocationExpression);
        }
        var 入れ子Invocation変換後 = new NestedInvocationTranslator(semanticModel).Visit(expression) as ExpressionSyntax??expression;
        return NormalizeExpression(入れ子Invocation変換後);
    }

    // lambda body や匿名型 initializer の中に残った LINQ invocation を同じ翻訳規則でそろえる。
    // これを挟まないと、generated source 側に using System.Linq が無い場所で `.Where(...)` が残る。
    private sealed class NestedInvocationTranslator:CSharpSyntaxRewriter {
        private readonly SemanticModel? semanticModel;

        public NestedInvocationTranslator(SemanticModel? semanticModel) {
            this.semanticModel=semanticModel;
        }

        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node) {
            var 変換済み = 呼び出し式翻訳試行(node,this.semanticModel);
            if(変換済み is not null) {
                return SyntaxFactory.ParseExpression(変換済み);
            }
            return base.VisitInvocationExpression(node);
        }
    }

    private static bool IsStaticEnumerableReceiver(string receiver) {
        return string.Equals(receiver,"Enumerable",StringComparison.Ordinal)
            ||string.Equals(receiver,"SystemEnumerable",StringComparison.Ordinal)
            ||string.Equals(receiver,"SeamlessEnumerable",StringComparison.Ordinal)
            ||receiver.EndsWith(".Enumerable",StringComparison.Ordinal);
    }

    private static bool IsStaticHashSetReceiver(string receiver) {
        return string.Equals(receiver,"HashSet",StringComparison.Ordinal)
            ||string.Equals(receiver,"SeamlessHashSet",StringComparison.Ordinal)
            ||receiver.EndsWith(".HashSet",StringComparison.Ordinal);
    }

    private static ImmutableArray<IPropertySymbol> GetGeneratableProperties(INamedTypeSymbol symbol) {
        var builder = ImmutableArray.CreateBuilder<IPropertySymbol>();
        foreach(var property in symbol.GetMembers().OfType<IPropertySymbol>()) {
            if(ShouldGenerateProperty(property)) {
                builder.Add(property);
            }
        }
        return builder.ToImmutable();
    }

    private static bool ShouldGenerateProperty(IPropertySymbol property) {
        foreach(var syntaxReference in property.DeclaringSyntaxReferences) {
            if(syntaxReference.GetSyntax() is not PropertyDeclarationSyntax syntax) {
                continue;
            }
            if(!syntax.Modifiers.Any(SyntaxKind.PartialKeyword)) {
                continue;
            }
            if(syntax.ExpressionBody is not null) {
                continue;
            }
            if(syntax.AccessorList is null||syntax.AccessorList.Accessors.Count!=1) {
                continue;
            }
            var accessor = syntax.AccessorList.Accessors[0];
            if(accessor.Kind()!=SyntaxKind.GetAccessorDeclaration) {
                continue;
            }
            if(accessor.Body is not null||accessor.ExpressionBody is not null) {
                continue;
            }
            return true;
        }
        return false;
    }

    private static string GetParameterName(IPropertySymbol property) {
        return GetParameterName(property.Name);
    }

    private static string GetParameterName(string name) {
        if(string.IsNullOrEmpty(name)) {
            return "value";
        }
        if(name.Length==1) {
            return char.ToLowerInvariant(name[0]).ToString();
        }
        return char.ToLowerInvariant(name[0])+name.Substring(1);
    }

    private static string NormalizeExpression(ExpressionSyntax expression) {
        return expression.ToString();
    }

    private static ExpressionSyntax RemoveParentheses(ExpressionSyntax expression) {
        while(expression is ParenthesizedExpressionSyntax parenthesized) {
            expression=parenthesized.Expression;
        }
        return expression;
    }

    private static bool NamespaceEquals(string candidateNamespace,string rootNamespace,string childNamespace) {
        return string.Equals(candidateNamespace,$"{rootNamespace}.{childNamespace}",StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeIdentifier(string value) {
        var builder = new StringBuilder(value.Length);
        foreach(var character in value) {
            if(char.IsLetterOrDigit(character)||character=='_') {
                builder.Append(character);
            }
            else {
                builder.Append('_');
            }
        }
        if(builder.Length==0||!char.IsLetter(builder[0])&&builder[0]!='_') {
            builder.Insert(0,'_');
        }
        return builder.ToString();
    }

    private static string ToLiteral(string value) {
        return SymbolDisplay.FormatLiteral(value,quote: true);
    }

    // unmanaged フィールドは record struct にまとめて一括シリアライズしやすくする。
    private static void AppendUnmanagedRecordSource(StringBuilder builder,ImmutableArray<IPropertySymbol> unmanagedProperties) {
        if(unmanagedProperties.Length>0) {
            builder.AppendLine($"    private readonly record struct _U({string.Join(", ",unmanagedProperties.Select(static property => $"{property.Type.ToDisplayString(FullyQualifiedFormat)} {property.Name}"))});");
            return;
        }

        builder.AppendLine("    private struct _U {");
        builder.AppendLine("    }");
    }

    private static void AppendSchemaUnmanagedRecordSource(StringBuilder builder,ImmutableArray<SchemaPropertyDefinition> unmanagedProperties) {
        if(unmanagedProperties.Length>0) {
            builder.AppendLine($"    private readonly record struct _U({string.Join(", ",unmanagedProperties.Select(static property => $"{property.TypeName} {property.Name}"))});");
            return;
        }

        builder.AppendLine("    private struct _U {");
        builder.AppendLine("    }");
    }

    // managed フィールドは MemoryPackable な _M 側へ寄せ、型別の serialize / deserialize 文をここで組み立てる。
    private static void AppendManagedRecordSource(StringBuilder builder,ImmutableArray<IPropertySymbol> managedProperties) {
        builder.AppendLine("    private struct _M:global::MemoryPack.IMemoryPackable<_M> {");
        foreach(var property in managedProperties) {
            builder.AppendLine($"        public {property.Type.ToDisplayString(FullyQualifiedFormat)} {property.Name};");
        }
        if(managedProperties.Length>0) {
            builder.AppendLine();
            builder.AppendLine($"        public _M({string.Join(", ",managedProperties.Select(static property => $"{property.Type.ToDisplayString(FullyQualifiedFormat)} {GetParameterName(property)}"))}) {{");
            foreach(var property in managedProperties) {
                builder.AppendLine($"            this.{property.Name}={GetParameterName(property)};");
            }
            builder.AppendLine("        }");
            builder.AppendLine();
        }
        builder.AppendLine("        static void global::MemoryPack.IMemoryPackFormatterRegister.RegisterFormatter() {");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        static void global::MemoryPack.IMemoryPackable<_M>.Serialize<TBufferWriter>(ref global::MemoryPack.MemoryPackWriter<TBufferWriter> writer,scoped ref _M value) {");
        foreach(var property in managedProperties) {
            builder.AppendLine($"            {BuildManagedSerializeStatement(property)}");
        }
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        static void global::MemoryPack.IMemoryPackable<_M>.Deserialize(ref global::MemoryPack.MemoryPackReader reader,scoped ref _M value) {");
        foreach(var property in managedProperties) {
            builder.AppendLine($"            {BuildManagedDeserializeStatement(property)}");
        }
        builder.AppendLine("        }");
        builder.AppendLine("    }");
    }

    private static void AppendSchemaManagedRecordSource(StringBuilder builder,ImmutableArray<SchemaPropertyDefinition> managedProperties) {
        builder.AppendLine("    private struct _M:global::MemoryPack.IMemoryPackable<_M> {");
        foreach(var property in managedProperties) {
            builder.AppendLine($"        public {property.TypeName} {property.Name};");
        }
        if(managedProperties.Length>0) {
            builder.AppendLine();
            builder.AppendLine($"        public _M({string.Join(", ",managedProperties.Select(static property => $"{property.TypeName} {GetParameterName(property.Name)}"))}) {{");
            foreach(var property in managedProperties) {
                builder.AppendLine($"            this.{property.Name}={GetParameterName(property.Name)};");
            }
            builder.AppendLine("        }");
            builder.AppendLine();
        }
        builder.AppendLine("        static void global::MemoryPack.IMemoryPackFormatterRegister.RegisterFormatter() {");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        static void global::MemoryPack.IMemoryPackable<_M>.Serialize<TBufferWriter>(ref global::MemoryPack.MemoryPackWriter<TBufferWriter> writer,scoped ref _M value) {");
        foreach(var property in managedProperties) {
            builder.AppendLine($"            {BuildSchemaManagedSerializeStatement(property)}");
        }
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        static void global::MemoryPack.IMemoryPackable<_M>.Deserialize(ref global::MemoryPack.MemoryPackReader reader,scoped ref _M value) {");
        foreach(var property in managedProperties) {
            builder.AppendLine($"            {BuildSchemaManagedDeserializeStatement(property)}");
        }
        builder.AppendLine("        }");
        builder.AppendLine("    }");
    }

    private static string BuildManagedSerializeStatement(IPropertySymbol property) {
        var propertyAccess = $"value.{property.Name}";
        if(property.Type.SpecialType==SpecialType.System_String) {
            return $"writer.WriteString({propertyAccess});";
        }
        return $"writer.WritePackable({propertyAccess});";
    }

    private static string BuildSchemaManagedSerializeStatement(SchemaPropertyDefinition property) {
        var propertyAccess = $"value.{property.Name}";
        if(property.IsString) {
            return $"writer.WriteString({propertyAccess});";
        }
        return $"writer.WritePackable({propertyAccess});";
    }

    private static string BuildManagedDeserializeStatement(IPropertySymbol property) {
        if(property.Type.SpecialType==SpecialType.System_String) {
            return $"value.{property.Name}=reader.ReadString()!;";
        }
        return $"reader.ReadPackable(ref value.{property.Name});";
    }

    private static string BuildSchemaManagedDeserializeStatement(SchemaPropertyDefinition property) {
        if(property.IsString) {
            return $"value.{property.Name}=reader.ReadString()!;";
        }
        return $"reader.ReadPackable(ref value.{property.Name});";
    }

    private static string GetStorageName(IPropertySymbol property) {
        return property.Type.IsUnmanagedType ? "_u" : "_m";
    }

    private static void AppendIndented(StringBuilder builder,string value,string indent) {
        var lines = value.Replace("\r\n","\n").Split('\n');
        foreach(var line in lines) {
            if(line.Length==0) {
                builder.AppendLine();
                continue;
            }

            builder.Append(indent);
            builder.AppendLine(line);
        }
    }

    private sealed class QueryRangeScope {
        public QueryRangeScope(string currentParameterName,IReadOnlyList<string> visibleVariables,IReadOnlyDictionary<string,ExpressionSyntax> rangeExpressions) {
            this.CurrentParameterName=currentParameterName;
            this.VisibleVariables=visibleVariables;
            this.RangeExpressions=rangeExpressions;
        }

        public string CurrentParameterName { get; }

        public IReadOnlyList<string> VisibleVariables { get; }

        public IReadOnlyDictionary<string,ExpressionSyntax> RangeExpressions { get; }

        public static QueryRangeScope CreateIdentity(string rangeVariable) {
            return new QueryRangeScope(
                rangeVariable,
                new[] { rangeVariable },
                new Dictionary<string,ExpressionSyntax>(StringComparer.Ordinal) {
                    [rangeVariable]=SyntaxFactory.IdentifierName(rangeVariable)
                });
        }

        public QueryRangeScope CreateCarrierScope(string currentParameterName,params string[] additionalVariables) {
            var visibleVariables = new List<string>(this.VisibleVariables.Count+additionalVariables.Length);
            visibleVariables.AddRange(this.VisibleVariables);
            visibleVariables.AddRange(additionalVariables);

            var rangeExpressions = new Dictionary<string,ExpressionSyntax>(StringComparer.Ordinal);
            foreach(var visibleVariable in visibleVariables) {
                rangeExpressions[visibleVariable]=SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(currentParameterName),
                    SyntaxFactory.IdentifierName(visibleVariable));
            }

            return new QueryRangeScope(currentParameterName,visibleVariables,rangeExpressions);
        }
    }

    private sealed class QueryRangeVariableRewriter:CSharpSyntaxRewriter {
        private readonly IReadOnlyDictionary<string,ExpressionSyntax> rangeExpressions;
        private readonly Dictionary<string,int> shadowedNames = new(StringComparer.Ordinal);

        public QueryRangeVariableRewriter(IReadOnlyDictionary<string,ExpressionSyntax> rangeExpressions) {
            this.rangeExpressions=rangeExpressions;
        }

        public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node) {
            var parameterName = node.Parameter.Identifier.ValueText;
            this.AddShadowedName(parameterName);
            var result = base.VisitSimpleLambdaExpression(node);
            this.RemoveShadowedName(parameterName);
            return result;
        }

        public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node) {
            this.AddShadowedNames(node.ParameterList.Parameters);
            var result = base.VisitParenthesizedLambdaExpression(node);
            this.RemoveShadowedNames(node.ParameterList.Parameters);
            return result;
        }

        public override SyntaxNode? VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node) {
            if(node.ParameterList is not null) {
                this.AddShadowedNames(node.ParameterList.Parameters);
            }
            var result = base.VisitAnonymousMethodExpression(node);
            if(node.ParameterList is not null) {
                this.RemoveShadowedNames(node.ParameterList.Parameters);
            }
            return result;
        }

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node) {
            var identifier = node.Identifier.ValueText;
            if(this.shadowedNames.ContainsKey(identifier)||!this.rangeExpressions.TryGetValue(identifier,out var replacement)) {
                return base.VisitIdentifierName(node);
            }

            return SyntaxFactory.ParenthesizedExpression(replacement.WithoutTrivia()).WithTriviaFrom(node);
        }

        private void AddShadowedNames(SeparatedSyntaxList<ParameterSyntax> parameters) {
            foreach(var parameter in parameters) {
                this.AddShadowedName(parameter.Identifier.ValueText);
            }
        }

        private void RemoveShadowedNames(SeparatedSyntaxList<ParameterSyntax> parameters) {
            foreach(var parameter in parameters) {
                this.RemoveShadowedName(parameter.Identifier.ValueText);
            }
        }

        private void AddShadowedName(string name) {
            if(this.shadowedNames.TryGetValue(name,out var count)) {
                this.shadowedNames[name]=count+1;
            }
            else {
                this.shadowedNames[name]=1;
            }
        }

        private void RemoveShadowedName(string name) {
            if(!this.shadowedNames.TryGetValue(name,out var count)) {
                return;
            }
            if(count==1) {
                this.shadowedNames.Remove(name);
            }
            else {
                this.shadowedNames[name]=count-1;
            }
        }
    }

    private readonly struct ScalarTerminalInfo {
        public ScalarTerminalInfo(string methodName,string returnTypeName,string accumulatorTypeName,bool returnTypeIsNullable,bool terminalValueIsNullable,bool useCheckedAccumulator,bool castAccumulatorToDoubleBeforeAverageDivide) {
            MethodName=methodName;
            ReturnTypeName=returnTypeName;
            AccumulatorTypeName=accumulatorTypeName;
            ReturnTypeIsNullable=returnTypeIsNullable;
            TerminalValueIsNullable=terminalValueIsNullable;
            UseCheckedAccumulator=useCheckedAccumulator;
            CastAccumulatorToDoubleBeforeAverageDivide=castAccumulatorToDoubleBeforeAverageDivide;
        }

        public string MethodName { get; }
        public string ReturnTypeName { get; }
        public string AccumulatorTypeName { get; }
        public bool ReturnTypeIsNullable { get; }
        public bool TerminalValueIsNullable { get; }
        public bool UseCheckedAccumulator { get; }
        public bool CastAccumulatorToDoubleBeforeAverageDivide { get; }
    }

    private readonly struct ScalarFusionCallInfo {
        public ScalarFusionCallInfo(string methodName,ExpressionSyntax source,ArgumentSyntax[] arguments,string[] typeArguments) {
            MethodName=methodName;
            Source=source;
            Arguments=arguments;
            TypeArguments=typeArguments;
        }

        public string MethodName { get; }
        public ExpressionSyntax Source { get; }
        public ArgumentSyntax[] Arguments { get; }
        public string[] TypeArguments { get; }
    }

    private sealed class ScalarFusionPipeline {
        public ScalarFusionPipeline(string sourceExpression,IReadOnlyList<ScalarFusionOperation> operations) {
            SourceExpression=sourceExpression;
            Operations=operations;
        }

        public string SourceExpression { get; }
        public IReadOnlyList<ScalarFusionOperation> Operations { get; }
    }

    private sealed class ScalarFusionOperation {
        private ScalarFusionOperation(string methodName,ArgumentSyntax[] arguments,string[] typeArguments,FusionLambda? lambda,FusionLambda? secondLambda) {
            MethodName=methodName;
            Arguments=arguments;
            TypeArguments=typeArguments;
            Lambda=lambda;
            SecondLambda=secondLambda;
        }

        public string MethodName { get; }
        public ArgumentSyntax[] Arguments { get; }
        public string[] TypeArguments { get; }
        public FusionLambda? Lambda { get; }
        public FusionLambda? SecondLambda { get; }

        public static ScalarFusionOperation Create(string methodName,ArgumentSyntax[] arguments,string[] typeArguments,FusionLambda? lambda,FusionLambda? secondLambda) {
            return new ScalarFusionOperation(methodName,arguments,typeArguments,lambda,secondLambda);
        }
    }

    private sealed class FusionLambda {
        public FusionLambda(string[] parameters,ExpressionSyntax body) {
            Parameters=parameters;
            Body=body;
        }

        public string[] Parameters { get; }
        public ExpressionSyntax Body { get; }
    }

    private readonly struct FusionLoopContext {
        public FusionLoopContext(string continueLabel,string stopFlag) {
            ContinueLabel=continueLabel;
            StopFlag=stopFlag;
        }

        public string ContinueLabel { get; }
        public string StopFlag { get; }
    }

    private sealed class LambdaParameterInlineRewriter:CSharpSyntaxRewriter {
        private readonly IReadOnlyDictionary<string,ExpressionSyntax> replacements;
        private readonly Dictionary<string,int> shadowedNames = new(StringComparer.Ordinal);

        public LambdaParameterInlineRewriter(IReadOnlyDictionary<string,ExpressionSyntax> replacements) {
            this.replacements=replacements;
        }

        public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node) {
            this.AddShadowedName(node.Parameter.Identifier.ValueText);
            var result = base.VisitSimpleLambdaExpression(node);
            this.RemoveShadowedName(node.Parameter.Identifier.ValueText);
            return result;
        }

        public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node) {
            this.AddShadowedNames(node.ParameterList.Parameters);
            var result = base.VisitParenthesizedLambdaExpression(node);
            this.RemoveShadowedNames(node.ParameterList.Parameters);
            return result;
        }

        public override SyntaxNode? VisitArgument(ArgumentSyntax node) {
            if(node.Parent is TupleExpressionSyntax
                &&node.NameColon is null
                &&node.Expression is IdentifierNameSyntax identifierName
                &&!this.shadowedNames.ContainsKey(identifierName.Identifier.ValueText)
                &&this.replacements.ContainsKey(identifierName.Identifier.ValueText)) {
                var expression = (ExpressionSyntax)this.Visit(node.Expression);
                return node
                    .WithNameColon(SyntaxFactory.NameColon(SyntaxFactory.IdentifierName(identifierName.Identifier.ValueText)))
                    .WithExpression(expression);
            }
            return base.VisitArgument(node);
        }

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node) {
            var identifier = node.Identifier.ValueText;
            if(node.Parent is MemberAccessExpressionSyntax memberAccess&&memberAccess.Name==node) {
                return base.VisitIdentifierName(node);
            }
            if(this.shadowedNames.ContainsKey(identifier)||!this.replacements.TryGetValue(identifier,out var replacement)) {
                return base.VisitIdentifierName(node);
            }
            return SyntaxFactory.ParenthesizedExpression(replacement.WithoutTrivia()).WithTriviaFrom(node);
        }

        private void AddShadowedNames(SeparatedSyntaxList<ParameterSyntax> parameters) {
            foreach(var parameter in parameters) {
                this.AddShadowedName(parameter.Identifier.ValueText);
            }
        }

        private void RemoveShadowedNames(SeparatedSyntaxList<ParameterSyntax> parameters) {
            foreach(var parameter in parameters) {
                this.RemoveShadowedName(parameter.Identifier.ValueText);
            }
        }

        private void AddShadowedName(string name) {
            if(this.shadowedNames.TryGetValue(name,out var count)) {
                this.shadowedNames[name]=count+1;
            }
            else {
                this.shadowedNames[name]=1;
            }
        }

        private void RemoveShadowedName(string name) {
            if(!this.shadowedNames.TryGetValue(name,out var count)) {
                return;
            }
            if(count==1) {
                this.shadowedNames.Remove(name);
            }
            else {
                this.shadowedNames[name]=count-1;
            }
        }
    }

    private sealed class LinqImplementationBodyRewriter:CSharpSyntaxRewriter {
        private readonly IReadOnlyDictionary<string,string> replacements;
        private readonly ExpressionSyntax noElementsExpression;
        private readonly LinqImplementationSource implementation;
        private readonly string targetTypeName;

        public LinqImplementationBodyRewriter(IReadOnlyDictionary<string,string> replacements,ExpressionSyntax noElementsExpression,LinqImplementationSource implementation,string targetTypeName) {
            this.replacements=replacements;
            this.noElementsExpression=noElementsExpression;
            this.implementation=implementation;
            this.targetTypeName=targetTypeName;
        }

        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node) {
            if(node.Expression is IdentifierNameSyntax identifier
                &&identifier.Identifier.ValueText=="NoElements"
                &&node.ArgumentList.Arguments.Count==0) {
                if(this.Visit(this.noElementsExpression.WithoutTrivia()) is ExpressionSyntax expression) {
                    return expression.WithTriviaFrom(node);
                }
                return node;
            }

            if(base.VisitInvocationExpression(node) is not InvocationExpressionSyntax visited) {
                return node;
            }
            if(TryGetSimpleName(visited.Expression,out var methodName)&&this.implementation.HasMethod(methodName)) {
                var memberName = visited.Expression.WithoutTrivia() as SimpleNameSyntax;
                if(memberName is null) {
                    return visited;
                }
                var access = SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.ParseExpression(this.targetTypeName),
                    memberName);
                return visited.WithExpression(access.WithTriviaFrom(visited.Expression));
            }
            return visited;
        }

        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node) {
            if(node.Expression is IdentifierNameSyntax identifier) {
                var replacement = identifier.Identifier.ValueText switch {
                    "ArgumentNullException" => "global::System.ArgumentNullException",
                    "Enumerable" => "global::Seamless.Linq.Enumerable",
                    "HashSet" => "global::Seamless.Linq.HashSet",
                    _ => null,
                };
                if(replacement is not null) {
                    return base.VisitMemberAccessExpression(node.WithExpression(SyntaxFactory.ParseExpression(replacement)));
                }
            }
            return base.VisitMemberAccessExpression(node);
        }

        public override SyntaxNode? VisitObjectCreationExpression(ObjectCreationExpressionSyntax node) {
            if(node.Type is IdentifierNameSyntax identifier&&identifier.Identifier.ValueText=="InvalidOperationException") {
                return base.VisitObjectCreationExpression(node.WithType(SyntaxFactory.ParseTypeName("global::System.InvalidOperationException")));
            }
            return base.VisitObjectCreationExpression(node);
        }

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node) {
            var identifier = node.Identifier.ValueText;
            if(this.replacements.TryGetValue(identifier,out var replacement)) {
                return SyntaxFactory.ParseExpression(replacement).WithTriviaFrom(node);
            }
            return base.VisitIdentifierName(node);
        }

        private static bool TryGetSimpleName(ExpressionSyntax expression,out string name) {
            name=string.Empty;
            if(expression is IdentifierNameSyntax identifier) {
                name=identifier.Identifier.ValueText;
                return !string.IsNullOrWhiteSpace(name);
            }
            if(expression is GenericNameSyntax genericName) {
                name=genericName.Identifier.ValueText;
                return !string.IsNullOrWhiteSpace(name);
            }
            return false;
        }
    }

    private sealed class ToArrayReturnRewriter:CSharpSyntaxRewriter {
        public override SyntaxNode VisitReturnStatement(ReturnStatementSyntax node) {
            if(node.Expression is null) {
                return node;
            }
            var expression = node.Expression.WithoutTrivia();
            var toArray = SyntaxFactory.InvocationExpression(SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                expression,
                SyntaxFactory.IdentifierName("ToArray")));
            return node.WithExpression(toArray.WithTriviaFrom(node.Expression));
        }
    }

    // 同一呼び出し位置に対する interceptor 二重生成を防ぐ comparer。
    private sealed class ContainerMethodInterceptTargetComparer:IEqualityComparer<ContainerMethodInterceptTarget> {
        public static ContainerMethodInterceptTargetComparer Instance { get; } = new();

        public bool Equals(ContainerMethodInterceptTarget? x,ContainerMethodInterceptTarget? y) {
            if(ReferenceEquals(x,y)) {
                return true;
            }
            if(x is null||y is null) {
                return false;
            }
            return x.FilePath==y.FilePath&&x.Line==y.Line&&x.Character==y.Character;
        }

        public int GetHashCode(ContainerMethodInterceptTarget obj) {
            unchecked {
                var hash = 17;
                hash=(hash*31)+obj.FilePath.GetHashCode();
                hash=(hash*31)+obj.Line;
                hash=(hash*31)+obj.Character;
                return hash;
            }
        }
    }

    private sealed class WhereInterceptTargetComparer:IEqualityComparer<WhereInterceptTarget> {
        public static WhereInterceptTargetComparer Instance { get; } = new();

        public bool Equals(WhereInterceptTarget? x,WhereInterceptTarget? y) {
            if(ReferenceEquals(x,y)) {
                return true;
            }
            if(x is null||y is null) {
                return false;
            }
            return x.FilePath==y.FilePath&&x.Line==y.Line&&x.Character==y.Character;
        }

        public int GetHashCode(WhereInterceptTarget obj) {
            unchecked {
                var hash = 17;
                hash=(hash*31)+obj.FilePath.GetHashCode();
                hash=(hash*31)+obj.Line;
                hash=(hash*31)+obj.Character;
                return hash;
            }
        }
    }

    private sealed class LinqInvocationInterceptTargetComparer:IEqualityComparer<LinqInvocationInterceptTarget> {
        public static LinqInvocationInterceptTargetComparer Instance { get; } = new();

        public bool Equals(LinqInvocationInterceptTarget? x,LinqInvocationInterceptTarget? y) {
            if(ReferenceEquals(x,y)) {
                return true;
            }
            if(x is null||y is null) {
                return false;
            }
            return x.InterceptsLocationAttributeText==y.InterceptsLocationAttributeText;
        }

        public int GetHashCode(LinqInvocationInterceptTarget obj) {
            return obj.InterceptsLocationAttributeText.GetHashCode();
        }
    }

    // [Optimize] method は syntax provider 側で個別に拾い、ここで重複除去しながら diagnostic を出す。
    private sealed class OptimizeMethodTargetComparer:IEqualityComparer<OptimizeMethodTarget> {
        public static OptimizeMethodTargetComparer Instance { get; } = new();

        public bool Equals(OptimizeMethodTarget? x,OptimizeMethodTarget? y) {
            if(ReferenceEquals(x,y)) {
                return true;
            }
            if(x is null||y is null) {
                return false;
            }
            return SymbolEqualityComparer.Default.Equals(x.Symbol,y.Symbol);
        }

        public int GetHashCode(OptimizeMethodTarget obj) {
            return SymbolEqualityComparer.Default.GetHashCode(obj.Symbol);
        }
    }

    // InterceptsLocation と generated interceptor の本文生成に必要な情報を集約した DTO。
    private sealed class WhereInterceptTarget {
        public WhereInterceptTarget(string identifier,string filePath,int line,int character,string containingMethodCref,string interceptsLocationAttributeText,string elementTypeName,bool usesIndex,string parameterName,string? indexParameterName,string predicateText,bool usePredicateDelegate) {
            this.Identifier=identifier;
            this.FilePath=filePath;
            this.Line=line;
            this.Character=character;
            this.ContainingMethodCref=containingMethodCref;
            this.InterceptsLocationAttributeText=interceptsLocationAttributeText;
            this.ElementTypeName=elementTypeName;
            this.UsesIndex=usesIndex;
            this.ParameterName=parameterName;
            this.IndexParameterName=indexParameterName;
            this.PredicateText=predicateText;
            this.UsePredicateDelegate=usePredicateDelegate;
        }

        public string Identifier { get; }
        public string FilePath { get; }
        public int Line { get; }
        public int Character { get; }
        public string ContainingMethodCref { get; }
        public string InterceptsLocationAttributeText { get; }
        public string ElementTypeName { get; }
        public bool UsesIndex { get; }
        public string ParameterName { get; }
        public string? IndexParameterName { get; }
        public string PredicateText { get; }
        public bool UsePredicateDelegate { get; }
        public string MethodName => $"OptimizedWhere_{this.Identifier}";
        public string EnumerableTypeName => $"OptimizedWhereEnumerable_{this.Identifier}";
    }

    private sealed class ContainerMethodInterceptTarget {
        public ContainerMethodInterceptTarget(string identifier,string filePath,int line,int character,string containingMethodCref,string interceptsLocationAttributeText,string receiverTypeName,string returnTypeName,string typeParameterList,string typeParameterConstraintList,string parameterList,string body,bool isExpressionBody) {
            this.Identifier=identifier;
            this.FilePath=filePath;
            this.Line=line;
            this.Character=character;
            this.ContainingMethodCref=containingMethodCref;
            this.InterceptsLocationAttributeText=interceptsLocationAttributeText;
            this.ReceiverTypeName=receiverTypeName;
            this.ReturnTypeName=returnTypeName;
            this.TypeParameterList=typeParameterList;
            this.TypeParameterConstraintList=typeParameterConstraintList;
            this.ParameterList=parameterList;
            this.Body=body;
            this.IsExpressionBody=isExpressionBody;
        }

        public string Identifier { get; }
        public string FilePath { get; }
        public int Line { get; }
        public int Character { get; }
        public string ContainingMethodCref { get; }
        public string InterceptsLocationAttributeText { get; }
        public string ReceiverTypeName { get; }
        public string ReturnTypeName { get; }
        public string TypeParameterList { get; }
        public string TypeParameterConstraintList { get; }
        public string ParameterList { get; }
        public string Body { get; }
        public bool IsExpressionBody { get; }
        public string MethodName => $"OptimizedContainerMethod_{this.Identifier}";
    }

    private sealed class LinqInvocationInterceptTarget {
        public LinqInvocationInterceptTarget(string identifier,string filePath,string containingMethodCref,string interceptsLocationAttributeText,string returnTypeName,string typeParameterList,string typeParameterConstraintList,string parameterList,string body,bool isExpressionBody,string? staticLambdaStrategySource = null) {
            this.Identifier=identifier;
            this.FilePath=filePath;
            this.ContainingMethodCref=containingMethodCref;
            this.InterceptsLocationAttributeText=interceptsLocationAttributeText;
            this.ReturnTypeName=returnTypeName;
            this.TypeParameterList=typeParameterList;
            this.TypeParameterConstraintList=typeParameterConstraintList;
            this.ParameterList=parameterList;
            this.Body=body;
            this.IsExpressionBody=isExpressionBody;
            this.StaticLambdaStrategySource=staticLambdaStrategySource;
        }

        public string Identifier { get; }
        public string FilePath { get; }
        public string ContainingMethodCref { get; }
        public string InterceptsLocationAttributeText { get; }
        public string ReturnTypeName { get; }
        public string TypeParameterList { get; }
        public string TypeParameterConstraintList { get; }
        public string ParameterList { get; }
        public string Body { get; }
        public bool IsExpressionBody { get; }
        public string? StaticLambdaStrategySource { get; }
        public string MethodName => $"OptimizedLinqInvocation_{this.Identifier}";
    }

    private sealed class LinqInvocationLambdaStrategy {
        public LinqInvocationLambdaStrategy(string kind,string typeName,string resultTypeName,string expressionText,string source) {
            this.Kind=kind;
            this.TypeName=typeName;
            this.ResultTypeName=resultTypeName;
            this.ExpressionText=expressionText;
            this.Source=source;
        }

        public string Kind { get; }
        public string TypeName { get; }
        public string ResultTypeName { get; }
        public string ExpressionText { get; }
        public string Source { get; }
    }

    private sealed class MovableStaticLambdaBodyChecker:CSharpSyntaxWalker {
        private readonly string parameterName;

        public MovableStaticLambdaBodyChecker(string parameterName) {
            this.parameterName=parameterName;
        }

        public bool CanMove { get; private set; } = true;

        public override void VisitIdentifierName(IdentifierNameSyntax node) {
            if(!this.CanMove) {
                return;
            }
            if(node.Identifier.ValueText==this.parameterName) {
                base.VisitIdentifierName(node);
                return;
            }
            if(node.Parent is MemberAccessExpressionSyntax memberAccess&&ReferenceEquals(memberAccess.Name,node)) {
                base.VisitIdentifierName(node);
                return;
            }
            if(node.Parent is QualifiedNameSyntax qualifiedName&&ReferenceEquals(qualifiedName.Right,node)) {
                base.VisitIdentifierName(node);
                return;
            }
            this.CanMove=false;
        }
    }

    private sealed class InterceptorSourceBucket {
        public InterceptorSourceBucket(string filePath) {
            FilePath=filePath;
        }

        public string FilePath { get; }
        public List<WhereInterceptTarget> WhereTargets { get; } = new();
        public List<ContainerMethodInterceptTarget> ContainerMethodTargets { get; } = new();
        public List<LinqInvocationInterceptTarget> LinqInvocationTargets { get; } = new();
    }

    private sealed class OptimizeMethodTarget {
        public OptimizeMethodTarget(IMethodSymbol symbol,Location location) {
            Symbol=symbol;
            Location=location;
        }

        public IMethodSymbol Symbol { get; }
        public Location Location { get; }
    }

    private sealed class LinqImplementationSource {
        public LinqImplementationSource(IReadOnlyList<MethodDeclarationSyntax> methods,ExpressionSyntax noElementsExpression) {
            Methods=methods;
            NoElementsExpression=noElementsExpression;
        }

        public IReadOnlyList<MethodDeclarationSyntax> Methods { get; }
        public ExpressionSyntax NoElementsExpression { get; }

        public bool HasMethod(string name) {
            return this.Methods.Any(method => method.Identifier.ValueText==name);
        }
    }

    // DatabaseGenerator マーカー配下で見つけた型宣言の最小情報。
    private sealed class TypeTarget {
        public TypeTarget(INamedTypeSymbol symbol,bool isPartial) {
            this.Symbol=symbol;
            this.IsPartial=isPartial;
        }

        public INamedTypeSymbol Symbol { get; }
        public bool IsPartial { get; }
    }

    private sealed class SchemaRecordDefinition {
        public SchemaRecordDefinition(string rootNamespace,string namespaceName,string name,ImmutableArray<SchemaPropertyDefinition> properties) {
            this.NamespaceName=namespaceName;
            this.Name=name;
            this.Properties=properties;
            this.FullTypeName=string.IsNullOrEmpty(rootNamespace)
                ? $"global::{namespaceName}.{name}"
                : $"global::{rootNamespace}.{namespaceName}.{name}";
        }

        public string NamespaceName { get; }
        public string Name { get; }
        public ImmutableArray<SchemaPropertyDefinition> Properties { get; }
        public string FullTypeName { get; }
    }

    private readonly struct SchemaPropertyDefinition {
        public SchemaPropertyDefinition(string typeName,string name) {
            this.TypeName=typeName;
            this.Name=name;
        }

        public string TypeName { get; }
        public string Name { get; }
        public bool IsString => this.TypeName=="string"||this.TypeName=="global::System.String";
        public bool IsUnmanaged => this.TypeName is "bool" or "byte" or "sbyte" or "short" or "ushort" or "int" or "uint" or "long" or "ulong" or "float" or "double" or "decimal" or "char";
    }

}
