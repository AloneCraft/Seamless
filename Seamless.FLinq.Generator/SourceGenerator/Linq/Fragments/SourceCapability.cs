namespace Seamless.FLinq.Generator;

// 列挙ソースが備える能力。Pipeline はこのフラグを見て、段や終端をどこまで畳めるかを決める。
// 基底クラスではないので Bases には置かない（AI_SYNTAX_RULES の File Layout Rules）。
[Flags]
internal enum SourceCapability {
    None = 0,
    KnownCount = 1,
    DirectSpan = 1<<1,
    Indexable = 1<<2,
    ChunkStreaming = 1<<3,
    NativeIterate = 1<<4,
    Unordered = 1<<5,
}
