using System.Collections;
using System.Diagnostics;
namespace Seamless.Collections;
using Tsavorite.core;

/// <summary>
/// Tsavorite をバックエンドに使うオンメモリ HashSet。
/// <see cref="ISet{T}"/> を実装し、利用者に Tsavorite の詳細を見せない。
/// 重複を許さず、キーのみを格納する。値は Empty 構造体を使用する。
/// </summary>
public sealed class Relation<TKey,TValue>:Collection<TValue,Empty>,IRelation<TValue>
	where TKey:IEquatable<TKey>
	where TValue:IEquatable<TValue>, ITuple<TKey>
{
    internal Relation(TsavoriteKV<SpanByte,SpanByte,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>> Store):base(Store){}

	public bool Add(TValue item){
		var KeyBytes=Serialize(item);
		var ValueBytes=Serialize(default(Empty));
		unsafe {
			fixed (byte* pKey = KeyBytes, pValue = ValueBytes) {
				var Key = SpanByte.FromPinnedPointer(pKey, KeyBytes.Length);
				var Input = SpanByte.FromPinnedPointer(pValue, ValueBytes.Length);
				SpanByteAndMemory output=default;
				var Status=this.Session.BasicContext.RMW(ref Key,ref Input,ref output);
                Status=Pending(this.Session,Status);

				//if(Status.IsPending){
				//	this.Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs,wait:true);
				//	CompletedOutputs.Next();
				//	Status=CompletedOutputs.Current.Status;
				//	CompletedOutputs.Dispose();
				//}else{
    //            }
                Debug.Assert(Status.Found!=Status.NotFound);
				return Status.NotFound;
			}
		}
	}
    IEnumerator IEnumerable.GetEnumerator()=>this.GetEnumerator();
	public IEnumerator<TValue>GetEnumerator()=>((IEnumerable<TValue>)this.GetKeyEnumerable()).GetEnumerator();
}
