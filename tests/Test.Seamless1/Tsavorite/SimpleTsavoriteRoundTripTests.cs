#if TSAVORITE1_1_1
using MemoryPack;
using Tsavorite.core;

using Key0 = Test.Seamless.Tsavorite.RoundTripSchema.Key0;
using Key1 = Test.Seamless.Tsavorite.RoundTripSchema.Key1;
using Key2 = Test.Seamless.Tsavorite.RoundTripSchema.Key2;
using Type0 = Test.Seamless.Tsavorite.RoundTripSchema.Type0;
using Type1 = Test.Seamless.Tsavorite.RoundTripSchema.Type1;
using Type2 = Test.Seamless.Tsavorite.RoundTripSchema.Type2;

using StoreFunc = Tsavorite.core.StoreFunctions<Tsavorite.core.SpanByte,Tsavorite.core.SpanByte,Tsavorite.core.SpanByteComparer,Tsavorite.core.SpanByteRecordDisposer>;
using StoreAllocator = Tsavorite.core.SpanByteAllocator<Tsavorite.core.StoreFunctions<Tsavorite.core.SpanByte,Tsavorite.core.SpanByte,Tsavorite.core.SpanByteComparer,Tsavorite.core.SpanByteRecordDisposer>>;

namespace Test.Seamless.Tsavorite {
    internal static partial class RoundTripSchema {
        [MemoryPackable]
        public readonly partial struct Key0 {
            public Key0(int column0) => this.Column0=column0;
            public int Column0 { get; }
        }

        [MemoryPackable]
        public readonly partial struct Key1 {
            public Key1(int column0,int column1) {
                this.Column0=column0;
                this.Column1=column1;
            }
            public int Column0 { get; }
            public int Column1 { get; }
        }

        [MemoryPackable]
        public readonly partial struct Key2 {
            [MemoryPackConstructor]
            public Key2(string column0,int column1,int column2) {
                this.Column0=column0;
                this.Column1=column1;
                this.Column2=column2;
            }
            public string Column0 { get; }
            public int Column1 { get; }
            public int Column2 { get; }
        }

        [MemoryPackable]
        public readonly partial struct Type0 {
            public Type0(int column0,int column1) {
                this.Column0=column0;
                this.Column1=column1;
            }
            public int Column0 { get; }
            public int Column1 { get; }
        }

        [MemoryPackable]
        public readonly partial struct Type1 {
            public Type1(int column0,int column1,int column2,int column3) {
                this.Column0=column0;
                this.Column1=column1;
                this.Column2=column2;
                this.Column3=column3;
            }
            public int Column0 { get; }
            public int Column1 { get; }
            public int Column2 { get; }
            public int Column3 { get; }
        }

        [MemoryPackable]
        public readonly partial struct Type2 {
            [MemoryPackConstructor]
            public Type2(string column0,int column1,int column2,int column3) {
                this.Column0=column0;
                this.Column1=column1;
                this.Column2=column2;
                this.Column3=column3;
            }
            public string Column0 { get; }
            public int Column1 { get; }
            public int Column2 { get; }
            public int Column3 { get; }
        }
    }

    [Collection("TsavoriteTests")]
    public sealed class SimpleTsavoriteRoundTripTests {
        private static string CreateFolder() => global::Test.Seamless.TestPath.Directory<SimpleTsavoriteRoundTripTests>();

        private static byte[] Serialize<T>(T value) => MemoryPackSerializer.Serialize(value);

        private static T Deserialize<T>(ReadOnlySpan<byte> bytes) =>
            MemoryPackSerializer.Deserialize<T>(bytes)!;

        [Fact, Trait("Category","Unit")]
        public void Key0_Record0_CanRoundTrip_WithoutDictionary() {
            var key = new Key0(10);
            var expected = new Type0(10,20);
            var keyBytes = Serialize(key);
            var actualValueBytes = RoundTrip(keyBytes,Serialize(expected));
            var actual = Rehydrate(Deserialize<Key0>(keyBytes),actualValueBytes);

            Assert.Equal(expected.Column0,actual.Column0);
            Assert.Equal(expected.Column1,actual.Column1);
        }

