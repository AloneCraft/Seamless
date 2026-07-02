namespace UI;

internal readonly record struct ScalableBenchmarkParameters(
    long IndexSize,
    long PageSize,
    long SegmentSize,
    long MemorySize,
    double MutableFraction);

internal enum ScalableBenchmarkArgumentColumn{
    Count,
    Index,
    Page,
    Segment,
    Memory,
    Fraction,
    Initial,
    Data,
}

internal static class BenchmarkArgumentColumns{
    private static readonly ScalableBenchmarkArgumentColumn[] PersistedColumns = [
        ScalableBenchmarkArgumentColumn.Count,
        ScalableBenchmarkArgumentColumn.Initial,
        ScalableBenchmarkArgumentColumn.Data,
    ];
    private static readonly ScalableBenchmarkArgumentColumn[] TsavoriteColumns = [
        ScalableBenchmarkArgumentColumn.Count,
        ScalableBenchmarkArgumentColumn.Index,
        ScalableBenchmarkArgumentColumn.Page,
        ScalableBenchmarkArgumentColumn.Segment,
        ScalableBenchmarkArgumentColumn.Memory,
        ScalableBenchmarkArgumentColumn.Fraction,
    ];
    private static readonly ScalableBenchmarkArgumentColumn[] EmptyColumns = [];

    public static IReadOnlyList<ScalableBenchmarkArgumentColumn> Get(BenchmarkSourceTabDefinition sourceTab) =>
        Get(sourceTab.Tab);

    public static IReadOnlyList<ScalableBenchmarkArgumentColumn> Get(BenchmarkSourceTab sourceTab) =>
        sourceTab switch{
            BenchmarkSourceTab.ScalablePersistedList
                or BenchmarkSourceTab.ScalablePersistedSet
                or BenchmarkSourceTab.ScalablePersistedDictionary => PersistedColumns,
            BenchmarkSourceTab.ScalableTsavoriteSet
                or BenchmarkSourceTab.ScalableTsavoriteDictionary
                or BenchmarkSourceTab.ScalableSet
                or BenchmarkSourceTab.ScalableDictionary => TsavoriteColumns,
            _ => EmptyColumns,
        };

    public static IReadOnlyList<string> GetNames(BenchmarkSourceTabDefinition sourceTab) =>
        Get(sourceTab).Select(static column => column.ToString()).ToArray();
}

internal static class ScalableBenchmarkNameFormatter{
    private const long DefaultIndexSize = 64;
    private const long DefaultPageSize = 4096;
    private const long DefaultSegmentSize = 8192;
    private const long DefaultMemorySize = 16384;
    private const double DefaultMutableFraction = 0.5;

    private static readonly string[] KnownTypeTokens = [
        "UnmanagedStruct",
        "ManagedStruct",
        "ManagedClass",
        "Boolean",
        "Decimal",
        "Double",
        "Single",
        "String",
        "UInt64",
        "UInt32",
        "UInt16",
        "Int64",
        "Int32",
        "Int16",
        "Byte",
        "SByte",
        "Char",
    ];

    public static bool IsScalableSourceTab(BenchmarkSourceTab sourceTab) =>
        sourceTab is BenchmarkSourceTab.ScalableSet
            or BenchmarkSourceTab.ScalableDictionary
            or BenchmarkSourceTab.ScalablePersistedList
            or BenchmarkSourceTab.ScalablePersistedSet
            or BenchmarkSourceTab.ScalableTsavoriteSet
            or BenchmarkSourceTab.ScalablePersistedDictionary
            or BenchmarkSourceTab.ScalableTsavoriteDictionary;

    public static bool UsesTypedBenchmarkName(BenchmarkSourceTab sourceTab) =>
        IsScalableSourceTab(sourceTab)
            ||sourceTab is BenchmarkSourceTab.SeamlessPersisted
                or BenchmarkSourceTab.SeamlessTsavorite
                or BenchmarkSourceTab.SystemArray
                or BenchmarkSourceTab.SystemRange
                or BenchmarkSourceTab.SystemList;

    public static bool UsesPreviousRightColumn(BenchmarkSourceTab sourceTab) =>
        sourceTab is BenchmarkSourceTab.ScalablePersistedSet
            or BenchmarkSourceTab.ScalableTsavoriteSet
            or BenchmarkSourceTab.ScalablePersistedDictionary
            or BenchmarkSourceTab.ScalableTsavoriteDictionary
            or BenchmarkSourceTab.ScalablePersistedList;

