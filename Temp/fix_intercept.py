#!/usr/bin/env python3
"""
Fix Intercept benchmarks:
- Simple files (68): move LINQ call from Intercept() body to InterceptCore() helper
- Chained files (6): move LINQ calls from Intercept1-4() bodies to Intercept1-4Core() helpers
"""
import re, os

BENCH_DIR = '/Users/nomurakazuhiro/Documents/GitHub/Seamless/sandbox/Benchmark/Benchmarks'

SIMPLE_FILES = [
    'Aggregate_func.cs','Aggregate_seed_func.cs','Aggregate_seed_func_resultSelector.cs',
    'AggregateBy_keySelector.cs','All_predicate.cs','Any.cs','Any_predicate.cs',
    'Average.cs','Average_selector.cs','Cast.cs','Chunk_size.cs','Concat_second.cs',
    'Contains_value.cs','Count.cs','Count_predicate.cs','CountBy_keySelector.cs',
    'ElementAt_index.cs','ElementAtOrDefault_index.cs','Except_second.cs',
    'ExceptBy_keySelector.cs','First.cs','FirstOrDefault.cs','FirstOrDefault_predicate.cs',
    'GroupBy_elementSelector.cs','GroupBy_elementSelector_resultSelector.cs',
    'GroupBy_keySelector.cs','GroupBy_resultSelector.cs','GroupJoin_inner.cs','Index.cs',
    'Intersect_second.cs','IntersectBy_keySelector.cs','Join_inner.cs','Last.cs',
    'LastOrDefault.cs','LastOrDefault_predicate.cs','LongCount.cs','LongCount_predicate.cs',
    'Max.cs','Max_selector.cs','MaxBy_keySelector.cs','Min.cs','Min_selector.cs',
    'MinBy_keySelector.cs','OfType.cs','SelectMany_indexSelector.cs',
    'SelectMany_indexSelector_resultSelector.cs','SelectMany_resultSelector.cs',
    'SelectMany_selector.cs','SequenceEqual_second.cs','Single.cs','Single_predicate.cs',
    'SingleOrDefault.cs','SingleOrDefault_predicate.cs','Sum.cs','Sum_selector.cs',
    'ThenBy_keySelector.cs','ThenByDescending_keySelector.cs',
    # ToArray/ToList/ToHashSet/ToDictionary/ToLookup: generator bug (wrong out type), skip
    'Union_second.cs','UnionBy_keySelector.cs','Zip_second.cs','Zip_tuple.cs',
]

CHAINED_FILES = [
    'Skip_count.cs','Take_count.cs','SkipLast_count.cs','TakeLast_count.cs',
    'Where_indexPredicate.cs','Select_indexSelector.cs',
]

# Matches: [Benchmark]\n    public void Intercept(){\n        <body>\n    }
SIMPLE_PAT = re.compile(
    r'    \[Benchmark\]\n    public void Intercept\(\)\{\n        (.+)\n    \}'
)

def add_this(body):
    """Add this. prefix to bare field accesses: src, src2, srcNested, srcOne, consumer."""
    import re as _re
    body = _re.sub(r'(?<!\.)(?<!\w)\b(src2|srcNested|srcOne|src|consumer)\b', r'this.\1', body)
    return body

def transform_simple(content):
    m = SIMPLE_PAT.search(content)
    if not m:
        return content, False
    body = add_this(m.group(1))
    repl = f'    void InterceptCore(){{ {body} }}\n\n    [Benchmark]\n    public void Intercept(){{ InterceptCore(); }}'
    return content[:m.start()] + repl + content[m.end():], True

# Matches each InterceptN block (with or without Baseline=true)
CHAINED_PAT = re.compile(
    r'    (\[Benchmark(?:\(Baseline=true\))?\])\n'
    r'    \[BenchmarkCategory\(Categories\.Intercept\)\]\n'
    r'    public void Intercept(\d)\(\)\{\n'
    r'        (.+)\n'
    r'    \}'
)

def transform_chained(content):
    matches = list(CHAINED_PAT.finditer(content))
    if len(matches) != 4:
        print(f'  [WARN] Expected 4 intercept methods, found {len(matches)}')
        return content, False

    cores = []
    benches = []
    for m in matches:
        bench_attr = m.group(1)
        n = m.group(2)
        body = m.group(3)
        cores.append(f'    void Intercept{n}Core(){{ {add_this(body)} }}')
        benches.append(
            f'    {bench_attr}\n'
            f'    [BenchmarkCategory(Categories.Intercept)]\n'
            f'    public void Intercept{n}(){{ Intercept{n}Core(); }}'
        )

    replacement = '\n'.join(cores) + '\n\n' + '\n\n'.join(benches)
    start = matches[0].start()
    end   = matches[-1].end()
    return content[:start] + replacement + content[end:], True

def process(files, transform_fn, label):
    ok = 0
    skip = 0
    for fname in files:
        fpath = os.path.join(BENCH_DIR, fname)
        with open(fpath, 'r') as f:
            content = f.read()
        new_content, changed = transform_fn(content)
        if changed:
            with open(fpath, 'w') as f:
                f.write(new_content)
            print(f'  [OK]   {fname}')
            ok += 1
        else:
            print(f'  [SKIP] {fname}')
            skip += 1
    print(f'{label}: {ok} updated, {skip} skipped')

print('=== Simple files ===')
process(SIMPLE_FILES, transform_simple, 'Simple')
print()
print('=== Chained files ===')
process(CHAINED_FILES, transform_chained, 'Chained')
