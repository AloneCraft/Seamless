//using Common;
//using Garnet.common;
//using MemoryPack;
//using Tsavorite.core;
//using Xunit;
//namespace Seamless.Collections.Tests;

//// ---------------------------------------------------------------------------
//// テスト用ヘルパー型
//// ---------------------------------------------------------------------------

///// <summary>
///// バイト配列を IHeapObject としてラップする。
///// ValueIsObject=true ストレージパスでオブジェクトログに書き出される値として使用する。
///// </summary>
//internal sealed class BytesHeapObject : IHeapObject {
//	public byte[] Data;
//	public BytesHeapObject(ReadOnlySpan<byte> span) => Data=span.ToArray();
//	public static int MaxSerializedObjectSize => 0;
//	public long HeapMemorySize => Data.Length;
//	public IHeapObject Clone() => new BytesHeapObject(Data);
//	public void Serialize(BinaryWriter w) { }
//	public void WriteType(BinaryWriter w,bool includeNamespace) { }
//	public void DoSerialize(BinaryWriter w) { }
//	public void CacheSerializedObjectData(ref LogRecord src,ref LogRecord dst,ref RMWInfo rmwInfo) { }
//	public void ClearSerializedObjectData() { }
//	public void Dispose() { }
//}

///// <summary>
///// IObjectSerializer.Serialize / Deserialize の呼び出し回数を計上する。
///// GetValueSerializeCreator が常に同一インスタンスを返すので、ページをまたいで呼び出し回数が積算される。
///// </summary>
//internal sealed class TrackingSerializer : IObjectSerializer<IHeapObject> {
//	public int SerializeCount;
//	public int DeserializeCount;
//	private Stream stream=null!;
//	public void BeginSerialize(Stream s) => stream=s;
//	public void Serialize(IHeapObject obj) {
//		SerializeCount++;
//		var data=((BytesHeapObject)obj).Data;
//		Span<byte> len=stackalloc byte[4];
//		BitConverter.TryWriteBytes(len,data.Length);
//		stream.Write(len);
//		stream.Write(data);
//	}
//	public void EndSerialize() => stream=null!;
//	public void BeginDeserialize(Stream s) => stream=s;
//	public void Deserialize(out IHeapObject obj) {
//		DeserializeCount++;
//		Span<byte> len=stackalloc byte[4];
//		stream.ReadExactly(len);
//		var buf=new byte[BitConverter.ToInt32(len)];
//		stream.ReadExactly(buf);
//		obj=new BytesHeapObject(buf);
//	}
//	public void EndDeserialize() => stream=null!;
//}

///// <summary>
///// ValueIsObject=true で値を IHeapObject として格納するセッション関数。
///// GetUpsertFieldInfo / GetRMWInitialFieldInfo が ObjectIdMap.ObjectIdSize + ValueIsObject=true を返すことで、
///// ObjectAllocator のオブジェクトログ書き込みパスに入る。
///// </summary>
//internal readonly struct HeapObjectSessionFunctions() : ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty> {
//	private readonly System.Buffers.MemoryPool<byte> memPool=System.Buffers.MemoryPool<byte>.Shared;

//	// ---- Reader ----
//	public bool Reader<TSourceLogRecord>(in TSourceLogRecord src,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref ReadInfo ri)
//		where TSourceLogRecord : ISourceLogRecord {
//		if(src.Info.ValueIsObject) {
//			var obj=(BytesHeapObject)src.ValueObject;
//			obj.Data.AsSpan().CopyTo(ref output,memPool);
//		} else {
//			src.ValueSpan.CopyTo(ref output,memPool);
//		}
//		return true;
//	}

