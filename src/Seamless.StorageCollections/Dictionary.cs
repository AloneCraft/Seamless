using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

using Tsavorite.core;
namespace Seamless.StorageCollections;

public class Dictionary<TKey,TValue>:IDictionary<TKey,TValue>{
    private readonly KVSettings KVSettings;
    private protected const long KB=1000L;
    private protected const long MB=KB*1000;
    private protected const long GB=KB*1000;
    private protected const long KiB=1024L;
    private protected const long MiB=KiB*1024;
    private protected const long GiB=KiB*1024;
    private protected const long 万=10000;
    public struct StoreFunctions : IStoreFunctions{
        // キーの同一性判定：ここが遅いと衝突時に性能がガタ落ちします
        public bool KeysEqual(ReadOnlySpan<byte> k1, ReadOnlySpan<byte> k2) 
        {
            // CPUのSIMD命令を利用して高速比較
            return k1.SequenceEqual(k2);
        }

        public long GetKeyHashCode64(ReadOnlySpan<byte> key) 
        {
            return (long)System.IO.Hashing.XxHash64.HashToUInt64(key);
        }

        // シリアライザの設定：32TB運用では通常、シリアライザに頼らず
        // 直接バイト列を扱う方が速いため、基本は false にします
        public bool HasValueSerializer => false;

        // メモリからページが追い出される際のメモリ解放設定
        // 参照型を含まない構造体（Blittable）なら false でOK
        public bool DisposeOnPageEviction => false;

        // --- 以下、未実装でも動作するメソッド群 ---

        public IObjectSerializer<IHeapObject> CreateValueObjectSerializer() => null;
        public IObjectSerializer<IHeapObject> BeginSerializeValue(Stream stream) => null;
        public IObjectSerializer<IHeapObject> BeginDeserializeValue(Stream stream) => null;
    
        public void DisposeValueObject(IHeapObject valueObject, DisposeReason reason) { }

