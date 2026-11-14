"""Compare original discovered cases with method-level receipts after migration."""
import argparse
import collections
import json
import re
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
MANIFEST=ROOT/'Document/refactoring-designs/20261104-test-migration.json'

def method(name):
    return re.sub(r'\[\d+\]$','',name.split('(')[0])

def audit(log_root,names):
    manifest=json.loads(MANIFEST.read_text(encoding='utf-8'))
    baseline=collections.Counter(method(name) for name in manifest['baselineTests'])
    remaps={entry['source']:entry['destination'] for entry in manifest.get('caseMoves',[])}
    actual=collections.Counter()
    verified=collections.Counter()
    outcomes={}
    for name in names:
        folder=log_root/name
        receipt=json.loads((folder/'receipt.json').read_text(encoding='utf-8-sig'))
        outcomes[name]=receipt['Outcome']
        if receipt['Outcome']!='Passed':raise RuntimeError('Coverage requires a passed check receipt: '+name)
        run=collections.Counter()
        passed=collections.Counter()
        for path in (folder/'results').glob('*.trx'):
            root=ET.parse(path).getroot()
            for result in root.findall('.//{*}UnitTestResult'):
                # Parameterized contract wrappers have their complete identities in JSONL receipts.
                key=method(result.attrib['testName'])
                if key.startswith('Benchmark.Dynamic.Tests.GeneratorContractTests.'):continue
                run[key]+=1
                if result.attrib.get('outcome')=='Passed':passed[key]+=1
        for path in folder.glob('*-results.jsonl'):
            for line in path.read_text(encoding='utf-8-sig').splitlines():
                result=json.loads(line)
                if 'ID' not in result or 'Status' not in result:continue
                run[method(result['ID'])]+=1
                if result['Status']=='Passed':passed[method(result['ID'])]+=1
        # A method can appear in several configurations; this is coverage, not an inflated sum.
        actual|=run
        verified|=passed
    missing={key:count-actual[remaps.get(key,key)] for key,count in baseline.items() if actual[remaps.get(key,key)]<count}
    unverified={key:count-verified[remaps.get(key,key)] for key,count in baseline.items() if verified[remaps.get(key,key)]<count}
    return dict(baselineCases=sum(baseline.values()),baselineMethods=len(baseline),missingCases=sum(missing.values()),missingMethods=missing,unverifiedCases=sum(unverified.values()),unverifiedMethods=unverified,checks=outcomes)

if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--logs',default='F:/CodexTemp/test-retirement-20261104')
    parser.add_argument('--checks',nargs='+',required=True)
    parser.add_argument('--output')
    args=parser.parse_args()
    report=audit(Path(args.logs),args.checks)
    text=json.dumps(report,ensure_ascii=False,indent=2)
    if args.output:Path(args.output).write_text(text+'\n',encoding='utf-8')
    print(text)
    if report['missingCases']:raise SystemExit(1)
