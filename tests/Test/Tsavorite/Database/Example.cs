using System.Buffers;
using System.Runtime.CompilerServices;
using MemoryPack;
using Test.Tsavorite.Database.Example.Keys;

#if TSAVORITE1_1_1
using Tsavorite.core;
using StoreFunc = Tsavorite.core.StoreFunctions<Tsavorite.core.SpanByte,Tsavorite.core.SpanByte,Tsavorite.core.SpanByteComparer,Tsavorite.core.SpanByteRecordDisposer>;
using StoreAllocator = Tsavorite.core.SpanByteAllocator<Tsavorite.core.StoreFunctions<Tsavorite.core.SpanByte,Tsavorite.core.SpanByte,Tsavorite.core.SpanByteComparer,Tsavorite.core.SpanByteRecordDisposer>>;
#endif

#if TSAVORITE2_0BETA3
using Tsavorite.Stores;
#endif

namespace Test.Tsavorite.Database.Example {

    /// <summary>
    /// Tsavorite に「シリアライズ済み bytes」を保存し、Read 後に <see cref="Records.Type0.Stack"/> を通して
    /// MemoryPack デシリアライズ（= ref struct view からの復元）する使い方サンプル。
    /// </summary>
    public sealed class 使い方 {
        [Fact]
        public void 使い方0() {
            var 書き込みたいデータ = new Records.Type0(0,"key1",2,"value1");
            var key = 書き込みたいデータ.Key;

            // 値は「Read で bytes を受け取り、それを Stack view に渡して復元する」前提で bytes として保存する。
            var valueBytes = MemoryPackSerializer.Serialize(書き込みたいデータ);

#if TSAVORITE1_1_1
            var keyBytes = MemoryPackSerializer.Serialize(key);
            var readBytes = RoundTrip_Tsavorite1_1_1(keyBytes,valueBytes);
#elif TSAVORITE2_0BETA3
            var readBytes = RoundTrip_Tsavorite2_0BETA3(key,valueBytes);
#else
            // どちらの Tsavorite 定数も無い場合は、このファイルはサンプル用途なのでスキップ。
            return;
#endif

            // Read で得た bytes を Stack view に渡して復元 (implicit cast)
            Records.Type0 castedValue = new Records.Type0.Stack(readBytes);

            Assert.Equal(書き込みたいデータ.Column2,castedValue.Column2);
            Assert.Equal(書き込みたいデータ.Column3,castedValue.Column3);
        }

#if TSAVORITE1_1_1
        private static byte[] RoundTrip_Tsavorite1_1_1(byte[] keyBytes,byte[] valueBytes) {
            var folder = global::Test.Tsavorite.TestPath.Directory<使い方>();
            using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true);

            using var settings = new KVSettings<SpanByte,SpanByte> {
                IndexSize=1L<<6,
                MemorySize=global::Tsavorite.Const.MinObjectLogCapacity,
                PageSize=1L<<12,
                SegmentSize=1L<<20,
                MutableFraction=1,
                LogDevice=logDevice,
            };

            var storeFunctions = new StoreFunc(
                new SpanByteComparer(),
                static () => null,
                static () => null,
                new SpanByteRecordDisposer());

            using var store = new TsavoriteKV<SpanByte,SpanByte,StoreFunc,StoreAllocator>(
                settings,
                storeFunctions,
                static (allocSettings,funcs) => new StoreAllocator(allocSettings,funcs));

            using var session = store.NewSession<SpanByte,byte[]?,Empty,ByteStoreFunctions>(new());
            var basic = session.BasicContext;

            var key = SpanByte.FromPinnedSpan(keyBytes);
            var value = SpanByte.FromPinnedSpan(valueBytes);

            var status = basic.Upsert(ref key,ref value);
            if(status.IsPending)
                basic.CompletePending(wait: true);

            byte[]? output = null;
            status=basic.Read(ref key,ref output);
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

