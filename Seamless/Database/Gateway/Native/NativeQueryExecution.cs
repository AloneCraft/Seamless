using Seamless.Database.Client;

namespace Seamless.Database.Gateway.Native;

public sealed record NativeQueryDatabaseBinding(string Name,int DatabaseId,Guid GenerationId,string SchemaFingerprint,string AssemblySha256);

/// <summary>取引から可視な同一versionの、ロード時に検証した実行物と依存DB。</summary>
public sealed class NativeQueryExecution{
    public Guid WalId{get;}
    public Guid QueryGenerationId{get;}
    public string ManifestSha256{get;}
    public string AssemblySha256{get;}
    public string ModuleId{get;}
    public string EntryType{get;}
    public NativeQueryDefinition Definition{get;}
    public INativeQueryModule Module{get;}
    public IReadOnlyList<NativeQueryDatabaseBinding> Databases{get;}
    private readonly NativeWireValidator? arguments;
    private readonly NativeRecordQueryContract? records;
    private readonly global::Seamless.Execution.ResourceBudget validationBudget;
    internal bool HasSchemaIndependentWireContract{get;}
    internal NativeQueryExecution(Guid walId,Guid generationId,string manifestSha256,string assemblySha256,string moduleId,string entryType,
        NativeQueryDefinition definition,INativeQueryModule module,IEnumerable<NativeQueryDatabaseBinding> databases,NativeWireValidator? arguments,NativeRecordQueryContract? records,bool schemaIndependentWireContract,global::Seamless.Execution.ResourceBudget validationBudget){
        this.WalId=walId;this.QueryGenerationId=generationId;this.ManifestSha256=manifestSha256;this.AssemblySha256=assemblySha256;this.ModuleId=moduleId;this.EntryType=entryType;
        this.Definition=definition;this.Module=module;this.Databases=Array.AsReadOnly(databases.ToArray());this.arguments=arguments;this.records=records;this.HasSchemaIndependentWireContract=schemaIndependentWireContract;this.validationBudget=validationBudget;
    }
    // v1は独自layout・Nullable等の物理ABIを完全には表さない。閉じた固定幅型だけを対象にする。
    internal static bool HasFixedWireContract(Type type)=>type==typeof(bool)||type==typeof(byte)||type==typeof(sbyte)||type==typeof(char)
        ||type==typeof(short)||type==typeof(ushort)||type==typeof(int)||type==typeof(uint)||type==typeof(long)||type==typeof(ulong)||type==typeof(float)||type==typeof(double);
    internal void ValidateArguments(ReadOnlyMemory<byte> payload,CancellationToken cancellationToken=default){
        cancellationToken.ThrowIfCancellationRequested();
        if(this.records is not null){this.records.ValidateArguments(payload.Span,this.validationBudget,cancellationToken);}else{this.arguments!.Validate(payload.Span);}
    }
}
