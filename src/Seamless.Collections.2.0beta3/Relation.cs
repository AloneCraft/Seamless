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
    internal Relation(
		TsavoriteKV<
			StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,
			ObjectAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>
		> Store):base(Store){}
    //internal Relation(TsavoriteKV<
    //     TStoreFunctions, TAllocator
    //> Store):base(Store){}
	public bool Add(TValue item){
        //SpanByteAndMemory Output=default;
        //this.Read(item,ref Output);
		var KeyBytes=Serialize(item);
		var ValueBytes=Serialize(default(Empty));
		var input=PinnedSpanByte.FromPinnedSpan(ValueBytes);
		SpanByteAndMemory Output=default;
		var fixedKey = (Garnet.common.FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
		var Status=this.Session.BasicContext.RMW(fixedKey,ref input,ref Output);
        Status=Pending(this.Session,Status);
		//if(Status.IsPending){
		//	this.Session.BasicContext.CompletePendingWithOutputs(out var completedOutputs,wait:true);
		//	completedOutputs.Next();
		//	Status=completedOutputs.Current.Status;
		//	completedOutputs.Dispose();
		//}
        Debug.Assert(Status.Found!=Status.NotFound);
		return Status.NotFound;
	}
    IEnumerator IEnumerable.GetEnumerator()=>this.GetEnumerator();
	public IEnumerator<TValue>GetEnumerator()=>((IEnumerable<TValue>)this.GetKeyEnumerable()).GetEnumerator();
}