        private readonly struct ByteStoreFunctions:
            ISessionFunctions<SpanByte,SpanByte,SpanByte,byte[]?,Empty> {
            public bool SingleReader(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref byte[]? output,ref ReadInfo readInfo) {
                output=value.AsReadOnlySpan().ToArray();
                return true;
            }

            public bool ConcurrentReader(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref byte[]? output,ref ReadInfo readInfo,ref RecordInfo recordInfo) {
                output=value.AsReadOnlySpan().ToArray();
                return true;
            }

            public void ReadCompletionCallback(ref SpanByte key,ref SpanByte input,ref byte[]? output,Empty ctx,Status status,RecordMetadata recordMetadata) { }

            public bool SingleWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref byte[]? output,ref UpsertInfo upsertInfo,WriteReason reason,ref RecordInfo recordInfo) {
                src.CopyTo(ref dst);
                return true;
            }

            public void PostSingleWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref byte[]? output,ref UpsertInfo upsertInfo,WriteReason reason) { }

            public bool ConcurrentWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref byte[]? output,ref UpsertInfo upsertInfo,ref RecordInfo recordInfo) {
                if(src.Length>dst.Length)
                    return false;
                src.CopyTo(ref dst);
                return true;
            }

            public void PostUpsertOperation<TEpochAccessor>(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref UpsertInfo upsertInfo,TEpochAccessor epoch)
                where TEpochAccessor : IEpochAccessor { }

            public int GetUpsertValueLength(ref SpanByte value,ref SpanByte input) => value.Length+4;

            // このサンプルは Upsert / Read のみ使う (RMW / Delete は未対応)
            private static T ThrowNotSupported<T>() => throw new NotSupportedException();

            public bool NeedInitialUpdate(ref SpanByte key,ref SpanByte input,ref byte[]? output,ref RMWInfo rmwInfo) => ThrowNotSupported<bool>();
            public bool InitialUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref byte[]? output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) => ThrowNotSupported<bool>();
            public bool InPlaceUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref byte[]? output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) => ThrowNotSupported<bool>();
            public void PostInitialUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref byte[]? output,ref RMWInfo rmwInfo) => throw new NotSupportedException();
            public bool NeedCopyUpdate(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref byte[]? output,ref RMWInfo rmwInfo) => ThrowNotSupported<bool>();
            public bool CopyUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte oldValue,ref SpanByte newValue,ref byte[]? output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) => ThrowNotSupported<bool>();
            public bool PostCopyUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte oldValue,ref SpanByte newValue,ref byte[]? output,ref RMWInfo rmwInfo) => ThrowNotSupported<bool>();
            public int GetRMWModifiedValueLength(ref SpanByte value,ref SpanByte input) => ThrowNotSupported<int>();
            public int GetRMWInitialValueLength(ref SpanByte input) => ThrowNotSupported<int>();
            public void PostRMWOperation<TEpochAccessor>(ref SpanByte key,ref SpanByte input,ref RMWInfo rmwInfo,TEpochAccessor epoch)
                where TEpochAccessor : IEpochAccessor => throw new NotSupportedException();
            public void RMWCompletionCallback(ref SpanByte key,ref SpanByte input,ref byte[]? output,Empty ctx,Status status,RecordMetadata recordMetadata) => throw new NotSupportedException();
            public bool SingleDeleter(ref SpanByte key,ref SpanByte value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo) => ThrowNotSupported<bool>();
            public void PostSingleDeleter(ref SpanByte key,ref DeleteInfo deleteInfo) => throw new NotSupportedException();
            public bool ConcurrentDeleter(ref SpanByte key,ref SpanByte value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo) => ThrowNotSupported<bool>();
            public void PostDeleteOperation<TEpochAccessor>(ref SpanByte key,ref DeleteInfo deleteInfo,TEpochAccessor epoch)
                where TEpochAccessor : IEpochAccessor => throw new NotSupportedException();
            public void ConvertOutputToHeap(ref SpanByte input,ref byte[]? output) { }
        }
