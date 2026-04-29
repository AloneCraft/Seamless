using AdventureWorks2008R2.Tables;

using MemoryPack;

using System.Buffers;
using System.Diagnostics;
using System.Dynamic;
using Seamless.Linq;

using Test.Seamless.Benchmarks.Simple.Keys;
// ReSharper disable UnusedType.Global
#pragma warning disable CS8981 // 型名には、小文字の ASCII 文字のみが含まれています。このような名前は、プログラミング言語用に予約されている可能性があります。
//レコードというの複数の型の列がある。
//その中で主キーを構成する列をKeyとして定義する
//ユーザーはプロパティを定義する
//SGではKeyの列をUnamanagedの大きなデータ型から小さなデータ型を_Uに定義する。Managedを_Mに定義する。ユーザーはプロパティを定義する。
//バックエンドでは_UをBlittableに読み書きする。_MをMemoryPackableに読み書きする。
//主キー以外の列はTypeに定義する。SGではKeyの列をUnamanagedの大きなデータ型から小さなデータ型を_Uに定義する。Managedを_Mに定義する。ユーザーはプロパティを定義する。
//書き込むときは主キーをKey,それ以外をValueとしてシリアライズする。
//読み込むときは主キーをKeyにデシリアライズする。Valueはそれ以外をデシリアライズする。その後に.Keyフィールドに代入する
namespace Test.Seamless.Benchmarks.Simple {
    static class DatabaseGenerator;
    public class Container {
        public virtual IEnumerable<(T0 o,T1 i)> SelectManyQuery<T0, T1>(IEnumerable<T0> outer,IEnumerable<T1> inner) {
            return from o in outer
                   from i in inner
                   select (o,i);
        }
        public virtual IEnumerable<(T0 o,T1 i)> SelectManyMethod<T0, T1>(IEnumerable<T0> outer,IEnumerable<T1> inner) {
            return outer.SelectMany(o => inner,(o,i) => (o,i));
        }
        public virtual IEnumerable<(T o,T p)> WhereSelectQuery<T>(IEnumerable<T> outer,T p) {
            return
                from o in outer
                where o.Equals(p)
                select (o,p);
        }
        public virtual IEnumerable<(T o,T p)> WhereSelectMethod<T>(IEnumerable<T> outer,T p) {
            return outer.Where(o => o.Equals(p)).Select(o => (o,p));
        }
        public virtual IEnumerable<(T0 o,T1 i)> Query2<T0, T1>(global::Seamless.Collections.IHashSet<T0> outer,global::Seamless.Collections.IHashSet<T1> inner)
            where T0 : IEquatable<T0>
            where T1 : IEquatable<T1> {
            //HashSet.Select()
            return Enumerable.SelectMany(outer,o => inner,(o,i) => (o,i));
            //from o in outer
            //    from i in inner
            //    select (o,i);
        }
        public virtual T AggregateMethod<T>(IEnumerable<T> outer) {
            return outer.Aggregate((a,b) => a);
        }
        public virtual IEnumerable<int> WhereAfterAnonymousSelect(int minimumColumn1) {
            return this.Type0Table
                .Select(row => new { row.Column0,row.Column1 })
                .Where(row => row.Column1>=minimumColumn1)
                .Select(row => row.Column0);
        }
        public virtual IEnumerable<int> WhereAfterTupleSelect(int minimumColumn1) {
            return this.Type0Table
                .Select(row => (row.Column0,row.Column1))
                .Where(row => row.Column1>=minimumColumn1)
                .Select(row => row.Column0);
        }
        public virtual IEnumerable<int> WhereAfterJoin(int minimumColumn1) {
            return this.Type0Table
                .Join(this.Type0TableFunction(minimumColumn1),outer => outer.Column0,inner => inner.Column0,(outer,inner) => new { outer,inner })
                .Where(pair => pair.outer.Column1>=minimumColumn1)
                .Select(pair => pair.inner.Column1);
        }
        public virtual IEnumerable<int> WhereAfterExcept(int minimumColumn1) {
            return this.Type0Table
                .Select(row => row.Column1)
                .Except(this.Type0Table.Select(row => row.Column0))
                .Where(value => value>=minimumColumn1);
        }
        public virtual List<Types.Type0> Type0Table =>
            new() {
                new Types.Type0(1,10),
                new Types.Type0(2,20),
                new Types.Type0(3,30),
            };

        public virtual List<Types.Type0> Type0TableFunction(int minimumColumn1) =>
            this.Type0Table;

