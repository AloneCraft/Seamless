using Benchmark.Tpc.Core;
namespace Benchmark.Tpc.Server;
/// <summary>再実行ログに載せる 1 回の書き込み。キーと行は保存形式(MemoryPack)のバイト列。</summary>
public readonly record struct RedoEntry(int TableId,WriteKind Kind,byte[] Key,byte[]? Row);
/// <summary>1 つの取引の書き込みの並び。取引の通し番号で並べ、確定は取引ごと。</summary>
public sealed record RedoTransaction(long Sequence,IReadOnlyList<RedoEntry> Entries);