#endif

#if TSAVORITE2_0BETA3
        private static byte[] RoundTrip_Tsavorite2_0BETA3(Key0 key,byte[] valueBytes) {
            using var store = HashStore.Create().Create<Key0,byte[]>(new MemoryPackSerializerStrategy(),new MemoryPackSerializerStrategy());
            using var dict = store.Create();

            dict[key]=valueBytes;

            Assert.True(dict.TryGetValue(key,out var readBytes));
            Assert.NotNull(readBytes);
            return readBytes!;
        }
#endif
    }
    static class DatabaseGenerator;
    namespace Keys {
        public partial struct Key0 {
            public int Column0 { get; private init; } = Column0;
            public string Column1 { get; private init; } = Column1;
        }
    }
    namespace Records {
        public partial struct Type0 {
            public int Column2 { get; } = Column2;
            public string Column3 { get; } = Column3;
        }
    }

    namespace Keys {
        public readonly partial struct Key0(int Column0,string Column1):IMemoryPackable<Key0> {
            public static void RegisterFormatter() {
                if(!MemoryPackFormatterProvider.IsRegistered<Key0>()) {
                    MemoryPackFormatterProvider.Register(new MemoryPack.Formatters.MemoryPackableFormatter<Key0>());
                }
                if(!MemoryPackFormatterProvider.IsRegistered<Key0[]>()) {
                    MemoryPackFormatterProvider.Register(new MemoryPack.Formatters.ArrayFormatter<Key0>());
                }
            }
            public static void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer,scoped ref Key0 value) where TBufferWriter : IBufferWriter<byte> {
                writer.WriteVarInt(value.Column0);
                writer.WriteString(value.Column1);
            }
            public static void Deserialize(ref MemoryPackReader reader,scoped ref Key0 value) {
                value=new(reader.ReadVarIntInt32(),reader.ReadString()!);
            }
            public readonly ref struct Stack {
                private readonly ReadOnlySpan<byte> data;
                public Stack(ReadOnlySpan<byte> input) => this.data=input;

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public static implicit operator Key0(Stack view) =>
                    MemoryPackSerializer.Deserialize<Key0>(view.data)!
                    ;
            }
        }
    }

    namespace Records {
        public readonly partial struct Type0(Key0 Key,int Column2,string Column3):IMemoryPackable<Type0> {
            public Key0 Key { get; } = Key;

            public Type0(int Column0,string Column1,int Column2,string Column3) : this(new Key0(Column0,Column1),Column2,Column3) {
            }

            public readonly ref struct Stack {
                private readonly ReadOnlySpan<byte> data;
                public Stack(ReadOnlySpan<byte> input) => this.data=input;

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public static implicit operator Type0(Stack view) =>
                    MemoryPackSerializer.Deserialize<Type0>(view.data)!
                    ;
            }
            public static void RegisterFormatter() {
                if(!MemoryPackFormatterProvider.IsRegistered<Type0>()) {
                    MemoryPackFormatterProvider.Register(new MemoryPack.Formatters.MemoryPackableFormatter<Type0>());
                }
                if(!MemoryPackFormatterProvider.IsRegistered<Type0[]>()) {
                    MemoryPackFormatterProvider.Register(new MemoryPack.Formatters.ArrayFormatter<Type0>());
                }
            }
            public static void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer,scoped ref Type0 value) where TBufferWriter : IBufferWriter<byte> {
                var Key = value.Key;
                Key0.Serialize(ref writer,ref Key);
                writer.WriteVarInt(value.Column2);
                writer.WriteString(value.Column3);
            }
            public static void Deserialize(ref MemoryPackReader reader,scoped ref Type0 value) {
                Key0 Key = default;
                Key0.Deserialize(ref reader,ref Key);
                value=new(Key,reader.ReadVarIntInt32(),reader.ReadString()!);
            }
        }
    }
}
