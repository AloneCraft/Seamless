namespace Seamless.Collections.Tsavorite;
/// <summary>
/// Tsavorite の索引、ログ、ストレージの詳細設定。
/// 省略した大きさは共通ストアの上限と予想件数から自動で決める。指定する大きさは 2 の冪であること。
/// </summary>
public sealed record TsavoriteTuning{
    public static TsavoriteTuning Default{get;}=new();
    /// <summary>索引の大きさ(バイト)。64 バイト以上。1 バケット 64 バイト。省略時は予想件数から決める。</summary>
    public long? IndexSize{get;init;}
    /// <summary>ログのページの大きさ(バイト)。4096 バイト以上、1GB 以下。</summary>
    private long? pageSize;
    public long PageSize{get=>this.pageSize??4096;init=>this.pageSize=value;}
    /// <summary>ログファイルのセグメントの大きさ(バイト)。ページ以上であること。</summary>
    private long? segmentSize;
    public long SegmentSize{get=>this.segmentSize??4096;init=>this.segmentSize=value;}
    /// <summary>ログのメモリ上の大きさ(バイト)。省略時はメモリ種ならストアのメモリ上限(2 の冪へ切り下げ)、それ以外は上限の半分(最大 64MB)。</summary>
    public long? LogMemorySize{get;init;}
    /// <summary>メモリ上のログのうち変更可能な領域の割合。0 より大きく 1 以下。</summary>
    private double? mutableFraction;
    public double MutableFraction{get=>this.mutableFraction??1;init=>this.mutableFraction=value;}
    /// <summary>索引が混んだときに自動で拡張する。false の場合は指定した大きさを維持する。</summary>
    public global::Seamless.Execution.Growth.Policy? Growth{get;init;}
    internal global::Seamless.Execution.ProfileSettings Overrides=>new(){IndexBytes=this.IndexSize,PageBytes=this.pageSize,SegmentBytes=this.segmentSize,LogMemoryBytes=this.LogMemorySize,MutableFraction=this.mutableFraction};
    /// <summary>ログファイルの読み書き方式。AccessPattern と BufferingMode をデバイスの選択に使う。他の項目は使わない。省略時は既定のデバイスを使う。メモリストアには指定できない。</summary>
    public StorageTuning? Storage{get;init;}

    internal void Validate(global::Seamless.Execution.StoreKind kind){
        if(this.IndexSize is long indexSize){
            ValidatePowerOfTwo(indexSize,nameof(this.IndexSize));
            if(indexSize<64){
                throw new ArgumentException("IndexSize must be at least 64 bytes.",nameof(this.IndexSize));
            }
        }
        ValidatePowerOfTwo(this.PageSize,nameof(this.PageSize));
        ValidatePowerOfTwo(this.SegmentSize,nameof(this.SegmentSize));
        if(this.LogMemorySize is long logMemorySize){
            ValidatePowerOfTwo(logMemorySize,nameof(this.LogMemorySize));
        }
        if(this.PageSize<1L<<12||this.PageSize>1L<<30){
            throw new ArgumentException("PageSize must be between 4096 bytes and 1GB.",nameof(this.PageSize));
        }
        if(this.SegmentSize<this.PageSize){
            throw new ArgumentException("SegmentSize must be at least PageSize.",nameof(this.SegmentSize));
        }
        if(double.IsNaN(this.MutableFraction)||this.MutableFraction<=0||this.MutableFraction>1){
            throw new ArgumentException("MutableFraction must be greater than zero and at most one.",nameof(this.MutableFraction));
        }
        if(kind==global::Seamless.Execution.StoreKind.Memory&&this.Storage is not null){
            throw new ArgumentException("Storage cannot be specified for a memory store.",nameof(this.Storage));
        }
    }

    private static void ValidatePowerOfTwo(long value,string paramName){
        if(value<=0||(value&(value-1))!=0){
            throw new ArgumentException("The size must be a positive power of two.",paramName);
        }
    }
}
