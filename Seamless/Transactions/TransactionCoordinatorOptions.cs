using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Seamless.Execution;
namespace Seamless.Transactions;

public enum TransactionConcurrencyMode{Exclusive,Optimistic}

public sealed record TransactionCoordinatorOptions{
    public TransactionConcurrencyMode Concurrency{get;init;}=TransactionConcurrencyMode.Exclusive;
    public TransactionHistoryOptions? History{get;init;}
    public ResourceBudget? ParentBudget{get;init;}
    public int MaximumPendingCommits{get;init;}=256;
    internal void Validate(){
        if(!Enum.IsDefined(this.Concurrency)){throw new ArgumentOutOfRangeException(nameof(this.Concurrency));}
        if(this.MaximumPendingCommits<=0){throw new ArgumentOutOfRangeException(nameof(this.MaximumPendingCommits));}
        if(this.Concurrency==TransactionConcurrencyMode.Optimistic&&this.History is null){throw new ArgumentException("Optimistic transactions require finite history and metadata budgets.");}
        this.History?.Validate();
    }
}

public sealed record TransactionHistoryOptions{
    public required long MaxRetainedMemoryBytes{get;init;}
    public required long MaxRetainedStorageBytes{get;init;}
    public required long MaxMetadataBytes{get;init;}
    public required long MaintenanceReserveMemoryBytes{get;init;}
    public required long MaintenanceReserveStorageBytes{get;init;}
    public required TimeSpan MaintenanceWaitTimeout{get;init;}
    internal void Validate(){
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(this.MaxRetainedMemoryBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(this.MaxRetainedStorageBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(this.MaxMetadataBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(this.MaintenanceReserveMemoryBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(this.MaintenanceReserveStorageBytes);
        if(this.MaintenanceWaitTimeout<=TimeSpan.Zero||this.MaintenanceWaitTimeout==Timeout.InfiniteTimeSpan){throw new ArgumentOutOfRangeException(nameof(this.MaintenanceWaitTimeout));}
    }
}
