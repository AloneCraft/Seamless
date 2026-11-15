using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
namespace Shared.Database.Generation;

internal sealed partial class DatabaseSchemaGeneration:IIncrementalGenerator {
    private static readonly DiagnosticDescriptor InvalidRecordDeclaration=new(
        "SDB003","Invalid database record declaration","Database record '{0}': {1}",
        "Seamless.Database.Generator",DiagnosticSeverity.Error,isEnabledByDefault:true);
    private static readonly SymbolDisplayFormat FullTypeFormat=new(
        globalNamespaceStyle:SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle:SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions:SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions:SymbolDisplayMiscellaneousOptions.ExpandNullable
            |SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
            |SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var targets=context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node,_)=>node is TypeDeclarationSyntax,
                static (syntaxContext,_)=>CreateTarget(syntaxContext))
            .Where(static target=>target is not null)
            .Select(static (target,_)=>target!);

        context.RegisterSourceOutput(targets.Collect(),static (productionContext,collected)=>Generate(productionContext,collected));
    }

    private static TypeTarget? CreateTarget(GeneratorSyntaxContext context) {
        var declaration=(TypeDeclarationSyntax)context.Node;
        if(context.SemanticModel.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol) {
            return null;
        }
        var compilation=context.SemanticModel.Compilation;
        return new(symbol,declaration.Modifiers.Any(SyntaxKind.PartialKeyword),
            compilation.GetTypeByMetadataName("Seamless.Database.Client.NativeWireLayoutAttribute") is not null,
            compilation.GetTypeByMetadataName("Seamless.Database.Client.INativeReplayDecoder`1") is not null,
            compilation.GetTypeByMetadataName("Seamless.Database.GeneratedRecordConstructorAttribute") is not null);
    }

    private static void Generate(SourceProductionContext context,ImmutableArray<TypeTarget> targets) {
        if(targets.IsDefaultOrEmpty) {
            return;
        }
        var uniqueTargets=targets
            .GroupBy(static target=>target.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),StringComparer.Ordinal)
            .Select(static group=>new TypeTarget(group.First().Symbol,group.Any(static target=>target.IsPartial),
                group.Any(static target=>target.NativeWire),group.Any(static target=>target.ReplayAvailable),group.Any(static target=>target.ConstructorMetadata)))
            .ToArray();
        var markerNamespaces=uniqueTargets
            .Where(static target=>target.Symbol.Name=="DatabaseGenerator")
            .Select(static target=>NamespaceName(target.Symbol.ContainingNamespace))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if(markerNamespaces.Length==0) {
            return;
        }
        var collectibleNamespaces=uniqueTargets
            .Where(static target=>target.Symbol.Name=="DatabaseGenerator"&&target.Symbol.GetMembers("Collectible").OfType<IFieldSymbol>().Any(static field=>field.IsConst&&field.ConstantValue is true))
            .Select(static target=>NamespaceName(target.Symbol.ContainingNamespace)).ToArray();
        // 行の型は <根>.Tables に宣言し、キーの型 <根>.Keys は生成する(Keys と Records に2回書く旧方式は消した)。
        var constructorMetadata=uniqueTargets.Any(static target=>target.ConstructorMetadata);
        GenerateDeclaredRecords(context,uniqueTargets,markerNamespaces,constructorMetadata,collectibleNamespaces,
            targets.Any(static target=>target.NativeWire),targets.Any(static target=>target.ReplayAvailable));
    }

    // collectibleRoots: DatabaseGenerator.Collectible を true にした根(Manager の実行時コンパイル・Gateway の実行時SQL)。
    // nativeWire/replayAvailable: 型から Seamless.Database.Client の通信用の配置の属性・再生用の読み取りの契約が見えるか。
    private static void GenerateDeclaredRecords(SourceProductionContext context,TypeTarget[] targets,string[] roots,bool constructorMetadata,
        string[] collectibleRoots,bool nativeWire,bool replayAvailable){
        foreach(var target in targets.OrderBy(static target=>target.Symbol.ToDisplayString(),StringComparer.Ordinal)){
            var symbol=target.Symbol;
            var recordNamespace=NamespaceName(symbol.ContainingNamespace);
            var root=roots.FirstOrDefault(candidate=>{
                var prefix=candidate.Length==0?"Tables":candidate+".Tables";
                return recordNamespace==prefix||recordNamespace.StartsWith(prefix+".",StringComparison.Ordinal);
            });
            if(root is null){continue;}
            var tableNamespace=root.Length==0?"Tables":root+".Tables";
            var keyNamespace=(root.Length==0?"Keys":root+".Keys")+recordNamespace.Substring(tableNamespace.Length);
            var properties=symbol.GetMembers().OfType<IPropertySymbol>()
                .Where(static property=>!property.IsStatic&&!property.IsImplicitlyDeclared).ToImmutableArray();
            var invalidShape=!target.IsPartial||symbol.TypeKind!=TypeKind.Struct||symbol.Arity!=0
                ||symbol.ContainingType is not null||symbol.IsRefLikeType||properties.IsEmpty;
            var invalidProperties=properties.Any(static property=>!IsPartialDefinition(property)
                ||property.DeclaredAccessibility!=Accessibility.Public||property.IsIndexer||property.RefKind!=RefKind.None
                ||property.Type.IsRefLikeType||property.Type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer)
                ||symbol.GetMembers().OfType<IPropertySymbol>().Any(static property=>property.IsStatic&&IsKeyColumn(property));
            var instanceFields=symbol.GetMembers().OfType<IFieldSymbol>().Any(static field=>!field.IsStatic&&!field.IsImplicitlyDeclared);
            var reserved=symbol.GetMembers().Any(static member=>!member.IsImplicitlyDeclared
                &&member.Name is "Key" or "_Key" or "_C" or "_S" or "C" or "S" or "Create" or "__NormalizeScalars" or "__ReadColumns" or "Strategy" or "Equals" or "GetHashCode");
            var duplicateKey=targets.Any(candidate=>candidate.Symbol.ContainingType is null&&candidate.Symbol.Arity==0&&candidate.Symbol.Name==symbol.Name
                &&NamespaceName(candidate.Symbol.ContainingNamespace)==keyNamespace);
            if(invalidShape||invalidProperties||instanceFields||reserved||duplicateKey){
                var reason="Use a non-generic top-level partial struct with public instance partial get or get/init data properties.";
                if(instanceFields){reason="Instance fields cannot be persisted by this declaration generator. Use partial data properties.";}
                if(reserved){reason="The declaration conflicts with a generated storage, factory or strategy member name.";}
                if(duplicateKey){reason="The generated key type conflicts with an existing type in "+keyNamespace+".";}
                context.ReportDiagnostic(Diagnostic.Create(InvalidRecordDeclaration,symbol.Locations.FirstOrDefault(),symbol.ToDisplayString(),reason));
                continue;
            }
            var keys=properties.Where(static property=>IsKeyColumn(property)).ToImmutableArray();
            if(keys.IsEmpty){keys=properties;}
            var values=properties.Where(property=>!keys.Contains(property,SymbolEqualityComparer.Default)).ToImmutableArray();
            var builder=new StringBuilder("// <auto-generated/>\n#nullable enable\n");
            var keyType="global::"+keyNamespace+".@"+symbol.Name;
            var options=new DeclaredOptions(constructorMetadata,collectibleRoots.Contains(root,StringComparer.Ordinal),nativeWire,replayAvailable);
            AppendDeclaredType(builder,symbol,keyNamespace,keys,keys,null,options);
            AppendDeclaredType(builder,symbol,recordNamespace,properties,values,keyType,options);
            context.AddSource(symbol.ToDisplayString()+".Record.g.cs",SourceText.From(builder.ToString(),Encoding.UTF8));
        }
    }

    private static bool IsKeyColumn(IPropertySymbol property)=>property.GetAttributes().Any(static attribute=>
        attribute.AttributeClass?.ToDisplayString()=="Seamless.Database.KeyAttribute");

    /// <param name="ConstructorMetadata">利用者の型から Seamless.Database の GeneratedRecordConstructorAttribute が見えるか。</param>
    /// <param name="Collectible">回収できる型として作るか。MemoryPack の共通の登録先(解除できない)へ型を登録しない。</param>
    /// <param name="NativeWire">通信用の配置の属性(NativeWireLayout)が見えるか。</param>
    /// <param name="ReplayAvailable">再生用の読み取りの契約(INativeReplayDecoder)が見えるか。</param>
    private sealed record DeclaredOptions(bool ConstructorMetadata,bool Collectible,bool NativeWire,bool ReplayAvailable);

    private static void AppendDeclaredType(StringBuilder builder,INamedTypeSymbol symbol,string namespaceName,
        ImmutableArray<IPropertySymbol> properties,ImmutableArray<IPropertySymbol> stored,string? keyType,DeclaredOptions options){
        var isKey=keyType is null;
        var name="@"+symbol.Name;
        var type="global::"+namespaceName+"."+name;
        var keyProperties=properties.Where(property=>!stored.Contains(property,SymbolEqualityComparer.Default)).ToImmutableArray();
        // [生成/引数名] 列の名前を引数名にする(外部キーの検査などがキーのコンストラクターを列の名前で探す)。キーの引数は列 key と重ならない名前にする。
        var keyParameter=stored.Any(static property=>property.Name=="key")?"__key":"key";
        var parameters=stored.Select(static property=>TypeName(property.Type)+" @"+property.Name).ToList();
        var arguments=stored.Select(static property=>"@"+property.Name).ToList();
        if(!isKey){parameters.Insert(0,keyType+" "+keyParameter);arguments.Insert(0,keyParameter);}
        var memoryPack=!options.Collectible;
        var wire=options.NativeWire&&!options.Collectible;
        var replay=wire&&options.ReplayAvailable&&properties.All(static property=>IsDeclaredReplayValue(property.Type));
        builder.AppendLine($"namespace {namespaceName}{{");
        var accessibility=symbol.DeclaredAccessibility==Accessibility.Public?"public":"internal";
        var modifiers=isKey?"readonly struct":(symbol.IsReadOnly?"readonly ":string.Empty)+"partial "+(symbol.IsRecord?"record ":string.Empty)+"struct";
        var contracts=new List<string>();
        if(memoryPack){contracts.Add($"global::MemoryPack.IMemoryPackable<{type}>");}
        contracts.Add($"global::System.IEquatable<{type}>");
        if(!isKey){contracts.Add($"global::Seamless.Collections.IRecord<{keyType}>");}
        if(replay){contracts.Add($"global::Seamless.Database.Client.INativeReplayDecoder<{type}>");}
        // [通信/配置] 直列化は宣言した列の順に1つずつ書く(AppendDeclaredFormatter)。別プロセスの業務クライアントは、この順で受け取った行を事前検査する。
        if(wire){builder.AppendLine($"[global::Seamless.Database.Client.NativeWireLayoutAttribute(1,new string[]{{{string.Join(",",properties.Select(static property=>global::Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(property.Name,true)))}}})]");}
        builder.AppendLine($"{accessibility} {modifiers} {name}:{string.Join(",",contracts)}{{");
        if(!isKey){
            builder.AppendLine($"    private readonly {keyType} _Key;");
            builder.AppendLine($"    public {keyType} Key{{get=>this._Key;init=>this._Key={keyType}.__NormalizeScalars(value);}}");
        }
        AppendSeparatedFields(builder,stored,"    ");
        var constructorMembers=stored.Select(static property=>global::Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(property.Name,true)).ToList();
        if(!isKey){constructorMembers.Insert(0,"\"Key\"");}
        if(options.ConstructorMetadata){builder.AppendLine($"    [global::Seamless.Database.GeneratedRecordConstructorAttribute({string.Join(",",constructorMembers)})]");}
        builder.AppendLine($"    public {name}({string.Join(",",parameters)}){{");
        builder.AppendLine("        this=default;");
        if(!isKey){builder.AppendLine($"        this._Key={keyType}.__NormalizeScalars({keyParameter});");}
        foreach(var references in new[]{true,false}){
            var fields=stored.Where(property=>Storage(property.Type,0,true).References==references).ToArray();
            if(fields.Length==0){continue;}
            var group=references?"C":"S";
            builder.AppendLine($"        this._{group}=new {group}{{{string.Join(",",fields.Select(static property=>"@"+property.Name+"="+NormalizeScalar(property.Type,"@"+property.Name)))}}};");
        }
        builder.AppendLine("    }");
        builder.AppendLine($"    public static {type} Create({string.Join(",",parameters)})=>new({string.Join(",",arguments)});");
        if(isKey){
            builder.AppendLine($"    internal static {type} __NormalizeScalars({type} value)=>new({string.Join(",",properties.Select(static property=>"value.@"+property.Name))});");
        }
        foreach(var property in properties){
            var isKeyColumn=!isKey&&keyProperties.Contains(property,SymbolEqualityComparer.Default);
            var group=Storage(property.Type,0,true).References?"C":"S";
            var field=isKeyColumn?"_Key.@"+property.Name:"_"+group+".@"+property.Name;
            var accessors=property.DeclaringSyntaxReferences.Select(static reference=>reference.GetSyntax()).OfType<PropertyDeclarationSyntax>()
                .SelectMany(static declaration=>declaration.AccessorList?.Accessors??default).ToArray();
            var getterModifiers=isKey?string.Empty:accessors.First(static accessor=>accessor.IsKind(SyntaxKind.GetAccessorDeclaration)).Modifiers.ToString();
            var init=accessors.FirstOrDefault(static accessor=>accessor.IsKind(SyntaxKind.InitAccessorDeclaration));
            var partial=isKey?string.Empty:"partial ";
            var getter="this."+field;
            // [計算列] 回収できる型(Gateway の実行時SQL)では、Gateway が式を登録していれば読むたびに計算し直す。登録が無ければ保存した値を返す。
            if(!isKey&&options.Collectible&&property.GetAttributes().FirstOrDefault(static attribute=>attribute.AttributeClass?.ToDisplayString()=="Seamless.Database.ComputedColumnAttribute") is{} computed){
                getter=$"global::Seamless.Database.GeneratedRowExpression<{type}>.Read<{TypeName(property.Type)}>(in this,{computed.ConstructorArguments[0].Value},this.{field})";
            }
            builder.AppendLine($"    public {partial}{TypeName(property.Type)} @{property.Name}{{{getterModifiers} get=>{getter};");
            if(isKey||init is not null){
                var initModifiers=isKey?string.Empty:init!.Modifiers.ToString();
                if(isKeyColumn){builder.AppendLine($"        {initModifiers} init=>this._Key=this._Key with{{@{property.Name}=value}};");}
                else{
                    builder.AppendLine($"        {initModifiers} init{{var fields=this._{group};fields.@{property.Name}={NormalizeScalar(property.Type,"value")};this._{group}=fields;}}");
                }
            }
            builder.AppendLine("    }");
        }
        AppendDeclaredStrategy(builder,symbol,type,properties,stored,keyType);
        if(memoryPack){AppendDeclaredFormatter(builder,type,name,properties,keyProperties,keyType);}
        if(replay){AppendDeclaredReplay(builder,type);}
        builder.AppendLine("}");
        builder.AppendLine("}");
    }

    // 再生用の読み取りができる列の型：基本の値(null 許容を含む)・文字列、それらの1次元配列。直列化(列ごとの WriteValue)の逆をそのまま読める形だけ。
    private static bool IsDeclaredReplayValue(ITypeSymbol type){
        if(type is IArrayTypeSymbol{Rank:1} array){return IsCanonicalScalar(array.ElementType)&&!TryGetNullableValueType(array.ElementType,out _);}
        return IsCanonicalScalar(type);
    }

    private static void AppendDeclaredReplay(StringBuilder builder,string type){
        builder.AppendLine($"    static {type} global::Seamless.Database.Client.INativeReplayDecoder<{type}>.Decode(global::System.ReadOnlySpan<byte> payload){{");
        builder.AppendLine("        // 参照を含まない行は MemoryPack が行のメモリをそのまま書く。参照を含む行は宣言した列の順に1つずつ書いたものを読む。");
        builder.AppendLine($"        if(!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{type}>()){{");
        builder.AppendLine($"            if(payload.Length!=global::System.Runtime.CompilerServices.Unsafe.SizeOf<{type}>()) throw new global::System.IO.InvalidDataException(\"Invalid native replay row length.\");");
        builder.AppendLine($"            return global::System.Runtime.InteropServices.MemoryMarshal.Read<{type}>(payload);");
        builder.AppendLine("        }");
        builder.AppendLine("        var reader=new global::MemoryPack.MemoryPackReader(payload,null!);");
        builder.AppendLine("        try{");
        builder.AppendLine("            var value=__ReadColumns(ref reader);");
        builder.AppendLine("            if(reader.Consumed!=payload.Length) throw new global::System.IO.InvalidDataException(\"Invalid native replay row length.\");");
        builder.AppendLine("            return value;");
        builder.AppendLine("        }finally{reader.Dispose();}");
        builder.AppendLine("    }");
    }

    private static void AppendDeclaredStrategy(StringBuilder builder,INamedTypeSymbol symbol,string type,
        ImmutableArray<IPropertySymbol> properties,ImmutableArray<IPropertySymbol> stored,string? keyType){
        var scalarOnly=properties.All(static property=>IsCanonicalScalar(property.Type));
        var contract="seamless-record-values-v1|"+type+"|"+string.Join("|",properties.Select(property=>
            property.Name+":"+TypeName(property.Type)+":"+(keyType is null||!stored.Contains(property,SymbolEqualityComparer.Default)?"key":"value")));
        var contractLiteral=global::Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(contract,true);
        builder.AppendLine($"    public readonly bool Equals({type} other)=>Strategy.Equals(this,other);");
        builder.AppendLine("    public override readonly int GetHashCode()=>Strategy.GetHashCode(this);");
        if(keyType is null||!symbol.IsRecord){
            builder.AppendLine($"    public override readonly bool Equals(object? value)=>value is {type} other&&this.Equals(other);");
            builder.AppendLine($"    public static bool operator ==({type} first,{type} second)=>Strategy.Equals(first,second);");
            builder.AppendLine($"    public static bool operator !=({type} first,{type} second)=>!Strategy.Equals(first,second);");
        }
        builder.AppendLine($"    public readonly struct Strategy:global::Seamless.Collections.IRecordStrategy<{type}>{{");
        builder.AppendLine($"        public static string ContractId=>{contractLiteral};");
        builder.AppendLine($"        public static bool IsGraphFree=>{(scalarOnly?"true":"false")};");
        if(scalarOnly){
            var arguments=stored.Select(static property=>"value.@"+property.Name).ToList();
            if(keyType is not null){arguments.Insert(0,"value.Key");}
            builder.AppendLine($"        public static {type} Normalize({type} value)=>new({string.Join(",",arguments)});");
            var comparisons=properties.Select(static property=>$"global::Seamless.Execution.RecordValueStrategy<{TypeName(property.Type)}>.Equals(first.@{property.Name},second.@{property.Name})");
            builder.AppendLine($"        public static bool Equals({type} first,{type} second)=>{string.Join("&&",comparisons)};");
            builder.AppendLine($"        public static int GetHashCode({type} value){{");
            builder.AppendLine("            var hash=unchecked((int)2166136261);");
            foreach(var property in properties){
                builder.AppendLine($"            hash=unchecked((hash^global::Seamless.Execution.RecordValueStrategy<{TypeName(property.Type)}>.GetHashCode(value.@{property.Name}))*16777619);");
            }
            builder.AppendLine("            return hash;");
            builder.AppendLine("        }");
        }else{
            builder.AppendLine($"        public static {type} Normalize({type} value)=>global::Seamless.Execution.RecordValueStrategy<{type}>.Normalize(value);");
            builder.AppendLine($"        public static bool Equals({type} first,{type} second)=>global::Seamless.Execution.RecordValueStrategy<{type}>.Equals(first,second);");
            builder.AppendLine($"        public static int GetHashCode({type} value)=>global::Seamless.Execution.RecordValueStrategy<{type}>.GetHashCode(value);");
        }
        builder.AppendLine("    }");
    }

    private static bool IsCanonicalScalar(ITypeSymbol type){
        if(TryGetNullableValueType(type,out var underlying)){type=underlying;}
        return type.TypeKind==TypeKind.Enum||type.SpecialType is SpecialType.System_Boolean or SpecialType.System_Byte
            or SpecialType.System_SByte or SpecialType.System_Char or SpecialType.System_Int16 or SpecialType.System_UInt16
            or SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64
            or SpecialType.System_IntPtr or SpecialType.System_UIntPtr or SpecialType.System_Single or SpecialType.System_Double
            or SpecialType.System_Decimal or SpecialType.System_DateTime or SpecialType.System_String
            ||type.ToDisplayString() is "System.DateTimeOffset" or "System.Half" or "System.Guid" or "System.TimeSpan"
                or "System.DateOnly" or "System.TimeOnly" or "System.Int128" or "System.UInt128";
    }

    private static string NormalizeScalar(ITypeSymbol type,string expression){
        if(TryGetNullableValueType(type,out var underlying)){type=underlying;}
        if(type.SpecialType is SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal or SpecialType.System_DateTime
            ||type.ToDisplayString() is "System.DateTimeOffset" or "System.Half"){
            return "global::Seamless.Collections.ScalarNormalization.Normalize("+expression+")";
        }
        return expression;
    }

    private static void AppendDeclaredFormatter(StringBuilder builder,string type,string name,
        ImmutableArray<IPropertySymbol> properties,ImmutableArray<IPropertySymbol> keys,string? keyType){
        builder.AppendLine($"    static {name}()=>global::MemoryPack.MemoryPackFormatterProvider.Register<{type}>();");
        builder.AppendLine("    static void global::MemoryPack.IMemoryPackFormatterRegister.RegisterFormatter(){");
        builder.AppendLine($"        if(!global::MemoryPack.MemoryPackFormatterProvider.IsRegistered<{type}>()){{global::MemoryPack.MemoryPackFormatterProvider.Register(new global::MemoryPack.Formatters.MemoryPackableFormatter<{type}>());}}");
        builder.AppendLine($"        if(!global::MemoryPack.MemoryPackFormatterProvider.IsRegistered<{type}[]>()){{global::MemoryPack.MemoryPackFormatterProvider.Register(new global::MemoryPack.Formatters.ArrayFormatter<{type}>());}}");
        builder.AppendLine("    }");
        // [直列化/通信] 列ごとの書き方は通信用の配置の約束(NativeWireLayout、Seamless.Database.Client の事前検査)に合わせる：
        // null 許容は有無の1バイトと値、文字列・配列は専用の書き方、参照を含まない値はメモリのまま、それ以外は MemoryPack の型として書く。
        builder.AppendLine($"    static void global::MemoryPack.IMemoryPackable<{type}>.Serialize<TBufferWriter>(ref global::MemoryPack.MemoryPackWriter<TBufferWriter> writer,scoped ref {type} value){{");
        for(var index=0;index<properties.Length;index++){builder.AppendLine($"        {WriteDeclaredColumn(properties[index],index)}");}
        builder.AppendLine("    }");
        builder.AppendLine($"    static void global::MemoryPack.IMemoryPackable<{type}>.Deserialize(ref global::MemoryPack.MemoryPackReader reader,scoped ref {type} value)=>value=__ReadColumns(ref reader);");
        // 宣言した列の順に読み、行を組み立てる。MemoryPack の読み取りと、再生用の読み取り(INativeReplayDecoder)が共有する。
        builder.AppendLine($"    private static {type} __ReadColumns(ref global::MemoryPack.MemoryPackReader reader){{");
        for(var index=0;index<properties.Length;index++){
            builder.AppendLine($"        {ReadDeclaredColumn(properties[index],index)}");
        }
        var arguments=properties.Select((property,index)=>(Property:property,Argument:"value"+index)).ToArray();
        var values=arguments.Where(item=>!keys.Contains(item.Property,SymbolEqualityComparer.Default)).Select(static item=>item.Argument).ToList();
        if(keyType is not null){
            var keyArguments=arguments.Where(item=>keys.Contains(item.Property,SymbolEqualityComparer.Default)).Select(static item=>item.Argument);
            values.Insert(0,keyType+".Create("+string.Join(",",keyArguments)+")");
        }
        builder.AppendLine($"        return new {type}({string.Join(",",values)});");
        builder.AppendLine("    }");
    }

    // 参照を含まない値か。生成器が中身を作る型(部分宣言のプロパティを持つ型)は、生成前のシンボルでは中身が空に見えるので、宣言した列の型までたどる。
    private static bool IsDeclaredUnmanaged(ITypeSymbol type,int depth=0){
        if(depth>=32||!type.IsUnmanagedType){return false;}
        if(type is not INamedTypeSymbol named){return true;}
        var properties=GetProperties(named);
        return properties.IsEmpty||properties.All(property=>IsDeclaredUnmanaged(property.Type,depth+1));
    }

    private static string WriteDeclaredColumn(IPropertySymbol property,int index){
        var value="value.@"+property.Name;
        if(TryGetNullableValueType(property.Type,out var underlying)){
            var write="writer.WriteValue(item"+index+");";if(IsDeclaredUnmanaged(underlying)){write="writer.WriteUnmanaged(item"+index+");";}
            return $"var nullable{index}={value};writer.WriteUnmanaged(nullable{index}.HasValue);if(nullable{index}.HasValue){{var item{index}=nullable{index}.GetValueOrDefault();{write}}}";
        }
        if(property.Type.SpecialType==SpecialType.System_String){return $"writer.WriteString({value});";}
        if(property.Type is IArrayTypeSymbol){return $"writer.WriteArray({value});";}
        if(IsDeclaredUnmanaged(property.Type)){return $"writer.WriteUnmanaged({value});";}
        return $"writer.WriteValue({value});";
    }

    private static string ReadDeclaredColumn(IPropertySymbol property,int index){
        var type=TypeName(property.Type);
        if(TryGetNullableValueType(property.Type,out var underlying)){
            var underlyingType=TypeName(underlying);
            var read=$"var item{index}=reader.ReadValue<{underlyingType}>();";if(IsDeclaredUnmanaged(underlying)){read=$"reader.ReadUnmanaged(out {underlyingType} item{index});";}
            return $"{type} value{index}=null;reader.ReadUnmanaged(out bool hasValue{index});if(hasValue{index}){{{read}value{index}=item{index};}}";
        }
        if(property.Type.SpecialType==SpecialType.System_String){return $"var value{index}=reader.ReadString()!;";}
        if(property.Type is IArrayTypeSymbol array){return $"{TypeName(array.ElementType)}[]? read{index}=null;reader.ReadArray(ref read{index});var value{index}=read{index}!;";}
        if(IsDeclaredUnmanaged(property.Type)){return $"reader.ReadUnmanaged(out {type} value{index});";}
        return $"var value{index}=reader.ReadValue<{type}>()!;";
    }

    private static ImmutableArray<IPropertySymbol> GetProperties(INamedTypeSymbol symbol) {
        var builder=ImmutableArray.CreateBuilder<IPropertySymbol>();
        foreach(var property in symbol.GetMembers().OfType<IPropertySymbol>()) {
            if(IsPartialDefinition(property)) {
                builder.Add(property);
            }
        }
        return builder.ToImmutable();
    }

    private static bool IsPartialDefinition(IPropertySymbol property) {
        foreach(var reference in property.DeclaringSyntaxReferences) {
            if(reference.GetSyntax() is not PropertyDeclarationSyntax syntax
                ||!syntax.Modifiers.Any(SyntaxKind.PartialKeyword)
                ||syntax.ExpressionBody is not null
                ||syntax.AccessorList is not {} accessorList
                ||accessorList.Accessors.Count is <1 or >2) {
                continue;
            }
            if(accessorList.Accessors.Any(static accessor=>accessor.IsKind(SyntaxKind.GetAccessorDeclaration))
                &&accessorList.Accessors.All(static accessor=>
                    (accessor.IsKind(SyntaxKind.GetAccessorDeclaration)||accessor.IsKind(SyntaxKind.InitAccessorDeclaration))
                    &&accessor.Body is null&&accessor.ExpressionBody is null)) {
                return true;
            }
        }
        return false;
    }

    private static bool TryGetNullableValueType(ITypeSymbol type,out ITypeSymbol underlyingType) {
        if(type is INamedTypeSymbol { IsGenericType:true } named
            &&named.OriginalDefinition.SpecialType==SpecialType.System_Nullable_T) {
            underlyingType=named.TypeArguments[0];
            return true;
        }
        underlyingType=type;
        return false;
    }

    private static string TypeName(ITypeSymbol type)=>type.ToDisplayString(FullTypeFormat);

    private static string NamespaceName(INamespaceSymbol symbol)=>symbol.IsGlobalNamespace?string.Empty:symbol.ToDisplayString();

    /// <param name="ConstructorMetadata">利用者の型から Seamless.Database の GeneratedRecordConstructorAttribute が見えるか。クライアント側だけの schema(Seamless.Database.Client だけを参照)には無いので付けない。</param>
    private sealed record TypeTarget(INamedTypeSymbol Symbol,bool IsPartial,bool NativeWire,bool ReplayAvailable,bool ConstructorMetadata);
}
