using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Seamless.Execution;
namespace Seamless.Transactions;

/// <summary>論理DBの表/世代を束ねる。物理保存先の寿命は呼出し側が所有する。</summary>
public sealed class VersionedTransactionParticipantGroup:IVersionedTransactionParticipantGroup,IDisposable{
    private readonly object gate=new();
    private readonly ResourceBudget budget;
    private readonly Dictionary<(int Table,string Codec),Member> members=[];
    private readonly HashSet<string> prefixes=new(StringComparer.Ordinal);
    private IReclaimableTransactionParticipant[]? closed;
    private bool disposed,failed,prepared,registering;
    public VersionedTransactionParticipantGroup(int partition,int tableId,string codecIdentity,ResourceBudget budget){
        ArgumentNullException.ThrowIfNull(budget);ArgumentException.ThrowIfNullOrWhiteSpace(codecIdentity);this.Partition=partition;this.TableId=tableId;this.CodecIdentity=codecIdentity;this.budget=budget;
    }
    public int Partition{get;}
    public int TableId{get;}
    public string CodecIdentity{get;}
    public long RecoveredVersion{get{lock(this.gate){this.Check();return this.members.Values.Select(static member=>member.Participant.RecoveredVersion).DefaultIfEmpty().Max();}}}
    public void Register(IRoutedVersionedTransactionParticipant participant){ArgumentNullException.ThrowIfNull(participant);this.RegisterBatch([participant]);}
    /// <summary>完成した一世代の全表を一括登録する。失敗時に部分所属を残さない。</summary>
    public void RegisterBatch(IReadOnlyList<IRoutedVersionedTransactionParticipant> participants){
        ArgumentNullException.ThrowIfNull(participants);
        lock(this.gate){
            this.Check();if(this.closed is not null){throw new InvalidOperationException("Cannot register children during group maintenance.");}
            if(this.registering){throw new InvalidOperationException("Recursive generation registration is not supported.");}
            using var working=this.budget.ReserveMemory(checked(256L+participants.Count*256L));
            var pending=new List<((int Table,string Codec) Key,string Prefix,Member Member)>(participants.Count);var ids=new HashSet<(int,string)>();var prefixes=new HashSet<string>(StringComparer.Ordinal);var added=0;this.registering=true;
            try{
                foreach(var participant in participants){
                    if(participant is not IReclaimableTransactionParticipant reclaimable||participant.Routing is not{} route||!ReferenceEquals(route.Group,this)||participant.Partition!=this.Partition){throw new ArgumentException("Every child must route to this reclaimable database group.",nameof(participants));}
                    var key=(participant.TableId,participant.CodecIdentity);var prefix=Convert.ToHexString(route.Prefix.Span);
                    if(this.members.ContainsKey(key)||this.prefixes.Contains(prefix)||!ids.Add(key)||!prefixes.Add(prefix)){throw new InvalidOperationException("Duplicate table/codec or dependency namespace.");}
                    var memory=this.budget.ReserveMemory(checked(512L+route.Prefix.Length*6L+participant.CodecIdentity.Length*2L));
                    try{pending.Add((key,prefix,new(reclaimable,memory)));}catch{memory.Dispose();throw;}
                }
                this.members.EnsureCapacity(checked(this.members.Count+pending.Count));this.prefixes.EnsureCapacity(checked(this.prefixes.Count+pending.Count));
                if(this.prepared){foreach(var item in pending){item.Member.Participant.PrepareForTransactions();}}
                foreach(var item in pending){this.members.Add(item.Key,item.Member);added++;if(!this.prefixes.Add(item.Prefix)){throw new InvalidOperationException("A dependency namespace changed during registration.");}}
            }catch{
                for(var index=0;index<added;index++){this.members.Remove(pending[index].Key);this.prefixes.Remove(pending[index].Prefix);}
                foreach(var item in pending){item.Member.Memory.Dispose();}throw;
            }finally{this.registering=false;}
        }
    }
    public bool Owns(IVersionedTransactionParticipant participant){lock(this.gate){return !this.disposed&&this.members.TryGetValue((participant.TableId,participant.CodecIdentity),out var member)&&ReferenceEquals(member.Participant,participant);}}
    public void PrepareForTransactions(){lock(this.gate){this.Check();foreach(var member in this.members.Values){member.Participant.PrepareForTransactions();}this.prepared=true;}}
    public bool TryResolve(WalEntry entry,out IVersionedTransactionParticipant participant){
        lock(this.gate){this.Check();if(entry.Partition==this.Partition&&this.members.TryGetValue((entry.TableId,entry.Codec),out var member)){participant=member.Participant;return true;}participant=null!;return false;}
    }
    // coordinator が全 root を排出し、共通保存と再取得に成功した後だけ呼ぶ。
    internal void Retire(IRoutedVersionedTransactionParticipant participant){
        lock(this.gate){
            this.Check();if(this.closed is not null){throw new InvalidOperationException("Finish physical maintenance before retiring children.");}
            if(!this.members.TryGetValue((participant.TableId,participant.CodecIdentity),out var member)||!ReferenceEquals(member.Participant,participant)){return;}
            this.members.Remove((participant.TableId,participant.CodecIdentity));this.prefixes.Remove(Convert.ToHexString(participant.Routing!.Prefix.Span));member.Memory.Dispose();
        }
    }
    public ITransactionChanges CreateChanges()=>throw new NotSupportedException("Use a typed child table of the group.");
    public IVersionedTransactionChanges CreateChanges(TransactionContext context)=>throw new NotSupportedException("Use a typed child table of the group.");
    public void Replay(WalEntry entry)=>throw new NotSupportedException("Group replay requires an installation context.");
    public void Replay(WalEntry entry,TransactionInstallationContext context){if(!this.TryResolve(entry,out var child)){throw new InvalidDataException("WAL generation/table codec is not registered.");}child.Replay(entry,context);}
    public async ValueTask ReclaimAsync(){
        IReclaimableTransactionParticipant[] children;
        lock(this.gate){this.Check();if(this.closed is not null){throw new InvalidOperationException("Group maintenance is already active.");}this.closed=children=this.members.Values.Select(static member=>member.Participant).ToArray();}
        try{foreach(var child in children){await child.ReclaimAsync().ConfigureAwait(false);}}
        catch{lock(this.gate){this.failed=true;}throw;}
    }
    public void ReopenAfterMaintenance(){
        IReclaimableTransactionParticipant[] children;lock(this.gate){this.Check();children=this.closed??throw new InvalidOperationException("Group maintenance is not active.");}
        try{foreach(var child in children){child.ReopenAfterMaintenance();}lock(this.gate){this.closed=null;}}
        catch{lock(this.gate){this.failed=true;}throw;}
    }
    public void FaultMaintenance(Exception error){ArgumentNullException.ThrowIfNull(error);lock(this.gate){this.failed=true;}}
    private void Check(){ObjectDisposedException.ThrowIf(this.disposed,this);if(this.failed){throw new InvalidOperationException("The group failed during physical maintenance; reopen the database.");}}
    public void Dispose(){lock(this.gate){if(this.disposed){return;}if(this.closed is not null&&!this.failed){throw new InvalidOperationException("Finish group maintenance before disposal.");}this.disposed=true;foreach(var member in this.members.Values){member.Memory.Dispose();}this.members.Clear();this.prefixes.Clear();this.closed=null;}}
    private sealed record Member(IReclaimableTransactionParticipant Participant,ResourceReservation Memory);
}
