using System.Buffers.Binary;
using System.Security.Cryptography;
using Seamless.Collections.Tsavorite;
namespace Seamless.Transactions.Collections;

public sealed partial class VersionedTransactionalTable<TKey,TRecord>{
    /// <summary>所有者が全取引を排出しregionを閉じた保存境界だけで、現在の確定行を検証する。</summary>
    public TransactionParticipantSnapshot GetCommittedSnapshot(){
        this.CheckUnpublishedSnapshot(false);
        if(this.region is not null){throw new InvalidOperationException("Drain transactions and close the native region before hashing a committed snapshot.");}
        using var working=this.resources.Budget.ReserveMemory(checked(this.data.PageBytes*4+1024+global::System.Text.Encoding.UTF8.GetByteCount(this.CodecIdentity)));
        using var hasher=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);using var access=this.data.Create();
        var strategy=new SnapshotHash(this,hasher);access.IterateRecords(ref strategy,this.data.MaximumCommittedVersion);
        Span<byte> count=stackalloc byte[8];BinaryPrimitives.WriteInt64LittleEndian(count,strategy.Count);hasher.AppendData(count);hasher.AppendData(global::System.Text.Encoding.UTF8.GetBytes(this.CodecIdentity));hasher.AppendData(strategy.Sum);hasher.AppendData(strategy.Xor);
        return new(strategy.Count,Convert.ToHexString(hasher.GetHashAndReset()));
    }
    private struct SnapshotHash(VersionedTransactionalTable<TKey,TRecord> table,IncrementalHash hasher):IHistoryStrategy<SnapshotHash>{
        private readonly VersionedTransactionalTable<TKey,TRecord> table=table;
        private readonly IncrementalHash hasher=hasher;
        internal readonly byte[] Sum=new byte[32],Xor=new byte[32];
        internal long Count;
        public static bool Invoke(ref SnapshotHash self,ReadOnlySpan<byte> key,ReadOnlySpan<byte> row,long version,bool deleted){
            self.table.resources.ThrowIfUnavailable();if(deleted){return true;}
            if(self.table.encoding!=VersionedRecordEncoding.Auto){self.Append(key,row);}
            else{
                using var canonicalKey=self.table.resources.Encode(self.table.DecodeStoreKey(key));using var canonicalRow=self.table.resources.Encode(self.table.DecodeStoreRow(row));self.Append(canonicalKey.Memory.Span,canonicalRow.Memory.Span);
            }
            return true;
        }
        private void Append(ReadOnlySpan<byte> key,ReadOnlySpan<byte> row){
            // 保存順序や旧版数に依存せず、現在値adapterと同じcanonical行hashを束ねる。
            Span<byte> lengths=stackalloc byte[8];BinaryPrimitives.WriteInt32LittleEndian(lengths,key.Length);BinaryPrimitives.WriteInt32LittleEndian(lengths[4..],row.Length);
            this.hasher.AppendData(lengths);this.hasher.AppendData(key);this.hasher.AppendData(row);Span<byte> digest=stackalloc byte[32];if(!this.hasher.TryGetHashAndReset(digest,out _)){throw new InvalidOperationException("Snapshot hash failed.");}
            var carry=0;for(var index=0;index<32;index++){this.Xor[index]^=digest[index];var value=this.Sum[index]+digest[index]+carry;this.Sum[index]=unchecked((byte)value);carry=value>>8;}this.Count=checked(this.Count+1);
        }
    }
}
