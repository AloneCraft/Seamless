---
name: seamless-tsavorite-collections
description: Seamless Tsavorite collection implementation rules. Use when changing Seamless.Collections.Tsavorite traversal, HashSet/Set/Dictionary enumeration, provider-native FLinq terminals, Iterate APIs, IterateLookup behavior, tombstone handling, or any code that might choose between IterateLookup and ScanCursor.
---

# Seamless Tsavorite Collections

## Core Rule

Use `Iterate` for provider logical traversal. For Tsavorite collections, `Iterate` must be implemented with `ReadSession.IterateLookup(...)`, not `ScanCursor`.

`ScanCursor` walks log records. Seamless Tsavorite collections use RMW tombstones and may have stale or duplicate log versions. A collection query must observe the logical current collection state, so provider-native LINQ and collection operations should follow the existing `IterateLookup` pattern used by `GetLongCount`, set operations, `ForEach`, and enumerators.

## Implementation Guidance

- Keep generated FLinq Tsavorite native terminals on `HashSet.Iterate(ref state)`.
- Do not reintroduce `ScanUnsafeDelegate`, `HashSetScanUnsafe`, or `Func<object?, bool>` native-scan escape hatches.
- For early termination from an iterator, return `CursorRecordResult.Skip | CursorRecordResult.EndBatch` after recording the terminal state.
- Skip tombstone records defensively inside custom iterator readers, even when using `IterateLookup`.
- If chunking is needed, study `Test/Seamless/IterateLookupChunkTests.cs` for `EndBatch` and `RetryLastRecord` cursor behavior before editing production traversal.

## Validation

When changing Tsavorite traversal or FLinq native terminals, compare provider-native results with normal enumeration after add/remove/re-add sequences. Deleted keys must not appear and re-added keys must not be counted twice.
