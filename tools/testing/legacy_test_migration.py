"""Auditable, hash-checked moves of the former Test project. No directory deletion."""
import argparse
import hashlib
import json
import re
import subprocess
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
MANIFEST=ROOT/'Document/refactoring-designs/20261104-test-migration.json'

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def initial_route(source):
    if source=='Test/SourceGenerator/GeneratorTestHelpers.cs':
        return 'generator-harness','Tests/Seamless.TestKit/Compilation/GeneratorTestHelpers.cs'
    if source.startswith('Test/Database.Generated/'):
        return 'fixtures',source.replace('Test/Database.Generated/','Tests/Seamless.TestKit/Databases/',1)
    if source=='Test/Seamless/Database/AdventureWorks2017/AdventureWorks2017GeneratedSchemaTests.cs':
        return 'schema-contract','Tests/Database.Compilation.Tests/Schema/AdventureWorks2017GeneratedSchemaTests.cs'
    relative=source.removeprefix('Test/')
    def route(group,project,tail=relative):
        return group,project+'/Migration/'+tail
    if source.startswith('Test/SourceGenerator/Database'):
        return route('database-generation','Tests/Database.Compilation.Tests')
    if source.startswith('Test/Seamless/Database/Spec/') or source in ('Test/Seamless/Database/DatabaseScalarQueryDisposeTests.cs','Test/Seamless/Database/DatabaseStoragePersistenceTests.cs'):
        return route('database-transactions','Tests/Seamless.Database.Transactions.Tests')
    if source.startswith(('Test/Seamless/Database/SqlServer/','Test/Seamless/Database/Tool/')):
        return route('sql-server','Tests/Database.Verification.Tests')
    if source.startswith('Test/Seamless/Database/Rdbms/'):
        return route('database-generation','Tests/Database.Compilation.Tests')
    if source.startswith(('Test/Seamless/Database/','Test/Seamless/AdventureWorks2008R2/')):
        return route('database-query','Tests/Database.Query.Tests')
    if source.startswith(('Test/SourceGenerator/','Test/System/Linq/Analyzer/')):
        return route('generator-contracts','Benchmark.Dynamic.Tests')
    if source.startswith('Test/System/Linq/BenchmarkValueComparison/'):
        return route('benchmark-core','Benchmark/Benchmark.Infrastructure.Tests')
    if source.startswith(('Test/System/Linq/','Test/FLinqBackends/')) or '/Linq/Enumerable/' in source:
        return 'linq-behavior','Benchmark/Benchmark.Dynamic.Core/RegressionSources/'+relative
    if source.startswith('Test/Seamless/System/') or any(name in source for name in ('LookupBuilderTests','IterateLookupChunkTests','SequenceEqualTests')):
        return route('linq-runtime','Benchmark.Dynamic.Tests')
    if source.startswith('Test/Seamless/Persisted/'):
        return route('persisted-store','Test.Persisted.Collections')
    if source.startswith('Test/Seamless/Stores/') and source.endswith(('/Compaction.cs','/StoreExtensions.cs','/ParallelRegion.cs')):
        return route('execution','Execution.Tests')
    if source.startswith('Test/Seamless/'):
        return route('tsavorite-store','Test.Tsavorite.Collections')
    if source.startswith('Test/BenchmarkWorker/') or source in ('Test/Benchmark/Pattern.cs','Test/Benchmark/BenchmarkFastWorkerSession.cs','Test/Benchmark/WorkerAssemblyResolution.cs'):
        return route('benchmark-windows','Benchmark.Dynamic.Tests')
    if source.startswith('Test/Benchmark/'):
        return route('benchmark-core','Benchmark/Benchmark.Infrastructure.Tests')
    return 'unclassified',None

