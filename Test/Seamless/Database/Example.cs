using System.Buffers;
using System.Runtime.CompilerServices;
using MemoryPack;
using Test.Seamless.Database.Example.Keys;
namespace Test.Seamless.Database.Example {

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

            var readBytes = RoundTrip_Tsavorite2_0BETA3(key,valueBytes);

            // Read で得た bytes を Stack view に渡して復元 (implicit cast)
            Records.Type0 castedValue = new Records.Type0.Stack(readBytes);

            Assert.Equal(書き込みたいデータ.Column2,castedValue.Column2);
            Assert.Equal(書き込みたいデータ.Column3,castedValue.Column3);
        }

        private static byte[] RoundTrip_Tsavorite2_0BETA3(Key0 key,byte[] valueBytes) {
            using var store = Store.Create().Create<Key0,byte[]>(new MemoryPackSerializerStrategy(),new MemoryPackSerializerStrategy());
            using var dict = store.Create();

            dict[key]=valueBytes;

            Assert.True(dict.TryGetValue(key,out var readBytes));
            Assert.NotNull(readBytes);
            return readBytes!;
        }
    }
    internal static class DatabaseGenerator;
    namespace Keys {
        public partial struct Key0 {
            public int Column0 { get; private init; } = Column0;
            public string Column1 { get; private init; } = Column1;
        }
    }
    namespace Records {
        #pragma warning disable CS0282
        public partial struct Type0 {
            public int Column2 { get; } = Column2;
            public string Column3 { get; } = Column3;
        }
        #pragma warning restore CS0282
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
