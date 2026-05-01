using System.Buffers;
using MemoryPack;
// ReSharper disable UnusedType.Global
#pragma warning disable CS8981 // 型名には、小文字の ASCII 文字のみが含まれています。このような名前は、プログラミング言語用に予約されている可能性があります。
//レコードというの複数の型の列がある。
//その中で主キーを構成する列をKeyとして定義する
//ユーザーはプロパティを定義する
//SGではKeyの列をUnamanagedの大きなデータ型から小さなデータ型を_Uに定義する。Managedを_Mに定義する。ユーザーはプロパティを定義する。
//バックエンドでは_UをBlittableに読み書きする。_MをMemoryPackableに読み書きする。
//主キー以外の列はRecordに定義する。SGではKeyの列をUnamanagedの大きなデータ型から小さなデータ型を_Uに定義する。Managedを_Mに定義する。ユーザーはプロパティを定義する。
//書き込むときは主キーをKey,それ以外をValueとしてシリアライズする。
//読み込むときは主キーをKeyにデシリアライズする。Valueはそれ以外をデシリアライズする。その後に.Keyフィールドに代入する
namespace Test.Seamless.Database.TPC_H{

    static class SourceGenerator;
    namespace Keys{
        public partial struct nation{
            public partial int n_nationkey{get;}
        }
        public partial struct region{
            public partial int r_regionkey{get;}
        }
    }
    namespace Records{
        public partial struct nation{
            public partial int n_nationkey{get;}
            public partial string n_name{get;}
        }
        public partial struct region{
            public partial int r_regionkey{get;}
            public partial string r_name{get;}
            public partial string r_comment{get;}
        }
    }
}
namespace Test.Seamless.Database.TPC_H{
    namespace Keys{
        //[MemoryPackable]
        partial struct nation:IMemoryPackable<nation>{
            public nation(int n_nationkeys){
                this._u=new(n_nationkeys);
                this._m=new();
            }
            private readonly record struct _U(int n_nationkeys);
            private _U _u;
            private struct _M:IMemoryPackable<_M>{
                public static void RegisterFormatter(){
                    throw new NotImplementedException();
                }
                public static void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer,scoped ref _M value)
                    where TBufferWriter:IBufferWriter<byte>{
                }
                public static void Deserialize(ref MemoryPackReader reader,scoped ref _M value){
                }
            }
            private _M _m;
            public partial int n_nationkey=>this._u.n_nationkeys;
            static nation(){
                global::MemoryPack.MemoryPackFormatterProvider.Register<nation>();
            }
            static void IMemoryPackFormatterRegister.RegisterFormatter(){
                if(!global::MemoryPack.MemoryPackFormatterProvider.IsRegistered<nation>()){
                    global::MemoryPack.MemoryPackFormatterProvider.Register(new global::MemoryPack.Formatters.MemoryPackableFormatter<nation>());
                }
                if(!global::MemoryPack.MemoryPackFormatterProvider.IsRegistered<nation[]>()){
                    global::MemoryPack.MemoryPackFormatterProvider.Register(new global::MemoryPack.Formatters.ArrayFormatter<nation>());
                }

            }

