"""Bounded phase evidence for an explicitly selected fixed-source Linux test cohort."""
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import sys
import xml.etree.ElementTree as ET
from datetime import datetime, timezone


def digest(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest().upper()


def write(path, value):
    with path.open('x', encoding='utf-8') as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2)


mode, run_arg, phase = sys.argv[1:4]
run = Path(run_arg).resolve()
prepared = json.loads((run / 'prepared.json').read_text(encoding='utf-8-sig'))
if str(run) != prepared['LinuxRun'] or prepared['Configuration'] != 'Release':
    raise SystemExit('Unexpected prepared Linux Release run.')
source = Path(prepared['LinuxSourceSnapshot']).resolve()
work = run / 'near-tests'
project = source / 'Tests/Management.Tests/Management.Tests.csproj'
if not project.is_file():
    raise SystemExit('The fixed source snapshot does not contain Management.Tests.')
if mode == 'input':
    scripts = {name: digest(Path(__file__).parent / name) for name in
               ('QueryVisualizationLinux.Tests.sh', 'QueryVisualizationLinux.Tests.Verify.py')}
    tests_prepared = json.loads((run / 'tests-prepared.json').read_text(encoding='utf-8-sig'))
    if tests_prepared['LinuxRun'] != str(run) or tests_prepared['SourceManifestSha256'] != prepared['SourceManifestSha256'] or tests_prepared['HarnessSha256'] != scripts:
        raise SystemExit('The separately prepared fixed test harness changed.')
    if phase != 'build':
        build = json.loads((work / 'build-result.json').read_text())
        original = json.loads((work / 'build-input.json').read_text())
        if build['status'] != 'Passed' or original['scripts'] != scripts or original['sourceManifestSha256'] != prepared['SourceManifestSha256']:
            raise SystemExit('The successful fixed test build or its harness identity changed.')
        output = json.loads((work / 'test-output.json').read_text())
        directory = Path(output['directory'])
        if digest(directory / 'Management.Tests.dll') != output['dllSha256'] or digest(directory / 'Management.Tests.deps.json') != output['depsSha256']:
            raise SystemExit('The fixed test assembly or dependency manifest changed.')
    write(work / (phase + '-input.json'), {
        'phase': phase, 'sourceManifestSha256': prepared['SourceManifestSha256'],
        'project': str(project), 'projectSha256': digest(project),
        'scripts': scripts,
        'configuration': 'Release', 'protectedPid': 90899,
        'protectedProcessIds': prepared.get('ProtectedProcessIds', [90899]),
        'databaseRoot': os.environ['SEAMLESS_MANAGEMENT_TEST_ROOT'],
        'cpuList': '0-7', 'memoryHigh': '1400M', 'memoryMax': '1700M',
        'observedAtUtc': datetime.now(timezone.utc).isoformat()})
elif mode == 'paths':
    properties = json.loads((work / 'paths.json').read_text())['Properties']
    expected = run / 'artifacts'
    for key in ('ArtifactsPath', 'BaseOutputPath', 'BaseIntermediateOutputPath'):
        actual = Path(properties[key]).resolve()
        if actual != expected and expected not in actual.parents:
            raise SystemExit('Tests output escaped the owned artifacts: ' + key)
    if Path(properties['RestorePackagesPath']).resolve() != Path(os.environ['NUGET_PACKAGES']).resolve():
        raise SystemExit('Unexpected tests package root.')
elif mode == 'native':
    outputs = list((run / 'artifacts/bin/Management.Tests/release').glob('Management.Tests.deps.json'))
    if len(outputs) != 1:
        raise SystemExit('Expected one fixed Release test output.')
    output = outputs[0].parent
    assets = set()
    for target in json.loads(outputs[0].read_text()).get('targets', {}).values():
        for library in target.values():
            assets.update(name for name in library.get('native', {}) if name.startswith('runtimes/linux-x64/native/'))
            assets.update(name for name, value in library.get('runtimeTargets', {}).items()
                          if value.get('rid') == 'linux-x64' and value.get('assetType') == 'native')
    for asset in assets:
        if not re.fullmatch(r'runtimes/linux-x64/native/[A-Za-z0-9_.-]+', asset):
            raise SystemExit('Invalid tests native asset path.')
        destination = output / asset
        flat = output / destination.name
        if not destination.is_file():
            if not flat.is_file():
                raise SystemExit('Missing tests native asset: ' + asset)
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(flat, destination)
        elif flat.is_file() and digest(destination) != digest(flat):
            raise SystemExit('Conflicting tests native asset: ' + asset)
    names = ('libnative_device.so', 'libnative_device_libaio.so', 'libbftree_garnet.so', 'libdiskann_garnet.so', 'liblua54.so')
    for name in names:
        if not (output / 'runtimes/linux-x64/native' / name).is_file():
            raise SystemExit('Required tests native asset missing: ' + name)
    write(work / 'test-output.json', {'directory': str(output), 'dllSha256': digest(output / 'Management.Tests.dll'),
          'depsSha256': digest(outputs[0]), 'native': [{'name': name, 'sha256': digest(output / 'runtimes/linux-x64/native' / name)} for name in names]})
elif mode == 'result':
    status, source_status, main_status = map(int, sys.argv[4:7])
    result = {'phase': phase, 'commandExitCode': status, 'sourceVerificationExitCode': source_status,
              'protectedMainVerificationExitCode': main_status, 'observedAtUtc': datetime.now(timezone.utc).isoformat()}
    good = status == source_status == main_status == 0
    if phase != 'build':
        trx = work / phase / 'checks.trx'
        if not trx.is_file():
            result['error'] = 'MissingTrx'
            good = False
        else:
            xml = ET.parse(trx)
            ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
            counters = xml.find('.//t:Counters', ns)
            tests = xml.findall('.//t:UnitTestResult', ns)
            result['counters'] = dict(counters.attrib) if counters is not None else {}
            result['testNames'] = [test.attrib['testName'] for test in tests]
            result['trxSha256'] = digest(trx)
            expected = {'preview': 38, 'generated': 39, 'trace-core': 9, 'trace-recorder': 8, 'trace-native': 4, 'history': 19, 'typed': 14, 'typed-crash': 3, 'heat': 27, 'object-ui': 177}.get(phase)
            result['expectedCount'] = expected
            if not tests or any(test.attrib.get('outcome') != 'Passed' for test in tests):
                good = False
            if expected is not None and len(tests) != expected:
                good = False
            if counters is None or int(counters.get('total', 0)) != len(tests) or int(counters.get('executed', 0)) != len(tests):
                good = False
    result['status'] = 'Passed' if good else 'Failed'
    write(work / (phase + '-result.json'), result)
    if not good:
        raise SystemExit('The explicit Linux test phase did not meet its evidence requirements.')
else:
    raise SystemExit('Unknown tests verification mode.')
