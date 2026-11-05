using Seamless.Collections;
using System.Runtime.InteropServices;

namespace TransactionModes;

public enum Mode{Single,Deferred,Immediate}
public enum Phase{BeforeWal,WalDurable,FirstTableApplied,BeforePublish}
public readonly record struct Key(int Table,int Id);
[StructLayout(LayoutKind.Sequential)]
public struct Row:IRecord<int>{
    public int Id;
    public int Group;
    public long Value;
    public long Owner;
    public long Previous;
    public long Deleted;
    public readonly int Key=>this.Id;
}
[StructLayout(LayoutKind.Sequential)]
public struct PlainRow:IRecord<int>{
    public int Id;
    public int Group;
    public long Value;
    public readonly int Key=>this.Id;
}
public readonly record struct ReadResult(Row Row,long Address,long Previous,bool Captured);
public sealed class ConflictException:Exception;
public sealed class HistoryBudgetException:Exception;
public sealed class CommitFailureException(bool committed,Exception inner):IOException("Commit failed",inner){public bool Committed{get;}=committed;}
public sealed class State{
    public long Csn;
    public readonly Guid Id=Guid.NewGuid();
}