            static void IMemoryPackable<nation>.Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer,scoped ref nation value){
                writer.WriteUnmanaged(value._u);
                writer.WritePackable(value._m);
            }
            static void IMemoryPackable<nation>.Deserialize(ref MemoryPackReader reader,scoped ref nation value){

                reader.ReadUnmanaged(out value._u);
                reader.ReadPackable(ref value._m);
            }
        }
        partial struct region:IMemoryPackable<region>{
            public region(int r_regionkey){
                this._u=new(r_regionkey);
                this._m=new();
            }
            private readonly record struct _U(int r_regionkey);
            private _U _u;
            private struct _M:IMemoryPackable<_M>{
                public static void RegisterFormatter(){
                    throw new NotImplementedException();
                }
                public static void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer,scoped ref _M value)
                    where TBufferWriter:IBufferWriter<byte>{
                }
                public static void Deserialize(ref MemoryPackReader reader,scoped ref _M value){
                }
            }
            private _M _m;
            public partial int r_regionkey=>this._u.r_regionkey;
            static region(){
                global::MemoryPack.MemoryPackFormatterProvider.Register<region>();
            }
            static void IMemoryPackFormatterRegister.RegisterFormatter(){
                if(!global::MemoryPack.MemoryPackFormatterProvider.IsRegistered<region>()){
                    global::MemoryPack.MemoryPackFormatterProvider.Register(new global::MemoryPack.Formatters.MemoryPackableFormatter<region>());
                }
                if(!global::MemoryPack.MemoryPackFormatterProvider.IsRegistered<region[]>()){
                    global::MemoryPack.MemoryPackFormatterProvider.Register(new global::MemoryPack.Formatters.ArrayFormatter<region>());
                }

            }
            public static void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer,scoped ref region value)
                where TBufferWriter:IBufferWriter<byte>{
                writer.WriteUnmanaged(value._u);
                writer.WritePackable(value._m);
            }
            public static void Deserialize(ref MemoryPackReader reader,scoped ref region value){
                reader.ReadUnmanaged(out value._u);
                reader.ReadPackable(ref value._m);
            }
        }
    }
    namespace Records{
        //[MemoryPackable]
        partial struct nation:IMemoryPackable<nation>{
            public nation(Keys.nation Key){
                this.Key=Key;
            }
            public nation(int n_nationkeys):this(new Keys.nation(n_nationkeys)){
                this._u=new();
                this._m=new();
            }
            public readonly Keys.nation Key;
            private readonly record struct _U;
            private _U _u;
            private record struct _M(string n_name):IMemoryPackable<_M>{
                //public 
                //public string n_name;
                public static void RegisterFormatter(){
                    throw new NotImplementedException();
                }
                public static void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer,scoped ref _M value)
                    where TBufferWriter:IBufferWriter<byte>{
                    writer.WriteString(value.n_name);
                }
                public static void Deserialize(ref MemoryPackReader reader,scoped ref _M value){
                    value.n_name=reader.ReadString();
                }
            }
            private _M _m;
            public partial int n_nationkey=>this.Key.n_nationkey;
            public partial string n_name=>this._m.n_name;
            //static partial void StaticConstructor();
            void mm(){
            }
            static nation(){
                global::MemoryPack.MemoryPackFormatterProvider.Register<nation>();
            //    StaticConstructor();
            }
            static void IMemoryPackFormatterRegister.RegisterFormatter(){
                if(!global::MemoryPack.MemoryPackFormatterProvider.IsRegistered<nation>()){
                    global::MemoryPack.MemoryPackFormatterProvider.Register(new global::MemoryPack.Formatters.MemoryPackableFormatter<nation>());
                }
                if(!global::MemoryPack.MemoryPackFormatterProvider.IsRegistered<nation[]>()){
                    global::MemoryPack.MemoryPackFormatterProvider.Register(new global::MemoryPack.Formatters.ArrayFormatter<nation>());
                }

            }

            static void IMemoryPackable<nation>.Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer,scoped ref nation value){
                writer.WriteUnmanaged(value._u);
                writer.WritePackable(value._m);
            }
            static void IMemoryPackable<nation>.Deserialize(ref MemoryPackReader reader,scoped ref nation value){

                reader.ReadUnmanaged(out value._u);
                reader.ReadPackable(ref value._m);
            }
        }
        partial struct region:IMemoryPackable<region>{
            public region(Keys.region Key){
                this.Key=Key;
            }
            public region(int r_regionkey):this(new Keys.region(r_regionkey)){
                this._u=new();
                this._m=new();
            }
            private readonly Keys.region Key;
            private readonly record struct _U(int r_regionkey);
            private _U _u;
            private struct _M:IMemoryPackable<_M>{
                public string r_name;
                public string r_comment;
                public static void RegisterFormatter(){
                    throw new NotImplementedException();
                }
                public static void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer,scoped ref _M value)
                    where TBufferWriter:IBufferWriter<byte>{
                    writer.WriteString(value.r_name);
                    writer.WriteString(value.r_comment);
                }
                public static void Deserialize(ref MemoryPackReader reader,scoped ref _M value){
                    value.r_name=reader.ReadString();
                    value.r_comment=reader.ReadString();
                }
            }
            private _M _m;
            public partial int r_regionkey=>this.Key.r_regionkey;
            public partial string r_name=>this._m.r_name;
            public partial string r_comment=>this._m.r_comment;
            static region(){
                global::MemoryPack.MemoryPackFormatterProvider.Register<region>();
            }
            static void IMemoryPackFormatterRegister.RegisterFormatter(){
                if(!global::MemoryPack.MemoryPackFormatterProvider.IsRegistered<region>()){
                    global::MemoryPack.MemoryPackFormatterProvider.Register(new global::MemoryPack.Formatters.MemoryPackableFormatter<region>());
                }
                if(!global::MemoryPack.MemoryPackFormatterProvider.IsRegistered<region[]>()){
                    global::MemoryPack.MemoryPackFormatterProvider.Register(new global::MemoryPack.Formatters.ArrayFormatter<region>());
                }

            }
            public static void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer,scoped ref region value)
                where TBufferWriter:IBufferWriter<byte>{
                writer.WriteUnmanaged(value._u);
                writer.WritePackable(value._m);
            }
            public static void Deserialize(ref MemoryPackReader reader,scoped ref region value){
                reader.ReadUnmanaged(out value._u);
                reader.ReadPackable(ref value._m);
            }
        }
    }
}
