"""JSON Lines の全セルと反復数を検査し、比較用の中央値を生成する。"""
import collections
import json
import statistics
import sys


def main():
    with open(sys.argv[1], encoding="utf-8-sig") as source:
        rows = [json.loads(line) for line in source if line.strip()]
    counts = collections.Counter(row["kind"] for row in rows)
    expected = {"mixed": 1440, "scan": 120, "history": 20, "aborted": 2}
    if dict(counts) != expected:
        raise ValueError(f"Incomplete matrix: {dict(counts)}, expected {expected}")
    if len({row["name"] for row in rows}) != len(rows):
        raise ValueError("Duplicate cell identifiers")
    if any(row["outcome"] != "Passed" for row in rows):
        raise ValueError("Failed cells must not be used for an adoption decision")
    mixed = collections.defaultdict(list)
    scans = collections.defaultdict(list)
    histories = collections.defaultdict(list)
    aborted = []
    for row in rows:
        if row["kind"] == "mixed":
            if row["sample"]["Errors"]:
                raise ValueError("A mixed cell recorded errors")
            key = tuple(row[field] for field in ("disk", "size", "concurrency", "readPercent", "hot", "mode"))
            mixed[key].append(row["sample"])
        elif row["kind"] == "scan":
            scans[(row["disk"], row["size"], row["mode"], row["flinq"])].append(row["elapsedMs"] / 8)
        elif row["kind"] == "history":
            histories[(row["disk"], row["mode"])].append(row)
        else:
            aborted.append(row)
    summary = {"counts": dict(counts), "mixed": [], "scans": [], "history": [], "aborted": aborted}
    fields = ("disk", "size", "concurrency", "readPercent", "hot", "mode")
    for key, samples in sorted(mixed.items()):
        if len(samples) != 5:
            raise ValueError(f"Incomplete repetitions: {key}")
        result = dict(zip(fields, key))
        for metric in ("SuccessPerSecond", "P50Ms", "P95Ms", "P99Ms", "Allocated", "PeakWorkingSet", "NativeLogBytes", "WalBytes"):
            result[metric] = statistics.median(sample[metric] for sample in samples)
        result["conflictPercent"] = statistics.median(100 * sample["Conflicts"] / max(1, sample["Success"] + sample["Conflicts"]) for sample in samples)
        result["AllocatedPerSuccess"] = statistics.median(sample["Allocated"] / max(1, sample["Success"]) for sample in samples)
        result["minThroughput"] = min(sample["SuccessPerSecond"] for sample in samples)
        result["maxThroughput"] = max(sample["SuccessPerSecond"] for sample in samples)
        summary["mixed"].append(result)
    for disk, size, mode, flinq in sorted(scans):
        if not flinq:
            continue
        native = scans[(disk, size, mode, True)]
        ordinary = scans[(disk, size, mode, False)]
        if len(native) != 5 or len(ordinary) != 5:
            raise ValueError("Incomplete scan repetitions")
        native_median = statistics.median(native)
        summary["scans"].append({"disk": disk, "size": size, "mode": mode, "flinqMs": native_median, "linqMs": statistics.median(ordinary), "speedup": statistics.median(ordinary) / native_median})
    for (disk, mode), samples in sorted(histories.items()):
        if len(samples) != 5:
            raise ValueError("Incomplete history repetitions")
        summary["history"].append({"disk": disk, "mode": mode, "elapsedMs": statistics.median(row["elapsedMs"] for row in samples), "nativeBytes": max(row["nativeBytes"] for row in samples)})
    summary["maxWorkingSetBytes"] = max(row["sample"]["PeakWorkingSet"] for row in rows if row["kind"] == "mixed")
    with open(sys.argv[2], "w", encoding="utf-8") as target:
        json.dump(summary, target, ensure_ascii=False, indent=2)
    print(json.dumps({"counts": summary["counts"], "maxWorkingSetMiB": summary["maxWorkingSetBytes"] / 2**20}))


if __name__ == "__main__":
    main()