def save(data):
    MANIFEST.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('action',choices=['init','route','audit','move','verify','baseline','retire'])
    parser.add_argument('--group')
    parser.add_argument('--receipt')
    parser.add_argument('--configuration',choices=['Debug','Release'])
    parser.add_argument('--log')
    args=parser.parse_args()
    if args.action=='init':
        if MANIFEST.exists():raise RuntimeError('Manifest already exists')
        sources=subprocess.check_output(['git','ls-files','--cached','--others','--exclude-standard','-z','Test'],cwd=ROOT).decode().split('\0')
        entries=[]
        for source in sorted(set(filter(None,sources))):
            path=ROOT/source
            if not path.is_file():raise RuntimeError('Missing source: '+source)
            text=path.read_text(encoding='utf-8-sig',errors='strict')
            group,destination=initial_route(source)
            entries.append(dict(source=source,destination=destination,group=group,state='planned',sha256=digest(path),testAttributes=len(re.findall(r'\[(?:[^\]\n]*\b)?(?:Fact|Theory)\b',text)),assertCalls=len(re.findall(r'\bAssert\.',text)),reason=''))
        save(dict(version=1,entries=entries))
    data=json.loads(MANIFEST.read_text(encoding='utf-8'))
    if args.action=='baseline':
        if not args.log:raise RuntimeError('--log is required')
        content=Path(args.log).read_text(encoding='utf-8-sig')
        names=[line.strip() for line in content.splitlines() if line.startswith('    Test.')]
        if not names:raise RuntimeError('No baseline tests were found')
        data['baselineTests']=names
        data['baselineLog']=str(Path(args.log).resolve())
        save(data)
    if args.action=='verify':
        if not all([args.group,args.receipt,args.configuration]):raise RuntimeError('group, receipt, configuration required')
        receipt=Path(args.receipt)
        outcome=json.loads(receipt.read_text(encoding='utf-8-sig'))
        if outcome['Outcome']!='Passed':raise RuntimeError('Check did not pass')
        tests=0
        outcomes=[]
        for trx in receipt.parent.glob('results/*.trx'):
            root=ET.parse(trx).getroot()
            counters=root.find('.//{*}Counters')
            outcomes.extend(root.findall('.//{*}UnitTestResult'))
            if counters is not None:
                if int(counters.attrib['failed'])!=0:raise RuntimeError('Test failures')
                tests+=int(counters.attrib['passed'])
        if tests==0 and args.group=='linq-behavior':
            results=[]
            for line in (receipt.parent/'stdout.log').read_text(encoding='utf-8-sig').splitlines():
                try:result=json.loads(line)
                except ValueError:continue
                if isinstance(result,dict) and 'ID' in result and 'Status' in result:results.append(result)
            moved={entry['source'] for entry in data.get('caseMoves',[])}
            required=set(data['baselineRegressionCases'])-moved
            actual={result['ID'] for result in results}
            if not required.issubset(actual) or len(actual)!=len(results):raise RuntimeError('Regression case coverage is incomplete')
            if any(result['Status']!='Passed' or result['FallbackCount']!=0 for result in results):raise RuntimeError('Regression failure or fallback')
            tests=len(results)
        if tests==0:raise RuntimeError('No passed tests in check receipt')
        for entry in data['entries']:
            if entry['group']!=args.group:continue
            if not entry['state'].startswith(('moved','verified')):raise RuntimeError('File has not moved')
            evidence=dict(receipt=str(receipt),passedTests=tests)
            source=(ROOT/entry['destination']).read_text(encoding='utf-8-sig')
            if 'SEAMLESS_SQLSERVER_TEST_CONNECTION' in source:
                namespace=re.search(r'namespace\s+([\w.]+)',source).group(1)
                prefix=namespace+'.'+Path(entry['destination']).stem+'.'
                matching=[result for result in outcomes if result.attrib['testName'].startswith(prefix)]
                if not matching:raise RuntimeError('SQL tests missing from receipt: '+prefix)
                evidence['passedTests']=sum(result.attrib['outcome']=='Passed' for result in matching)
                evidence['skippedTests']=sum(result.attrib['outcome']=='NotExecuted' for result in matching)
                evidence['environment']='SEAMLESS_SQLSERVER_TEST_CONNECTION'
            entry.setdefault('verification',{})[args.configuration]=evidence
            entry['currentSha256']=digest(ROOT/entry['destination'])
            entry['state']='moved-verified-'+args.configuration.lower()
            if all(config in entry['verification'] for config in ['Debug','Release']):entry['state']='verified'
            if any(check.get('skippedTests',0) for check in entry['verification'].values()):entry['state']='moved-environment-pending'
        save(data)
    if args.action=='retire':
        for entry in data['entries']:
            if entry['group']=='unclassified':continue
            if entry['state'] not in ('verified','moved-environment-pending'):raise RuntimeError('Unverified migration: '+entry['source'])
            if not all(config in entry.get('verification',{}) for config in ['Debug','Release']):raise RuntimeError('Missing configuration evidence: '+entry['source'])
        for entry in data['entries']:
            if entry['group']!='unclassified':continue
            if entry['source'] not in ('Test/Test.csproj','Test/GlobalUsings.cs','Test/AssemblyInfo.cs'):raise RuntimeError('Unknown retirement file')
            path=ROOT/entry['source']
            if digest(path)!=entry['sha256']:raise RuntimeError('Retirement source changed: '+str(path))
        for entry in data['entries']:
            if entry['group']!='unclassified':continue
            (ROOT/entry['source']).unlink();entry['state']='retired';entry['reason']='全責務を既存の移行先へ移したため、旧プロジェクト固有のファイルを廃止。SQL環境依存の未検証は明示的に保持。'
        save(data)
    if args.action=='route':
        for entry in data['entries']:
            if entry['state']!='planned':continue
            entry['group'],entry['destination']=initial_route(entry['source'])
            source=(ROOT/entry['source']).read_text(encoding='utf-8-sig')
            entry['testMethods']=re.findall(r'\[(?:Fact|Theory)\b[^\]]*\][\s\S]{0,200}?public\s+(?:async\s+)?(?:void|Task(?:<[^>]+>)?)\s+(\w+)\s*\(',source)
        save(data)
    if args.action=='move':
        if not args.group:raise RuntimeError('--group is required')
        selected=[e for e in data['entries'] if e['group']==args.group and e['state']=='planned']
        if not selected:raise RuntimeError('No planned entries in group')
        # Preflight every file before moving any of them.
        for entry in selected:
            source=(ROOT/entry['source']).resolve()
            destination=(ROOT/entry['destination']).resolve()
            if not source.is_relative_to(ROOT/'Test') or not destination.is_relative_to(ROOT):raise RuntimeError('Unsafe path')
            if destination.exists() or digest(source)!=entry['sha256']:raise RuntimeError('Source changed or destination exists: '+entry['source'])
        for entry in selected:
            destination=ROOT/entry['destination']
            destination.parent.mkdir(parents=True,exist_ok=True)
            (ROOT/entry['source']).rename(destination)
            entry['state']='moved-unverified'
            save(data)
    states={}
    for entry in data['entries']:
        states[entry['state']]=states.get(entry['state'],0)+1
        if entry['state']=='retired':
            if (ROOT/entry['source']).exists():raise RuntimeError('Retired file reappeared: '+entry['source'])
            continue
        path=ROOT/(entry['destination'] if entry['state'].startswith(('moved','verified')) else entry['source'])
        if not path.is_file():raise RuntimeError('Missing migration file: '+str(path))
    groups={}
    for entry in data['entries']:groups[entry['group']]=groups.get(entry['group'],0)+1
    print(json.dumps(dict(files=len(data['entries']),states=states,groups=groups),ensure_ascii=False))

if __name__=='__main__':main()