        [Fact, Trait("Category","Unit")]
        public void Key1_Record1_CanRoundTrip_WithoutDictionary() {
            var key = new Key1(11,12);
            var expected = new Type1(11,12,13,14);
            var keyBytes = Serialize(key);
            var actualValueBytes = RoundTrip(keyBytes,Serialize(expected));
            var actual = Rehydrate(Deserialize<Key1>(keyBytes),actualValueBytes);

            Assert.Equal(expected.Column0,actual.Column0);
            Assert.Equal(expected.Column1,actual.Column1);
            Assert.Equal(expected.Column2,actual.Column2);
            Assert.Equal(expected.Column3,actual.Column3);
        }

        [Fact, Trait("Category","Unit")]
        public void Key2_Record2_CanRoundTrip_WithoutDictionary() {
            var key = new Key2("alpha",21,22);
            var expected = new Type2("alpha",21,22,23);
            var keyBytes = Serialize(key);
            var actualValueBytes = RoundTrip(keyBytes,Serialize(expected));
            var actual = Rehydrate(Deserialize<Key2>(keyBytes),actualValueBytes);

            Assert.Equal(expected.Column0,actual.Column0);
            Assert.Equal(expected.Column1,actual.Column1);
            Assert.Equal(expected.Column2,actual.Column2);
            Assert.Equal(expected.Column3,actual.Column3);
        }

        private static Type0 Rehydrate(Key0 key,ReadOnlySpan<byte> valueBytes) {
            var value = Deserialize<Type0>(valueBytes);
            return new Type0(key.Column0,value.Column1);
        }

        private static Type1 Rehydrate(Key1 key,ReadOnlySpan<byte> valueBytes) {
            var value = Deserialize<Type1>(valueBytes);
            return new Type1(key.Column0,key.Column1,value.Column2,value.Column3);
        }

        private static Type2 Rehydrate(Key2 key,ReadOnlySpan<byte> valueBytes) {
            var value = Deserialize<Type2>(valueBytes);
            return new Type2(key.Column0,key.Column1,key.Column2,value.Column3);
        }

        private static byte[] RoundTrip(byte[] keyBytes,byte[] valueBytes) {
            using var handle = new NativeStoreHandle(CreateFolder());
            using var session = handle.Store.NewSession<SpanByte,byte[]?,Empty,ByteStoreFunctions>(new());
            var basicContext = session.BasicContext;
            var key = SpanByte.FromPinnedSpan(keyBytes);
            var value = SpanByte.FromPinnedSpan(valueBytes);

            var status = basicContext.Upsert(ref key,ref value);
            if(status.IsPending)
                basicContext.CompletePending(wait: true);

            byte[]? output = null;
            status=basicContext.Read(ref key,ref output);
            status=CompletePending(session,status,ref output);

            Assert.True(status.Found);
            Assert.NotNull(output);
            return output!;
        }

        private static Status CompletePending(
            ClientSession<SpanByte,SpanByte,SpanByte,byte[]?,Empty,ByteStoreFunctions,StoreFunc,StoreAllocator> session,
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
            internal readonly TsavoriteKV<SpanByte,SpanByte,StoreFunc,StoreAllocator> Store;

            public NativeStoreHandle(string folder) {
                this.folder=folder;
                this.logDevice=Devices.CreateLogDevice(
                    Path.Combine(folder,"log.data"),
                    deleteOnClose: true,
                    capacity: global::Seamless.Const.MinObjectLogCapacity);

                var settings = new KVSettings<SpanByte,SpanByte> {
                    IndexSize=1L<<6,
                    MemorySize=global::Seamless.Const.MinObjectLogCapacity,
                    PageSize=1L<<12,
                    SegmentSize=1L<<20,
                    MutableFraction=1,
                    LogDevice=this.logDevice,
                };

                var storeFunctions = new StoreFunc(
                    new SpanByteComparer(),
                    static () => null,
                    static () => null,
                    new SpanByteRecordDisposer());

                this.Store=new TsavoriteKV<SpanByte,SpanByte,StoreFunc,StoreAllocator>(
                    settings,
                    storeFunctions,
                    static (allocSettings,storeFuncs) => new StoreAllocator(allocSettings,storeFuncs));
            }

            public void Dispose() {
                this.Store.Log.FlushAndEvict(wait: true);
                this.Store.Dispose();
                this.logDevice.Dispose();
                try { Directory.Delete(this.folder,recursive: true); } catch { }
            }
        }

