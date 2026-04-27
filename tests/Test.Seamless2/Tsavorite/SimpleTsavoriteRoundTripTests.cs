using Garnet.common;
using MemoryPack;
using Tsavorite.core;

using StoreFunc = Tsavorite.core.StoreFunctions<Tsavorite.core.SpanByteComparer,Tsavorite.core.DefaultRecordDisposer>;
using StoreAllocator = Tsavorite.core.SpanByteAllocator<Tsavorite.core.StoreFunctions<Tsavorite.core.SpanByteComparer,Tsavorite.core.DefaultRecordDisposer>>;

namespace Test.Seamless.Benchmarks.Simple {
    [Collection("TsavoriteTests")]
    public sealed class SimpleTsavoriteRoundTripTests {
        private static string CreateFolder() => global::Test.Seamless.TestPath.Directory<SimpleTsavoriteRoundTripTests>();

        private static byte[] Serialize<T>(T value) => MemoryPackSerializer.Serialize(value);

        private static T Deserialize<T>(ReadOnlySpan<byte> bytes) =>
            MemoryPackSerializer.Deserialize<T>(bytes)!;

        [Fact, Trait("Category","Unit")]
        public void Key0_Record0_CanRoundTrip_WithoutDictionary() {
            var key = new Keys.Key0(10);
            var expected = new Records.Record0(10,20);
            var keyBytes = Serialize(key);
            var actualValueBytes = RoundTrip(keyBytes,Serialize(expected));
            var actual = Rehydrate(Deserialize<Keys.Key0>(keyBytes),actualValueBytes);

            Assert.Equal(expected.Column0,actual.Column0);
            Assert.Equal(expected.Column1,actual.Column1);
        }

        [Fact, Trait("Category","Unit")]
        public void Key1_Record1_CanRoundTrip_WithoutDictionary() {
            var key = new Keys.Key1(11,12);
            var expected = new Records.Record1(11,12,13,14);
            var keyBytes = Serialize(key);
            var actualValueBytes = RoundTrip(keyBytes,Serialize(expected));
            var actual = Rehydrate(Deserialize<Keys.Key1>(keyBytes),actualValueBytes);

            Assert.Equal(expected.Column0,actual.Column0);
            Assert.Equal(expected.Column1,actual.Column1);
            Assert.Equal(expected.Column2,actual.Column2);
            Assert.Equal(expected.Column3,actual.Column3);
        }

        [Fact, Trait("Category","Unit")]
        public void Key2_Record2_CanRoundTrip_WithoutDictionary() {
            var key = new Keys.Key2("alpha",21,22);
            var expected = new Records.Record2("alpha",21,22,23);
            var keyBytes = Serialize(key);
            var actualValueBytes = RoundTrip(keyBytes,Serialize(expected));
            var actual = Rehydrate(Deserialize<Keys.Key2>(keyBytes),actualValueBytes);

            Assert.Equal(expected.Column0,actual.Column0);
            Assert.Equal(expected.Column1,actual.Column1);
            Assert.Equal(expected.Column2,actual.Column2);
            Assert.Equal(expected.Column3,actual.Column3);
        }

        private static Records.Record0 Rehydrate(Keys.Key0 key,ReadOnlySpan<byte> valueBytes) {
            var value = Deserialize<Records.Record0>(valueBytes);
            return new Records.Record0(key.Column0,value.Column1);
        }

        private static Records.Record1 Rehydrate(Keys.Key1 key,ReadOnlySpan<byte> valueBytes) {
            var value = Deserialize<Records.Record1>(valueBytes);
            return new Records.Record1(key.Column0,key.Column1,value.Column2,value.Column3);
        }

        private static Records.Record2 Rehydrate(Keys.Key2 key,ReadOnlySpan<byte> valueBytes) {
            var value = Deserialize<Records.Record2>(valueBytes);
            return new Records.Record2(key.Column0,key.Column1,key.Column2,value.Column3);
        }

        private static byte[] RoundTrip(byte[] keyBytes,byte[] valueBytes) {
            using var handle = new NativeStoreHandle(CreateFolder());
            using var session = handle.Store.NewSession<FixedSpanByteKey,Empty,byte[]?,Empty,ByteStoreFunctions>(new());
            var basicContext = session.BasicContext;
            var key = (FixedSpanByteKey)(ReadOnlySpan<byte>)keyBytes;

            var status = basicContext.Upsert(key,valueBytes);
            if(status.IsPending)
                basicContext.CompletePending(wait: true);

            byte[]? output = null;
            status=basicContext.Read(key,ref output);
            status=CompletePending(session,status,ref output);

            Assert.True(status.Found);
            Assert.NotNull(output);
            return output!;
        }