//	// ---- InitialWriter ----
//	public bool InitialWriter(ref LogRecord dst,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ReadOnlySpan<byte> srcValue,ref SpanByteAndMemory output,ref UpsertInfo ui)
//		=> dst.TrySetValueObjectAndPrepareOptionals(new BytesHeapObject(srcValue),in sizeInfo);
//	public bool InitialWriter(ref LogRecord dst,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,IHeapObject srcValue,ref SpanByteAndMemory output,ref UpsertInfo ui)
//		=> dst.TrySetValueObjectAndPrepareOptionals(srcValue,in sizeInfo);
//	public bool InitialWriter<TSourceLogRecord>(ref LogRecord dst,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,in TSourceLogRecord src,ref SpanByteAndMemory output,ref UpsertInfo ui)
//		where TSourceLogRecord : ISourceLogRecord {
//		if(src.Info.ValueIsObject) return dst.TrySetValueObjectAndPrepareOptionals(src.ValueObject,in sizeInfo);
//		return dst.TrySetValueObjectAndPrepareOptionals(new BytesHeapObject(src.ValueSpan),in sizeInfo);
//	}

//	// ---- InPlaceWriter ----
//	public bool InPlaceWriter(ref LogRecord lr,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ReadOnlySpan<byte> srcValue,ref SpanByteAndMemory output,ref UpsertInfo ui)
//		=> lr.TrySetValueObject(new BytesHeapObject(srcValue));
//	public bool InPlaceWriter(ref LogRecord lr,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,IHeapObject srcValue,ref SpanByteAndMemory output,ref UpsertInfo ui)
//		=> lr.TrySetValueObject(srcValue);
//	public bool InPlaceWriter<TSourceLogRecord>(ref LogRecord dst,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,in TSourceLogRecord src,ref SpanByteAndMemory output,ref UpsertInfo ui)
//		where TSourceLogRecord : ISourceLogRecord {
//		if(src.Info.ValueIsObject) return dst.TrySetValueObject(src.ValueObject);
//		return dst.TrySetValueObject(new BytesHeapObject(src.ValueSpan));
//	}

//	// ---- PostUpsert / PostInitialWriter ----
//	public void PostUpsertOperation<TKey,TEpochAccessor>(TKey key,ref PinnedSpanByte input,ReadOnlySpan<byte> valueSpan,ref UpsertInfo ui,TEpochAccessor epoch)
//		where TKey : IKey, allows ref struct where TEpochAccessor : IEpochAccessor { }
//	public void PostUpsertOperation<TKey,TEpochAccessor>(TKey key,ref PinnedSpanByte input,IHeapObject obj,ref UpsertInfo ui,TEpochAccessor epoch)
//		where TKey : IKey, allows ref struct where TEpochAccessor : IEpochAccessor { }
//	public void PostInitialWriter(ref LogRecord dst,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ReadOnlySpan<byte> srcValue,ref SpanByteAndMemory output,ref UpsertInfo ui) { }
//	public void PostInitialWriter(ref LogRecord dst,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,IHeapObject srcValue,ref SpanByteAndMemory output,ref UpsertInfo ui) { }
//	public void PostInitialWriter<TSourceLogRecord>(ref LogRecord dst,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,in TSourceLogRecord src,ref SpanByteAndMemory output,ref UpsertInfo ui)
//		where TSourceLogRecord : ISourceLogRecord { }

//	// ---- RMW ----
//	public bool NeedInitialUpdate<TKey>(TKey key,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo ri)
//		where TKey : IKey, allows ref struct => true;
//	public bool InitialUpdater(ref LogRecord dst,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo ri)
//		=> dst.TrySetValueObjectAndPrepareOptionals(new BytesHeapObject(input.ReadOnlySpan),in sizeInfo);
//	public bool InPlaceUpdater(ref LogRecord lr,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo ri)
//		=> lr.TrySetValueObject(new BytesHeapObject(input.ReadOnlySpan));
//	public void PostInitialUpdater(ref LogRecord dst,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo ri) { }
//	public bool NeedCopyUpdate<TSourceLogRecord>(in TSourceLogRecord src,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo ri)
//		where TSourceLogRecord : ISourceLogRecord => true;
//	public bool CopyUpdater<TSourceLogRecord>(in TSourceLogRecord src,ref LogRecord dst,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo ri)
//		where TSourceLogRecord : ISourceLogRecord {
//		if(src.Info.ValueIsObject) return dst.TrySetValueObjectAndPrepareOptionals(new BytesHeapObject(((BytesHeapObject)src.ValueObject).Data),in sizeInfo);
//		return dst.TryCopyFrom(in src,in sizeInfo);
//	}
//	public bool PostCopyUpdater<TSourceLogRecord>(in TSourceLogRecord src,ref LogRecord dst,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo ri)
//		where TSourceLogRecord : ISourceLogRecord => true;
//	public void PostRMWOperation<TKey,TEpochAccessor>(TKey key,ref PinnedSpanByte input,ref RMWInfo ri,TEpochAccessor epoch)
//		where TKey : IKey, allows ref struct where TEpochAccessor : IEpochAccessor { }
//	public void RMWCompletionCallback(ref DiskLogRecord disk,ref PinnedSpanByte input,ref SpanByteAndMemory output,Empty ctx,Status status,RecordMetadata meta) { }

