namespace Common;
public static class Const{
    public const int Chunk_Length=2;
    public const int Count=253;
    public const int IndexSizeBits=6;
    public const int PageSizeBits=12;
    public const int MemorySizeBits=PageSizeBits+1;
    public const long IndexSize=1L<<IndexSizeBits;
    public const long LogMemorySize=1L<<MemorySizeBits;
    public const long PageSize=1L<<PageSizeBits;
    public const long SegmentSize=1L<<22; // 4MB: PageSize*1(4096)はページごとに別ファイルになり非効率
    public const long MinLogCapacity=SegmentSize*2;
    public const long MinObjectLogCapacity=1L<<22;
    public const long MaxObjectLogCapacity=1L<<62;
}
