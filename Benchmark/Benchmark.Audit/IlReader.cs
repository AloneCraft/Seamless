using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;

namespace Benchmark.Audit;

/// <summary>IL命令1つ。Token はメタデータトークンを持つ命令だけ有効。</summary>
internal readonly record struct IlInstruction(int Offset,short Code,int Token,int[] BranchTargets,bool Constrained);

/// <summary>ループ(後方分岐の区間)。</summary>
internal readonly record struct IlLoop(int Start,int End);

/// <summary>メソッド本体のILを命令列とループ区間へ分解する。</summary>
internal static class IlReader {
    private static readonly Dictionary<short,OpCode> opCodes = typeof(OpCodes)
        .GetFields(BindingFlags.Public|BindingFlags.Static)
        .Select(static field => (OpCode)field.GetValue(null)!)
        .ToDictionary(static code => code.Value);

    public static short Call => OpCodes.Call.Value;
    public static short Callvirt => OpCodes.Callvirt.Value;
    public static short Newobj => OpCodes.Newobj.Value;
    public static short Newarr => OpCodes.Newarr.Value;
    public static short Box => OpCodes.Box.Value;
    public static short Ldftn => OpCodes.Ldftn.Value;
    public static short Ldvirtftn => OpCodes.Ldvirtftn.Value;
    public static short Throw => OpCodes.Throw.Value;

    /// <summary>box の直後にこれらが来る形は JIT がボックス化を消す(ジェネリックの型判定や null 比較など)。</summary>
    public static bool IsBoxElidedBy(short next) =>
        next==OpCodes.Isinst.Value
        ||next==OpCodes.Ldnull.Value
        ||next==OpCodes.Unbox_Any.Value
        ||next==OpCodes.Brtrue.Value
        ||next==OpCodes.Brtrue_S.Value
        ||next==OpCodes.Brfalse.Value
        ||next==OpCodes.Brfalse_S.Value;

    public static IReadOnlyList<IlInstruction> Decode(MethodBodyBlock body) {
        var reader = body.GetILReader();
        var instructions = new List<IlInstruction>();
        var constrained = false;
        while(reader.RemainingBytes>0) {
            var offset = reader.Offset;
            short value = reader.ReadByte();
            if(value==0xFE) {
                value=(short)(0xFE00|reader.ReadByte());
            }
            if(!opCodes.TryGetValue(value,out var code)) {
                throw new InvalidDataException($"未知のIL命令 0x{value:X4} (オフセット {offset})");
            }
            var token = 0;
            int[] targets = [];
            switch(code.OperandType) {
                case OperandType.InlineNone:
                    break;
                case OperandType.ShortInlineBrTarget: {
                    var delta = reader.ReadSByte();
                    targets=[reader.Offset+delta];
                    break;
                }
                case OperandType.InlineBrTarget: {
                    var delta = reader.ReadInt32();
                    targets=[reader.Offset+delta];
                    break;
                }
                case OperandType.InlineSwitch: {
                    var count = reader.ReadInt32();
                    var deltas = new int[count];
                    for(var index = 0;index<count;index++) {
                        deltas[index]=reader.ReadInt32();
                    }
                    var origin = reader.Offset;
                    targets=deltas.Select(delta => origin+delta).ToArray();
                    break;
                }
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    reader.ReadByte();
                    break;
                case OperandType.InlineVar:
                    reader.ReadInt16();
                    break;
                case OperandType.InlineI:
                case OperandType.ShortInlineR:
                    reader.ReadInt32();
                    break;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    reader.ReadInt64();
                    break;
                case OperandType.InlineString:
                    reader.ReadInt32();
                    break;
                case OperandType.InlineField:
                case OperandType.InlineMethod:
                case OperandType.InlineSig:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                    token=reader.ReadInt32();
                    break;
                default:
                    throw new InvalidDataException($"未対応のオペランド種別 {code.OperandType}");
            }
            instructions.Add(new IlInstruction(offset,value,token,targets,constrained));
            constrained=value==OpCodes.Constrained.Value;
        }
        return instructions;
    }

    /// <summary>
    /// 本体の命令列の指紋。メタデータトークンはビルドごとに番号が変わるので、参照先の名前(tokenName)で置き換える。
    /// 即値・分岐先・局所変数番号はそのまま含める。
    /// </summary>
    public static string Fingerprint(MethodBodyBlock body,Func<int,string> tokenName) {
        var reader = body.GetILReader();
        var builder = new System.Text.StringBuilder();
        builder.Append("max=").Append(body.MaxStack).Append(';');
        while(reader.RemainingBytes>0) {
            short value = reader.ReadByte();
            if(value==0xFE) {
                value=(short)(0xFE00|reader.ReadByte());
            }
            var code = opCodes[value];
            builder.Append(code.Name);
            switch(code.OperandType) {
                case OperandType.InlineNone:
                    break;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    builder.Append(' ').Append(reader.ReadSByte());
                    break;
                case OperandType.InlineVar:
                    builder.Append(' ').Append(reader.ReadInt16());
                    break;
                case OperandType.InlineBrTarget:
                case OperandType.InlineI:
                case OperandType.ShortInlineR:
                    builder.Append(' ').Append(reader.ReadInt32());
                    break;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    builder.Append(' ').Append(reader.ReadInt64());
                    break;
                case OperandType.InlineSwitch: {
                    var count = reader.ReadInt32();
                    for(var index = 0;index<count;index++) {
                        builder.Append(' ').Append(reader.ReadInt32());
                    }
                    break;
                }
                case OperandType.InlineString:
                case OperandType.InlineField:
                case OperandType.InlineMethod:
                case OperandType.InlineSig:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                    builder.Append(' ').Append(tokenName(reader.ReadInt32()));
                    break;
                default:
                    throw new InvalidDataException($"未対応のオペランド種別 {code.OperandType}");
            }
            builder.Append(';');
        }
        return builder.ToString();
    }

    /// <summary>後方分岐(分岐先が自分以前)をループ区間とみなす。</summary>
    public static IReadOnlyList<IlLoop> FindLoops(IReadOnlyList<IlInstruction> instructions) {
        var loops = new List<IlLoop>();
        foreach(var instruction in instructions) {
            foreach(var target in instruction.BranchTargets) {
                if(target<=instruction.Offset) {
                    loops.Add(new IlLoop(target,instruction.Offset));
                }
            }
        }
        return loops;
    }

    public static int LoopDepth(IReadOnlyList<IlLoop> loops,int offset) {
        var depth = 0;
        foreach(var loop in loops) {
            if(loop.Start<=offset&&offset<=loop.End) {
                depth++;
            }
        }
        return depth;
    }
}