    public static string CreateBenchmarkName(
        BenchmarkSourceTabDefinition sourceTab,
        string typeFullName,
        string methodName,
        int elementCount = 0){
        if(!UsesTypedBenchmarkName(sourceTab.Tab)){
            return methodName;
        }

        var genericText = CreateGenericTypeText(typeFullName);
        if(string.IsNullOrWhiteSpace(genericText)){
            return methodName;
        }

        return "<"+genericText+">."+methodName;
    }

    public static string CreateBenchmarkTypeText(string typeFullName) =>
        CreateGenericTypeText(typeFullName);

    public static string CreateLegacyMethod1ComparisonTypeText(string typeFullName) {
        var value=CreateBenchmarkTypeText(typeFullName);
        if(!string.IsNullOrWhiteSpace(value)){
            return value;
        }

        var typeName=ShortTypeName(typeFullName);
        if(typeName is "FLinq" or "Linq" or "ZLinq"){
            return "int";
        }
        return string.Empty;
    }

    public static string CreateBenchmarkName(
        string genericText,
        string methodName,
        ScalableBenchmarkParameters parameters) =>
        "<"+genericText+">."+methodName;

    public static string CreateBenchmarkName(string typeFullName,string methodName){
        var genericText=CreateGenericTypeText(typeFullName);
        return string.IsNullOrWhiteSpace(genericText)
            ? methodName
            : "<"+genericText+">."+methodName;
    }

    public static string GetArgumentValue(IReadOnlyList<BenchmarkArgument> arguments,ScalableBenchmarkArgumentColumn column) =>
        BenchmarkArgumentFormatting.GetValue(arguments,column.ToString());