//	// ---- Delete ----
//	public bool InitialDeleter(ref LogRecord lr,ref DeleteInfo di) => true;
//	public void PostInitialDeleter(ref LogRecord lr,ref DeleteInfo di) { }
//	public bool InPlaceDeleter(ref LogRecord lr,ref DeleteInfo di) => true;
//	public void PostDeleteOperation<TKey,TEpochAccessor>(TKey key,ref DeleteInfo di,TEpochAccessor epoch)
//		where TKey : IKey, allows ref struct where TEpochAccessor : IEpochAccessor { }
//	public void ReadCompletionCallback(ref DiskLogRecord disk,ref PinnedSpanByte input,ref SpanByteAndMemory output,Empty ctx,Status status,RecordMetadata meta) { }

//	// ---- サイズ計算 ----
//	public RecordFieldInfo GetRMWInitialFieldInfo<TKey>(TKey key,ref PinnedSpanByte input)
//		where TKey : IKey, allows ref struct
//		=> new() { KeySize=key.KeyBytes.Length,ValueSize=ObjectIdMap.ObjectIdSize,ValueIsObject=true };
//	public RecordFieldInfo GetRMWModifiedFieldInfo<TSourceLogRecord>(in TSourceLogRecord src,ref PinnedSpanByte input)
//		where TSourceLogRecord : ISourceLogRecord
//		=> new() { KeySize=src.Key.Length,ValueSize=ObjectIdMap.ObjectIdSize,ValueIsObject=true };
//	public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,ReadOnlySpan<byte> value,ref PinnedSpanByte input)
//		where TKey : IKey, allows ref struct
//		=> new() { KeySize=key.KeyBytes.Length,ValueSize=ObjectIdMap.ObjectIdSize,ValueIsObject=true };
//	public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,IHeapObject value,ref PinnedSpanByte input)
//		where TKey : IKey, allows ref struct
//		=> new() { KeySize=key.KeyBytes.Length,ValueSize=ObjectIdMap.ObjectIdSize,ValueIsObject=true };
//	public RecordFieldInfo GetUpsertFieldInfo<TKey,TSourceLogRecord>(TKey key,in TSourceLogRecord src,ref PinnedSpanByte input)
//		where TKey : IKey, allows ref struct
//		where TSourceLogRecord : ISourceLogRecord
//		=> new() { KeySize=key.KeyBytes.Length,ValueSize=ObjectIdMap.ObjectIdSize,ValueIsObject=true };
//	public void ConvertOutputToHeap(ref PinnedSpanByte input,ref SpanByteAndMemory output) { }
//}

///// <summary>
///// TrackingSerializer を GetValueSerializeCreator() で提供するテスト用ストア。
///// LocalMemoryDevice を LogDevice / ObjectLogDevice として受け取り、実際のページ退避を発生させる。
///// </summary>
//internal sealed class TrackingDictionaryStore : CollectionStore<int,string> {
//	/// <summary>呼び出し回数を計上するシリアライザ。</summary>
//	public readonly TrackingSerializer Serializer=new();
//	protected override IObjectSerializer<IHeapObject>? GetValueSerializeCreator() => Serializer;
//	/// <param name="logDevice">主ログ用 LocalMemoryDevice</param>
//	/// <param name="objLogDevice">オブジェクトログ用 LocalMemoryDevice（容量 >= MinObjectLogCapacity）</param>
//	public TrackingDictionaryStore(IDevice logDevice,IDevice objLogDevice)
//		: base(Const.IndexSize,Const.PageSize,Const.SegmentSize,Const.LogMemorySize,0.1,logDevice,objLogDevice) { }
//}

