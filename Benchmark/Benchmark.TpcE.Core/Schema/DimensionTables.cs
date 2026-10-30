using MemoryPack;
using Seamless.Collections;
namespace Benchmark.TpcE.Core;
// 次元系の 4 表(TPC-E 2.2.7)。

public readonly record struct AddressKey(long Id);
/// <summary>ADDRESS(2.2.7.1)。</summary>
[MemoryPackable]
public partial record struct AddressRow(long Id,string? Line1,string? Line2,Code16 ZipCode,string? Country):IRecord<AddressKey>{
    readonly AddressKey IRecord<AddressKey>.Key=>new(this.Id);
}
public readonly record struct StatusTypeKey(Code8 Id);
/// <summary>STATUS_TYPE(2.2.7.2)。5 行の固定表。</summary>
[MemoryPackable]
public partial record struct StatusTypeRow(Code8 Id,string Name):IRecord<StatusTypeKey>{
    readonly StatusTypeKey IRecord<StatusTypeKey>.Key=>new(this.Id);
}
public readonly record struct TaxrateKey(Code8 Id);
/// <summary>TAXRATE(2.2.7.3)。Rate は 0〜1。</summary>
[MemoryPackable]
public partial record struct TaxrateRow(Code8 Id,string Name,decimal Rate):IRecord<TaxrateKey>{
    readonly TaxrateKey IRecord<TaxrateKey>.Key=>new(this.Id);
}
public readonly record struct ZipCodeKey(Code16 Code);
/// <summary>ZIP_CODE(2.2.7.4)。</summary>
[MemoryPackable]
public partial record struct ZipCodeRow(Code16 Code,string Town,string Division):IRecord<ZipCodeKey>{
    readonly ZipCodeKey IRecord<ZipCodeKey>.Key=>new(this.Code);
}