        // チェックポイント完了通知：進捗ログを出したい場合に実装
        private Action _checkpointCallback;
        public void SetCheckpointCompletedCallback(Action callback) =>
            this._checkpointCallback = callback;
        public void OnCheckpointCompleted() =>
            this._checkpointCallback?.Invoke();
    }    
    public Dictionary(){
        var baseDir=Environment.CurrentDirectory;
        this.KVSettings=new Tsavorite.core.KVSettings(baseDir);
        var TsavoriteKVSettings=this.CreateSize();
        var StoreFunctions=new StoreFunctions();
        //using var TsavoriteKV=new TsavoriteKV<StoreFunctions,ObjectAllocator<StoreFunctions>>(TsavoriteKVSettings,this._StoreFunctions1,this.AllocatorFactory);
        using var TsavoriteKV0 = new TsavoriteKV<StoreFunctions,SpanByteAllocator<StoreFunctions>>(
            TsavoriteKVSettings,
            StoreFunctions,
            (settings, functions) => new SpanByteAllocator<StoreFunctions>(settings, functions)
        );
    }
    private protected static int RoundUpShift(long count){
        if (count <= 1) return 0;
        // count が 2のべき乗（例: 1024）なら 10 を返す
        // count が 2のべき乗でない（例: 1025）なら、切り上げた 11 を返す
        var Shift=BitOperations.Log2((ulong)count - 1) + 1;
        return Shift;
    }    
    private protected KVSettings CreateSize(bool DeleteFolder=false,long IndexSize=64,long PageSize=4*KiB,long MemorySize=8*KiB,long SegmentSize=16*KiB){
        var IndexSizeBits  =RoundUpShift(IndexSize);
        var PageSizeBits   =RoundUpShift(PageSize);
        var MemorySizeBits =RoundUpShift(MemorySize);
        var SegmentSizeBits=RoundUpShift(SegmentSize);
        return this.CreateBits(DeleteFolder,IndexSizeBits,PageSizeBits,MemorySizeBits,SegmentSizeBits);

    }
    private protected KVSettings CreateBits(bool DeleteFolder,int IndexSizeBits,int PageSizeBits,int MemorySizeBits,int SegmentSizeBits){
        var FolderName=this.GetType().Name;
        var MutableFraction=0.1;
        var BaseDirectory = Path.Combine(Environment.CurrentDirectory,FolderName+'.'+typeof(TValue).Name);
        if(DeleteFolder){
            if(Directory.Exists(BaseDirectory)){
                Directory.Delete(BaseDirectory,true);
            }
        }
        Directory.CreateDirectory(BaseDirectory);
        var LogPath      =Path.Combine(BaseDirectory,"log");
        var ObjLogPath   =Path.Combine(BaseDirectory,"objlog");
        var CheckpointDir=Path.Combine(BaseDirectory,"checkpoint");
        var LogDevice      =Devices.CreateLogDevice(LogPath, deleteOnClose: false);
        var ObjectLogDevice=Devices.CreateLogDevice(ObjLogPath, deleteOnClose: false);
        //Debug.Assert(IndexSizeBits <PageSizeBits);
        //Debug.Assert(PageSizeBits  <MemorySizeBits);
        Debug.Assert(MemorySizeBits<SegmentSizeBits);
        //Debug.Assert(IndexSizeBits +1<=PageSizeBits);
        //Debug.Assert(PageSizeBits  +1<=MemorySizeBits);
        Debug.Assert(MemorySizeBits+1<=SegmentSizeBits);
        var IndexSize      =1L<<IndexSizeBits;
        var PageSize       =1L<<PageSizeBits;
        var MemorySize     =1L<<MemorySizeBits;
        var SegmentSize    =1L<<SegmentSizeBits;
        //Debug.Assert(IndexSize%64==0);
        var KVSettings=new KVSettings{
            LogDevice        =LogDevice,
            ObjectLogDevice  =ObjectLogDevice,
            IndexSize        =IndexSize,
            PageSize         =PageSize,
            SegmentSize      =SegmentSize,
            //CheckpointDir    =CheckpointDir,
            MutableFraction  =MutableFraction,
            PreallocateLog   =false,
            MemorySize       =MemorySize,
            Epoch            =new LightEpoch(),
            CheckpointManager=new DeviceLogCommitCheckpointManager(
                new LocalStorageNamedDeviceFactoryCreator(),
                new DefaultCheckpointNamingScheme(CheckpointDir)),
        };
        return KVSettings;
    }
    public IEnumerator<KeyValuePair<TKey,TValue>> GetEnumerator(){
        throw new NotImplementedException();
    }
    IEnumerator IEnumerable.GetEnumerator(){
        return this.GetEnumerator();
    }
    public void Add(KeyValuePair<TKey,TValue> item){
        throw new NotImplementedException();
    }
    public void Clear(){
        throw new NotImplementedException();
    }
    public bool Contains(KeyValuePair<TKey,TValue> item){
        throw new NotImplementedException();
    }
    public void CopyTo(KeyValuePair<TKey,TValue>[] array,int arrayIndex){
        throw new NotImplementedException();
    }
    public bool Remove(KeyValuePair<TKey,TValue> item){
        throw new NotImplementedException();
    }
    public int Count{
        get;
    }
    public bool IsReadOnly{
        get;
    }
    public void Add(TKey key,TValue value){
        throw new NotImplementedException();
    }
    public bool ContainsKey(TKey key){
        throw new NotImplementedException();
    }
    public bool Remove(TKey key){
        throw new NotImplementedException();
    }
    public bool TryGetValue(TKey key,[MaybeNullWhen(false)]out TValue value){
        throw new NotImplementedException();
    }
    public TValue this[TKey key]{
        get=>
            throw new NotImplementedException();
        set=>
            throw new NotImplementedException();
    }
    public ICollection<TKey> Keys{
        get;
    }
    public ICollection<TValue> Values{
        get;
    }
}
[Flags]
enum  ggg{}
//public static class Factory{
//    public static Dictionary<TKey,TValue> CreateDictionary<TKey,TValue>(
//        StorageConfiguration config,
//        string name
//    ){
//        // 1. Tsavoriteのデバイス・ログ設定の構築
//        // 2. MemoryPackシリアライザのセットアップ
//        // 3. StorageDictionaryの実装クラスを生成して返す
//        return new TsavoriteDictionary<TKey,TValue>(config,name);
//    }
//}