        private static Status CompletePending(
            ClientSession<FixedSpanByteKey,Empty,byte[]?,Empty,ByteStoreFunctions,StoreFunc,StoreAllocator> session,
            Status status,
            ref byte[]? output) {
            if(!status.IsPending)
                return status;

            session.BasicContext.CompletePendingWithOutputs(out var completedOutputs,wait: true);
            using(completedOutputs) {
                if(completedOutputs.Next()) {
                    status=completedOutputs.Current.Status;
                    output=completedOutputs.Current.Output;
                }
            }
            return status;
        }

        private sealed class NativeStoreHandle:IDisposable {
            private readonly string folder;
            private readonly IDevice logDevice;
            private readonly KVSettings settings;
            internal readonly TsavoriteKV<StoreFunc,StoreAllocator> Store;

            public NativeStoreHandle(string folder) {
                this.folder=folder;
                this.logDevice=Devices.CreateLogDevice(
                    Path.Combine(folder,"log.data"),
                    deleteOnClose: true,
                    capacity: global::Seamless.Const.MinObjectLogCapacity);
                this.settings=new KVSettings {
                    IndexSize=64,
                    LogMemorySize=global::Seamless.Const.MinObjectLogCapacity,
                    PageSize=1L<<12,
                    SegmentSize=1L<<20,
                    MutableFraction=1,
                    LogDevice=this.logDevice,
                };

                var storeFunctions = new StoreFunc(
                    new SpanByteComparer(),
                    static () => null,
                    new DefaultRecordDisposer());

                this.Store=new TsavoriteKV<StoreFunc,StoreAllocator>(
                    this.settings,
                    storeFunctions,
                    static (allocSettings,storeFuncs) => new StoreAllocator(allocSettings,storeFuncs));
            }

            public void Dispose() {
                this.Store.Log.FlushAndEvict(wait: true);
                this.Store.Dispose();
                this.settings.Dispose();
                this.logDevice.Dispose();
                try { Directory.Delete(this.folder,recursive: true); } catch { }
            }
        }

        private readonly struct ByteStoreFunctions:
            ISessionFunctions<Empty,byte[]?,Empty> {

            private static T ThrowNotSupported<T>() => throw new NotSupportedException();

            public bool Reader<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref Empty input,ref byte[]? output,ref ReadInfo readInfo)
                where TSourceLogRecord : ISourceLogRecord {
                output=srcLogRecord.ValueSpan.ToArray();
                return true;
            }

            public void ReadCompletionCallback(ref DiskLogRecord diskLogRecord,ref Empty input,ref byte[]? output,Empty ctx,Status status,RecordMetadata recordMetadata) {
                output=status.Found ? diskLogRecord.ValueSpan.ToArray() : null;
            }

            public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref Empty input,ReadOnlySpan<byte> srcValue,ref byte[]? output,ref UpsertInfo upsertInfo) {
                return dstLogRecord.TrySetValueSpanAndPrepareOptionals(srcValue,in sizeInfo);
            }

            public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref Empty input,IHeapObject srcValue,ref byte[]? output,ref UpsertInfo upsertInfo) =>
                ThrowNotSupported<bool>();

            public bool InitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref Empty input,in TSourceLogRecord inputLogRecord,ref byte[]? output,ref UpsertInfo upsertInfo)
                where TSourceLogRecord : ISourceLogRecord =>
                ThrowNotSupported<bool>();

            public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref Empty input,ReadOnlySpan<byte> srcValue,ref byte[]? output,ref UpsertInfo upsertInfo) {
                return logRecord.TrySetValueSpanAndPrepareOptionals(srcValue,in sizeInfo);
            }

            public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref Empty input,IHeapObject srcValue,ref byte[]? output,ref UpsertInfo upsertInfo) =>
                ThrowNotSupported<bool>();

            public bool InPlaceWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref Empty input,in TSourceLogRecord inputLogRecord,ref byte[]? output,ref UpsertInfo upsertInfo)
                where TSourceLogRecord : ISourceLogRecord =>
                ThrowNotSupported<bool>();

            public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref Empty input,ReadOnlySpan<byte> srcValue,ref byte[]? output,ref UpsertInfo upsertInfo) {
            }

            public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref Empty input,IHeapObject srcValue,ref byte[]? output,ref UpsertInfo upsertInfo) =>
                throw new NotSupportedException();

            public void PostInitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref Empty input,in TSourceLogRecord inputLogRecord,ref byte[]? output,ref UpsertInfo upsertInfo)
                where TSourceLogRecord : ISourceLogRecord =>
                throw new NotSupportedException();

            public bool NeedInitialUpdate<TKey>(TKey key,ref Empty input,ref byte[]? output,ref RMWInfo rmwInfo)