        public virtual IEnumerable<Types.Type0> FilterType0(int minimumColumn1) =>
            from row in this.Type0Table
            where row.Column1>=minimumColumn1
            select row;

        public virtual IEnumerable<int> ProjectType0Column1(int minimumColumn1) =>
            from row in this.Type0TableFunction(minimumColumn1)
            where row.Column1>=minimumColumn1
            select row.Column1;
    }

    namespace Keys {
        public partial struct Key0 {
            public partial int Column0 { get; }
        }
        public partial struct Key1 {
            public partial int Column0 { get; }
            public partial int Column1 { get; }
        }
        public partial struct Key2 {
            public partial string Column0 { get; }
            public partial int Column1 { get; }
            public partial int Column2 { get; }
        }
    }
    namespace Types {
        public partial struct Type0 {
            public partial int Column0 { get; }
            public partial int Column1 { get; }
        }
        public partial struct Type1 {
            public partial int Column0 { get; }
            public partial int Column1 { get; }
            public partial int Column2 { get; }
            public partial int Column3 { get; }
        }
        public partial struct Type2 {
            public partial string Column0 { get; }
            public partial int Column1 { get; }
            public partial int Column2 { get; }
            public partial int Column3 { get; }
        }
    }
}
/*
namespace Test.Seamless.Benchmarks.Simple {
    namespace Keys {
        //[MemoryPackable]
        partial struct Key0:IMemoryPackable<Key0> {
            public Key0(int Column0) {
                this._u=new(Column0);
                this._m=new();
            }
            private readonly record struct _U(int Column0);
            private _U _u;
            private struct _M:IMemoryPackable<_M> {
                public static void RegisterFormatter() {
                    throw new NotImplementedException();
                }
                public static void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer,scoped ref _M value)
                    where TBufferWriter : IBufferWriter<byte> {
                }
                public static void Deserialize(ref MemoryPackReader reader,scoped ref _M value) {
                }
            }
            private _M _m;
            public partial int Column0 => this._u.Column0;
            static Key0() {
                global::MemoryPack.MemoryPackFormatterProvider.Register<Key0>();
            }
            static void IMemoryPackFormatterRegister.RegisterFormatter() {
                if(!global::MemoryPack.MemoryPackFormatterProvider.IsRegistered<Key0>()) {
                    global::MemoryPack.MemoryPackFormatterProvider.Register(new global::MemoryPack.Formatters.MemoryPackableFormatter<Key0>());
                }
                if(!global::MemoryPack.MemoryPackFormatterProvider.IsRegistered<Key0[]>()) {
                    global::MemoryPack.MemoryPackFormatterProvider.Register(new global::MemoryPack.Formatters.ArrayFormatter<Key0>());
                }

            }

            static void IMemoryPackable<Key0>.Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer,scoped ref Key0 value) {
                writer.WriteUnmanaged(value._u);
                writer.WritePackable(value._m);
            }
            static void IMemoryPackable<Key0>.Deserialize(ref MemoryPackReader reader,scoped ref Key0 value) {

                reader.ReadUnmanaged(out value._u);
                reader.ReadPackable(ref value._m);
            }
        }
        partial struct Key1:IMemoryPackable<Key1> {
            public Key1(int Column0,int Column1) {
                this._u=new(Column0,Column1);
                this._m=new();
            }
            private readonly record struct _U(int Column0,int Column1);
            private _U _u;
            private struct _M:IMemoryPackable<_M> {
                public static void RegisterFormatter() {
                    throw new NotImplementedException();
                }
                public static void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer,scoped ref _M value)
                    where TBufferWriter : IBufferWriter<byte> {
                }
                public static void Deserialize(ref MemoryPackReader reader,scoped ref _M value) {
                }
            }
            private _M _m;
            public partial int Column0 => this._u.Column0;
            public partial int Column1 => this._u.Column1;
            static Key1() {
                global::MemoryPack.MemoryPackFormatterProvider.Register<Key1>();
            }
            static void IMemoryPackFormatterRegister.RegisterFormatter() {
                if(!global::MemoryPack.MemoryPackFormatterProvider.IsRegistered<Key1>()) {
                    global::MemoryPack.MemoryPackFormatterProvider.Register(new global::MemoryPack.Formatters.MemoryPackableFormatter<Key1>());
                }
                if(!global::MemoryPack.MemoryPackFormatterProvider.IsRegistered<Key1[]>()) {
                    global::MemoryPack.MemoryPackFormatterProvider.Register(new global::MemoryPack.Formatters.ArrayFormatter<Key1>());
                }

            }
            public static void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer,scoped ref Key1 value)
                where TBufferWriter : IBufferWriter<byte> {
                writer.WriteUnmanaged(value._u);
                writer.WritePackable(value._m);
            }
            public static void Deserialize(ref MemoryPackReader reader,scoped ref Key1 value) {
                reader.ReadUnmanaged(out value._u);
                reader.ReadPackable(ref value._m);
            }
        }
        partial struct Key2:IMemoryPackable<Key2> {
            public Key2(string Column0,int Column1,int Column2) {
                this._u=new(Column1,Column2);
                this._m=new(Column0);
            }
            private readonly record struct _U(int Column1,int Column2);
            private _U _u;
            private struct _M(string Column0):IMemoryPackable<_M> {
                public string Column0 = Column0;
                public static void RegisterFormatter() {
                    throw new NotImplementedException();
                }
                public static void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer,scoped ref _M value)
                    where TBufferWriter : IBufferWriter<byte> {
                    writer.WriteString(value.Column0);
                }
                public static void Deserialize(ref MemoryPackReader reader,scoped ref _M value) {
                    value.Column0=reader.ReadString();
                }
            }
            private _M _m;
            public partial string Column0 => this._m.Column0;
            public partial int Column1 => this._u.Column1;
            public partial int Column2 => this._u.Column2;
            static Key2() {
                global::MemoryPack.MemoryPackFormatterProvider.Register<Key2>();
            }
            static void IMemoryPackFormatterRegister.RegisterFormatter() {
                if(!global::MemoryPack.MemoryPackFormatterProvider.IsRegistered<Key2>()) {
                    global::MemoryPack.MemoryPackFormatterProvider.Register(new global::MemoryPack.Formatters.MemoryPackableFormatter<Key2>());
                }
                if(!global::MemoryPack.MemoryPackFormatterProvider.IsRegistered<Key2[]>()) {
                    global::MemoryPack.MemoryPackFormatterProvider.Register(new global::MemoryPack.Formatters.ArrayFormatter<Key2>());
                }

            }
            public static void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer,scoped ref Key2 value)
                where TBufferWriter : IBufferWriter<byte> {
                writer.WriteUnmanaged(value._u);
                writer.WritePackable(value._m);
            }
            public static void Deserialize(ref MemoryPackReader reader,scoped ref Key2 value) {
                reader.ReadUnmanaged(out value._u);
                reader.ReadPackable(ref value._m);
            }
        }
    }
    namespace Types {
        partial struct Type0:IMemoryPackable<Type0> {
            public Type0(Keys.Key0 Key) {
                this.Key=Key;
            }
            public Type0(int Column0,int Column1) : this(new Keys.Key0(Column0)) {
                this._u=new(Column1);
                this._m=new();
            }
            public readonly Keys.Key0 Key;
            private readonly record struct _U(int Column1);
            private _U _u;
            private record struct _M(string n_name):IMemoryPackable<_M> {
                public static void RegisterFormatter() {
                    throw new NotImplementedException();
                }
                public static void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer,scoped ref _M value)
                    where TBufferWriter : IBufferWriter<byte> {
                    writer.WriteString(value.n_name);
                }
                public static void Deserialize(ref MemoryPackReader reader,scoped ref _M value) {
                    value.n_name=reader.ReadString();
                }
            }
            private _M _m;
            public partial int Column0 => this.Key.Column0;
            public partial int Column1 => this._u.Column1;
            static Type0() {
                global::MemoryPack.MemoryPackFormatterProvider.Register<Type0>();
            }
            static void IMemoryPackFormatterRegister.RegisterFormatter() {
                if(!global::MemoryPack.MemoryPackFormatterProvider.IsRegistered<Type0>()) {
                    global::MemoryPack.MemoryPackFormatterProvider.Register(new global::MemoryPack.Formatters.MemoryPackableFormatter<Type0>());
                }
                if(!global::MemoryPack.MemoryPackFormatterProvider.IsRegistered<Type0[]>()) {
                    global::MemoryPack.MemoryPackFormatterProvider.Register(new global::MemoryPack.Formatters.ArrayFormatter<Type0>());
                }

            }

            static void IMemoryPackable<Type0>.Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer,scoped ref Type0 value) {
                writer.WriteUnmanaged(value._u);
                writer.WritePackable(value._m);
            }
            static void IMemoryPackable<Type0>.Deserialize(ref MemoryPackReader reader,scoped ref Type0 value) {
                reader.ReadUnmanaged(out value._u);
                reader.ReadPackable(ref value._m);
            }
        }
        partial struct Type1:IMemoryPackable<Type1> {
            public Type1(Keys.Key1 Key) {
                this.Key=Key;
            }
            public Type1(int Column0,int Column1,int Column2,int Column3) : this(new Keys.Key1(Column0,Column1)) {
                this._u=new(Column2,Column3);
                this._m=new();
            }
            private readonly Keys.Key1 Key;
            private readonly record struct _U(int Column2,int Column3);
            private _U _u;
            private struct _M:IMemoryPackable<_M> {
                public static void RegisterFormatter() {
                    throw new NotImplementedException();
                }
                public static void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer,scoped ref _M value)
                    where TBufferWriter : IBufferWriter<byte> {
                }
                public static void Deserialize(ref MemoryPackReader reader,scoped ref _M value) {
                }
            }
            private _M _m;
            public partial int Column0 => this.Key.Column0;
            public partial int Column1 => this.Key.Column1;
            public partial int Column2 => this._u.Column2;
            public partial int Column3 => this._u.Column3;
            static Type1() {
                global::MemoryPack.MemoryPackFormatterProvider.Register<Type1>();
            }
            static void IMemoryPackFormatterRegister.RegisterFormatter() {
                if(!global::MemoryPack.MemoryPackFormatterProvider.IsRegistered<Type1>()) {
                    global::MemoryPack.MemoryPackFormatterProvider.Register(new global::MemoryPack.Formatters.MemoryPackableFormatter<Key1>());
                }
                if(!global::MemoryPack.MemoryPackFormatterProvider.IsRegistered<Type1[]>()) {
                    global::MemoryPack.MemoryPackFormatterProvider.Register(new global::MemoryPack.Formatters.ArrayFormatter<Key1>());
                }

            }
            public static void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer,scoped ref Type1 value)
                where TBufferWriter : IBufferWriter<byte> {
                writer.WriteUnmanaged(value._u);
                writer.WritePackable(value._m);
            }
            public static void Deserialize(ref MemoryPackReader reader,scoped ref Type1 value) {
                reader.ReadUnmanaged(out value._u);
                reader.ReadPackable(ref value._m);
            }
        }
        partial struct Type2:IMemoryPackable<Type2> {
            public Type2(Keys.Key2 Key) {
                this.Key=Key;
            }
            public Type2(string Column0,int Column1,int Column2,int Column3) : this(new Keys.Key2(Column0,Column1,Column2)) {
                this._u=new(Column3);
                this._m=new();
            }
            private readonly Keys.Key2 Key;
            private readonly record struct _U(int Column3);
            private _U _u;
            private struct _M:IMemoryPackable<_M> {
                public string r_name;
                public string r_comment;
                public static void RegisterFormatter() {
                    throw new NotImplementedException();
                }
                public static void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer,scoped ref _M value)
                    where TBufferWriter : IBufferWriter<byte> {
                    writer.WriteString(value.r_name);
                    writer.WriteString(value.r_comment);
                }
                public static void Deserialize(ref MemoryPackReader reader,scoped ref _M value) {
                    value.r_name=reader.ReadString();
                    value.r_comment=reader.ReadString();
                }
            }
            private _M _m;
            public partial string Column0 => this.Key.Column0;
            public partial int Column1 => this.Key.Column1;
            public partial int Column2 => this.Key.Column2;
            public partial int Column3 => this._u.Column3;
            static Type2() {
                global::MemoryPack.MemoryPackFormatterProvider.Register<Type2>();
            }
            static void IMemoryPackFormatterRegister.RegisterFormatter() {
                if(!global::MemoryPack.MemoryPackFormatterProvider.IsRegistered<Type2>()) {
                    global::MemoryPack.MemoryPackFormatterProvider.Register(new global::MemoryPack.Formatters.MemoryPackableFormatter<Type2>());
                }
                if(!global::MemoryPack.MemoryPackFormatterProvider.IsRegistered<Type2[]>()) {
                    global::MemoryPack.MemoryPackFormatterProvider.Register(new global::MemoryPack.Formatters.ArrayFormatter<Type2>());
                }
            }
            public static void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer,scoped ref Type2 value)
                where TBufferWriter : IBufferWriter<byte> {
                writer.WriteUnmanaged(value._u);
                writer.WritePackable(value._m);
            }
            public static void Deserialize(ref MemoryPackReader reader,scoped ref Type2 value) {
                reader.ReadUnmanaged(out value._u);
                reader.ReadPackable(ref value._m);
            }
        }
    }
}
*/