        private readonly struct ByteStoreFunctions:
            ISessionFunctions<SpanByte,SpanByte,SpanByte,byte[]?,Empty> {

            private static T ThrowNotSupported<T>() => throw new NotSupportedException();

            public bool SingleReader(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref byte[]? output,ref ReadInfo readInfo) {
                output=value.AsReadOnlySpan().ToArray();
                return true;
            }

            public bool ConcurrentReader(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref byte[]? output,ref ReadInfo readInfo,ref RecordInfo recordInfo) {
                output=value.AsReadOnlySpan().ToArray();
                return true;
            }

            public void ReadCompletionCallback(ref SpanByte key,ref SpanByte input,ref byte[]? output,Empty ctx,Status status,RecordMetadata recordMetadata) {
            }

            public bool SingleWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref byte[]? output,ref UpsertInfo upsertInfo,WriteReason reason,ref RecordInfo recordInfo) {
                src.CopyTo(ref dst);
                return true;
            }

            public void PostSingleWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref byte[]? output,ref UpsertInfo upsertInfo,WriteReason reason) {
            }

            public bool ConcurrentWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref byte[]? output,ref UpsertInfo upsertInfo,ref RecordInfo recordInfo) {
                if(src.Length>dst.Length)
                    return false;
                src.CopyTo(ref dst);
                return true;
            }

            public void PostUpsertOperation<TEpochAccessor>(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref UpsertInfo upsertInfo,TEpochAccessor epoch)
                where TEpochAccessor : IEpochAccessor {
            }

            public bool NeedInitialUpdate(ref SpanByte key,ref SpanByte input,ref byte[]? output,ref RMWInfo rmwInfo) =>
                ThrowNotSupported<bool>();

            public bool InitialUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref byte[]? output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) =>
                ThrowNotSupported<bool>();

            public bool InPlaceUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref byte[]? output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) =>
                ThrowNotSupported<bool>();

            public void PostInitialUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref byte[]? output,ref RMWInfo rmwInfo) =>
                throw new NotSupportedException();

            public bool NeedCopyUpdate(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref byte[]? output,ref RMWInfo rmwInfo) =>
                ThrowNotSupported<bool>();

            public bool CopyUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte oldValue,ref SpanByte newValue,ref byte[]? output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) =>
                ThrowNotSupported<bool>();

            public bool PostCopyUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte oldValue,ref SpanByte newValue,ref byte[]? output,ref RMWInfo rmwInfo) =>
                ThrowNotSupported<bool>();

            public int GetUpsertValueLength(ref SpanByte value,ref SpanByte input) => value.Length+4;

            public int GetRMWModifiedValueLength(ref SpanByte value,ref SpanByte input) =>
                ThrowNotSupported<int>();

            public int GetRMWInitialValueLength(ref SpanByte input) =>
                ThrowNotSupported<int>();

            public void PostRMWOperation<TEpochAccessor>(ref SpanByte key,ref SpanByte input,ref RMWInfo rmwInfo,TEpochAccessor epoch)
                where TEpochAccessor : IEpochAccessor =>
                throw new NotSupportedException();

            public void RMWCompletionCallback(ref SpanByte key,ref SpanByte input,ref byte[]? output,Empty ctx,Status status,RecordMetadata recordMetadata) =>
                throw new NotSupportedException();

            public bool SingleDeleter(ref SpanByte key,ref SpanByte value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo) =>
                ThrowNotSupported<bool>();

            public void PostSingleDeleter(ref SpanByte key,ref DeleteInfo deleteInfo) =>
                throw new NotSupportedException();

            public bool ConcurrentDeleter(ref SpanByte key,ref SpanByte value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo) =>
                ThrowNotSupported<bool>();

            public void PostDeleteOperation<TEpochAccessor>(ref SpanByte key,ref DeleteInfo deleteInfo,TEpochAccessor epoch)
                where TEpochAccessor : IEpochAccessor =>
                throw new NotSupportedException();

            public void ConvertOutputToHeap(ref SpanByte input,ref byte[]? output) {
            }
        }
    }
}
#endif