#if NET10_0_OR_GREATER
                where TKey : IKey, allows ref struct
#else
                where TKey : IKey
#endif
                => ThrowNotSupported<bool>();

            public bool InitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref Empty input,ref byte[]? output,ref RMWInfo rmwInfo) =>
                ThrowNotSupported<bool>();

            public bool InPlaceUpdater(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref Empty input,ref byte[]? output,ref RMWInfo rmwInfo) =>
                ThrowNotSupported<bool>();

            public void PostInitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref Empty input,ref byte[]? output,ref RMWInfo rmwInfo) =>
                throw new NotSupportedException();

            public bool NeedCopyUpdate<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref Empty input,ref byte[]? output,ref RMWInfo rmwInfo)
                where TSourceLogRecord : ISourceLogRecord =>
                ThrowNotSupported<bool>();

            public bool CopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref Empty input,ref byte[]? output,ref RMWInfo rmwInfo)
                where TSourceLogRecord : ISourceLogRecord =>
                ThrowNotSupported<bool>();

            public bool PostCopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref Empty input,ref byte[]? output,ref RMWInfo rmwInfo)
                where TSourceLogRecord : ISourceLogRecord =>
                ThrowNotSupported<bool>();

            public void PostUpsertOperation<TKey,TEpochAccessor>(TKey key,ref Empty input,ReadOnlySpan<byte> valueSpan,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
#if NET10_0_OR_GREATER
                where TKey : IKey, allows ref struct
#else
                where TKey : IKey
#endif
                where TEpochAccessor : IEpochAccessor {
            }

            public void PostUpsertOperation<TKey,TEpochAccessor>(TKey key,ref Empty input,IHeapObject valueObject,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
#if NET10_0_OR_GREATER
                where TKey : IKey, allows ref struct
#else
                where TKey : IKey
#endif
                where TEpochAccessor : IEpochAccessor =>
                throw new NotSupportedException();

            public void PostRMWOperation<TKey,TEpochAccessor>(TKey key,ref Empty input,ref RMWInfo rmwInfo,TEpochAccessor epochAccessor)
#if NET10_0_OR_GREATER
                where TKey : IKey, allows ref struct
#else
                where TKey : IKey
#endif
                where TEpochAccessor : IEpochAccessor =>
                throw new NotSupportedException();

            public void RMWCompletionCallback(ref DiskLogRecord diskLogRecord,ref Empty input,ref byte[]? output,Empty ctx,Status status,RecordMetadata recordMetadata) =>
                throw new NotSupportedException();

            public RecordFieldInfo GetRMWModifiedFieldInfo<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref Empty input)
                where TSourceLogRecord : ISourceLogRecord =>
                ThrowNotSupported<RecordFieldInfo>();

            public RecordFieldInfo GetRMWInitialFieldInfo<TKey>(TKey key,ref Empty input)
#if NET10_0_OR_GREATER
                where TKey : IKey, allows ref struct
#else
                where TKey : IKey
#endif
                => ThrowNotSupported<RecordFieldInfo>();

            public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,ReadOnlySpan<byte> value,ref Empty input)
#if NET10_0_OR_GREATER
                where TKey : IKey, allows ref struct
#else
                where TKey : IKey
#endif
                => new() { KeySize=key.KeyBytes.Length,ValueSize=value.Length };

            public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,IHeapObject value,ref Empty input)
#if NET10_0_OR_GREATER
                where TKey : IKey, allows ref struct
#else
                where TKey : IKey
#endif
                => ThrowNotSupported<RecordFieldInfo>();

            public RecordFieldInfo GetUpsertFieldInfo<TKey,TSourceLogRecord>(TKey key,in TSourceLogRecord inputLogRecord,ref Empty input)
#if NET10_0_OR_GREATER
                where TKey : IKey, allows ref struct
#else
                where TKey : IKey
#endif
                where TSourceLogRecord : ISourceLogRecord =>
                ThrowNotSupported<RecordFieldInfo>();

            public bool InitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) =>
                ThrowNotSupported<bool>();

            public void PostInitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) =>
                throw new NotSupportedException();

            public bool InPlaceDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) =>
                ThrowNotSupported<bool>();

            public void PostDeleteOperation<TKey,TEpochAccessor>(TKey key,ref DeleteInfo deleteInfo,TEpochAccessor epochAccessor)
#if NET10_0_OR_GREATER
                where TKey : IKey, allows ref struct
#else
                where TKey : IKey
#endif
                where TEpochAccessor : IEpochAccessor =>
                throw new NotSupportedException();

            public void ConvertOutputToHeap(ref Empty input,ref byte[]? output) {
            }
        }
    }
}