    public static string GetDisplayArgumentValue(
        BenchmarkSourceTabDefinition sourceTab,
        IReadOnlyList<BenchmarkArgument> arguments,
        ScalableBenchmarkArgumentColumn column){
        var value=GetArgumentValue(arguments,column);
        if(!string.IsNullOrWhiteSpace(value)||!IsScalableSourceTab(sourceTab.Tab)){
            return value;
        }

        if(!UsesTsavoriteStorageArguments(sourceTab.Tab)){
            return value;
        }

        var count=BenchmarkArgumentFormatting.GetInt(arguments,ScalableBenchmarkArgumentColumn.Count.ToString());
        var parameters=CreateParameters(sourceTab,count);
        return column switch{
            ScalableBenchmarkArgumentColumn.Index => parameters.IndexSize.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ScalableBenchmarkArgumentColumn.Page => parameters.PageSize.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ScalableBenchmarkArgumentColumn.Segment => parameters.SegmentSize.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ScalableBenchmarkArgumentColumn.Memory => parameters.MemorySize.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ScalableBenchmarkArgumentColumn.Fraction => parameters.MutableFraction.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture),
            _ => value,
        };
    }

    private static bool UsesTsavoriteStorageArguments(BenchmarkSourceTab sourceTab) =>
        sourceTab is BenchmarkSourceTab.ScalableTsavoriteSet
            or BenchmarkSourceTab.ScalableTsavoriteDictionary
            or BenchmarkSourceTab.ScalableSet
            or BenchmarkSourceTab.ScalableDictionary;

    public static string CreateParameterSuffix(ScalableBenchmarkParameters parameters) =>
        FormatFixed(parameters.IndexSize,6)
        +"_"+FormatFixed(parameters.PageSize,7)
        +"_"+FormatFixed(parameters.SegmentSize,7)
        +"_"+FormatFixed(parameters.MemorySize,7)
        +"_"+FormatFraction(parameters.MutableFraction);

    public static string CreateGenericTypeText(string typeFullName){
        var typeName = ShortTypeName(typeFullName);
        if(typeName.StartsWith("Dictionary",StringComparison.Ordinal)){
            return string.Join(",",SplitTypeTokens(NormalizeTypeSuffix(typeName["Dictionary".Length..])).Select(FormatTypeToken));
        }
        if(typeName.StartsWith("List",StringComparison.Ordinal)){
            return FormatTypeToken(NormalizeTypeSuffix(typeName["List".Length..]));
        }
        if(typeName.StartsWith("Set",StringComparison.Ordinal)){
            return FormatTypeToken(NormalizeTypeSuffix(typeName["Set".Length..]));
        }
        if(typeName.StartsWith("HashSet",StringComparison.Ordinal)){
            return FormatTypeToken(NormalizeTypeSuffix(typeName["HashSet".Length..]));
        }
        if(typeName.StartsWith("BlittableHashSet",StringComparison.Ordinal)){
            return FormatTypeToken(NormalizeTypeSuffix(typeName["BlittableHashSet".Length..]));
        }
        if(typeName.StartsWith("FLinq",StringComparison.Ordinal)){
            return FormatTypeToken(NormalizeTypeSuffix(typeName["FLinq".Length..]));
        }
        if(typeName.StartsWith("Linq",StringComparison.Ordinal)){
            return FormatTypeToken(NormalizeTypeSuffix(typeName["Linq".Length..]));
        }
        if(typeName.StartsWith("ZLinq",StringComparison.Ordinal)){
            return FormatTypeToken(NormalizeTypeSuffix(typeName["ZLinq".Length..]));
        }

        return string.Empty;
    }

    public static string ShortTypeName(string typeFullName){
        if(string.IsNullOrWhiteSpace(typeFullName)){
            return string.Empty;
        }

        var index = typeFullName.LastIndexOf('.');
        return index<0||index==typeFullName.Length-1
            ? typeFullName
            : typeFullName[(index+1)..];
    }

    private static ScalableBenchmarkParameters CreateParameters(BenchmarkSourceTabDefinition sourceTab,int elementCount) {
        var memorySize = elementCount>0
            ? GetScalableMemorySize(elementCount)
            : DefaultMemorySize;
        return sourceTab.Tab switch{
            BenchmarkSourceTab.ScalableTsavoriteSet
                or BenchmarkSourceTab.ScalableTsavoriteDictionary
                or BenchmarkSourceTab.ScalableSet
                or BenchmarkSourceTab.ScalableDictionary => new(
                    131072,
                    4096,
                    4096,
                    memorySize,
                    1.0),
            _ => new(
                DefaultIndexSize,
                DefaultPageSize,
                DefaultSegmentSize,
                memorySize,
                DefaultMutableFraction),
        };
    }

    private static long GetScalableMemorySize(int count) {
        var required = Math.Max(8192L,count*128L);
        var result = 1L;
        while(result<required) {
            result<<=1;
        }
        return result;
    }

    private static string FormatFixed(long value,int width) =>
        value.ToString("D"+width.ToString(System.Globalization.CultureInfo.InvariantCulture),System.Globalization.CultureInfo.InvariantCulture);

    private static string FormatFraction(double value) =>
        ((int)Math.Round(value*10,MidpointRounding.AwayFromZero)).ToString("D2",System.Globalization.CultureInfo.InvariantCulture);

    private static IEnumerable<string> SplitTypeTokens(string text){
        for(var index=0;index<text.Length;){
            var token = KnownTypeTokens.FirstOrDefault(candidate => text[index..].StartsWith(candidate,StringComparison.Ordinal));
            if(token is not null){
                yield return token;
                index+=token.Length;
                continue;
            }

            var next=index+1;
            yield return text[index..next];
            index=next;
        }
    }

    private static string NormalizeTypeSuffix(string text){
        var value = text.TrimStart('_');
        if(value.StartsWith("MemoryPack",StringComparison.Ordinal)){
            value=value["MemoryPack".Length..].TrimStart('_');
        }
        if(value.StartsWith("Blittable",StringComparison.Ordinal)){
            value=value["Blittable".Length..].TrimStart('_');
        }
        return value;
    }

    private static string FormatTypeToken(string token) =>
        token switch{
            "String" => "string",
            "Int" => "int",
            "Int32" => "int",
            "Decimal" => "decimal",
            "Boolean" => "System.Boolean",
            "Double" => "System.Double",
            "Single" => "System.Single",
            "UInt64" => "System.UInt64",
            "UInt32" => "System.UInt32",
            "UInt16" => "System.UInt16",
            "Int64" => "System.Int64",
            "Int16" => "System.Int16",
            "Byte" => "System.Byte",
            "SByte" => "System.SByte",
            "Char" => "System.Char",
            "ManagedClass" => "ManagedClass",
            "ManagedStruct" => "ManagedStruct",
            "UnmanagedStruct" => "UnmanagedStruct",
            _ => token,
        };
}