//// ---------------------------------------------------------------------------
//// テスト本体
//// ---------------------------------------------------------------------------

///// <summary>
///// ValueIsObject=true レコードのページ退避時に IObjectSerializer.Serialize が呼ばれ、
///// ディスクから読み戻し時に IObjectSerializer.Deserialize が呼ばれることを検証する。
///// </summary>
//public class IObjectSerializerViaTsavorite {
//	private static byte[] SerInt(int v) => MemoryPackSerializer.Serialize(v);
//	private static byte[] SerStr(string v) => MemoryPackSerializer.Serialize(v);
//	private static FixedSpanByteKey MakeKey(byte[] b) => (FixedSpanByteKey)(ReadOnlySpan<byte>)b;

//	// ログデバイス容量: SegmentSize の 64 倍（262 KB）
//	private const long LogCapacity=Const.SegmentSize*64;

//	[Fact,Trait("Category","Coverage")]
//	public void IObjectSerializer_Serialize呼ばれる_ValueIsObjectページ退避時() {
//		// LocalMemoryDevice: 実際の非同期I/Oを行うメモリ上デバイス。
//		// MemoryOnlyDevice と異なり ReadAsync/WriteAsync が正常動作するため
//		// ページ退避が発生し ObjectAllocator が IObjectSerializer を呼び出す。
//		using var logDevice=new LocalMemoryDevice(LogCapacity,Const.SegmentSize,1,0,64u,"log");
//		using var objDevice=new LocalMemoryDevice(Const.MinObjectLogCapacity,Const.MinObjectLogCapacity,1,0,64u,"objlog");
//		using var store=new TrackingDictionaryStore(logDevice,objDevice);

//		using var session=store.Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,HeapObjectSessionFunctions>(new HeapObjectSessionFunctions());
//		var bc=session.BasicContext;

//		// MemorySize (8192 bytes, MutableFraction=0.1) を大幅に超えるデータを書き込み、
//		// 複数ページをオブジェクトログへ退避させる。
//		const int Count=1000;
//		for(var i=0; i<Count; i++) {
//			var status=bc.Upsert(MakeKey(SerInt(i)),SerStr($"value_{i}"));
//			if(status.IsPending) {
//				bc.CompletePendingWithOutputs(out var completed,wait: true);
//				completed.Dispose();
//			}
//		}
//		bc.CompletePending(wait: true);

//		// ページ退避によって IObjectSerializer.Serialize が呼ばれていること
//		Assert.True(
//			store.Serializer.SerializeCount>0,
//			$"SerializeCount={store.Serializer.SerializeCount}: ページ退避が発生しなかった可能性がある"
//		);

//		// ディスクから読み戻してデシリアライズが呼ばれていること
//		for(var i=0; i<Count; i++) {
//			SpanByteAndMemory output=default;
//			var status=bc.Read(MakeKey(SerInt(i)),ref output);
//			if(status.IsPending) {
//				bc.CompletePendingWithOutputs(out var completed,wait: true);
//				completed.Next();
//				var s=completed.Current.Status;
//				Assert.True(s.Found,$"key={i} が見つからなかった");
//				completed.Dispose();
//			} else {
//				Assert.True(status.Found,$"key={i} が見つからなかった (in-memory)");
//				output.Memory?.Dispose();
//			}
//		}
//		bc.CompletePending(wait: true);

//		Assert.True(
//			store.Serializer.DeserializeCount>0,
//			$"DeserializeCount={store.Serializer.DeserializeCount}: ディスクからの読み戻しが発生しなかった可能性がある"
//		);
//	}
//}
