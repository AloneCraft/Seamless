"""Validate the owned Linux acceptance candidate without opening an existing DB."""
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
    return hashlib.sha256(Path(path).read_bytes()).hexdigest().upper()


def write(path, value):
    with Path(path).open('x', encoding='utf-8') as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2)


mode = sys.argv[1]
run = Path(sys.argv[2]).resolve()
prepared = json.loads((run / 'prepared.json').read_text(encoding='utf-8-sig'))
if str(run) != prepared['LinuxRun']:
    raise SystemExit('Unexpected owned run path.')
for name, sha256 in prepared['HarnessSha256'].items():
    if digest(run / name) != sha256:
        raise SystemExit('The prepared verification input changed: ' + name)

if mode == 'source':
    manifest_path = run / 'source-manifest.json'
    if digest(manifest_path) != prepared['SourceManifestSha256']:
        raise SystemExit('The fixed source manifest changed.')
    source = Path(prepared['LinuxSourceSnapshot']).resolve()
    if not str(source).startswith('/mnt/f/Seamless/'):
        raise SystemExit('The build requires an isolated F source snapshot.')
    manifest = json.loads(manifest_path.read_text(encoding='utf-8-sig'))
    expected = set()
    for relative, sha256 in manifest.items():
        relative = relative.replace('\\', '/')
        path = (source / relative).resolve()
        if source not in path.parents or not path.is_file() or digest(path) != sha256.upper():
            raise SystemExit('Fixed source mismatch: ' + relative)
        expected.add(relative)
    actual = {str(path.relative_to(source)) for path in source.rglob('*') if path.is_file()}
    if actual != expected:
        raise SystemExit('The fixed snapshot file set differs from its manifest.')
    project = run / 'probe/QueryVisualizationAcceptance.csproj'
    if digest(project) != prepared['ProbeProjectSha256'] or digest(run / 'probe/Program.cs') != prepared['SourceSha256']:
        raise SystemExit('The prepared P0 probe changed.')
    references = [item.attrib['Include'] for item in ET.parse(project).iter('ProjectReference')]
    if references != [str(source / 'Seamless.Management.Server/WindowsServer.csproj')]:
        raise SystemExit('The P0 project does not reference the fixed source snapshot.')
    write(run / ('source-' + sys.argv[3] + '.json'), {
        'snapshot': str(source), 'manifestSha256': digest(manifest_path), 'fileCount': len(expected),
        'probeSha256': digest(run / 'probe/Program.cs'), 'observedAtUtc': datetime.now(timezone.utc).isoformat()})
elif mode == 'main':
    pid = 90899
    protected = prepared.get('ProtectedProcessIds', [pid])
    if protected not in ([90899], [90899, 93455]):
        raise SystemExit('Unexpected protected process set.')
    identities = []
    for protected_pid in protected:
        process = Path('/proc') / str(protected_pid)
        try:
            identity = {'pid': protected_pid, 'present': True,
                        'startTicks': process.joinpath('stat').read_text().rsplit(')', 1)[1].split()[19],
                        'uid': process.stat().st_uid, 'executable': os.readlink(process / 'exe'),
                        'cgroup': process.joinpath('cgroup').read_text().strip(),
                        'comm': process.joinpath('comm').read_text().strip()}
        except FileNotFoundError:
            if protected_pid == pid:
                raise SystemExit('The protected main process is absent.')
            identity = {'pid': protected_pid, 'present': False}
        if identity['present'] and identity['uid'] != os.getuid():
            raise SystemExit('Unexpected protected process owner.')
        if protected_pid == 93455 and identity['present'] and (identity['executable'] != '/opt/google/chrome/chrome' or identity['comm'] != 'chrome'):
            raise SystemExit('The protected browser process identity is different.')
        identities.append(identity)
    record = dict(identities[0])
    record['observedAtUtc'] = datetime.now(timezone.utc).isoformat()
    record['protectedProcesses'] = identities
    record['diagnostics'] = []
    if any(not identity['present'] for identity in identities):
        record['diagnostics'].append('ProtectedBrowserAbsent')
    if 'seamless-manager-prepared-wal-live-20261109.scope' not in record['cgroup'] or record['uid'] != os.getuid():
        raise SystemExit('The protected main process identity is different.')
    original = run / 'main-prepared.json'
    if original.exists():
        before = json.loads(original.read_text())
        for field in ('pid', 'startTicks', 'uid', 'executable', 'cgroup'):
            if record[field] != before[field]:
                raise SystemExit('Protected main process changed: ' + field)
        if record['protectedProcesses'] != before.get('protectedProcesses', [identities[0]]):
            raise SystemExit('A protected process changed or disappeared after preparation.')
    write(run / ('main-' + sys.argv[3] + '.json'), record)
elif mode == 'paths':
    expected = run / 'artifacts'
    for name in ('QueryVisualizationAcceptance', 'WindowsServer'):
        properties = json.loads((run / ('paths-' + name + '.json')).read_text())['Properties']
        for key in ('ArtifactsPath', 'BaseOutputPath', 'BaseIntermediateOutputPath'):
            actual = Path(properties[key]).resolve()
            if actual != expected and expected not in actual.parents:
                raise SystemExit('Project output escaped owned artifacts: ' + name + ' ' + key)
        if Path(properties['RestorePackagesPath']).resolve() != Path(os.environ['NUGET_PACKAGES']).resolve():
            raise SystemExit('Unexpected package root: ' + name)
    if digest(run / 'probe' / 'Program.cs') != prepared['SourceSha256']:
        raise SystemExit('The fixed P0 source changed.')
elif mode == 'runtime':
    runtime = run / 'runtime'
    assets = set()
    for path in runtime.glob('*.deps.json'):
        document = json.loads(path.read_text())
        for target in document.get('targets', {}).values():
            for library in target.values():
                assets.update(name for name in library.get('native', {}) if name.startswith('runtimes/linux-x64/native/'))
                assets.update(name for name, value in library.get('runtimeTargets', {}).items()
                              if value.get('rid') == 'linux-x64' and value.get('assetType') == 'native')
    for asset in assets:
        if not re.fullmatch(r'runtimes/linux-x64/native/[A-Za-z0-9_.-]+', asset):
            raise SystemExit('Invalid native asset path.')
        destination = runtime / asset
        flat = runtime / destination.name
        if destination.exists():
            if flat.exists() and digest(destination) != digest(flat):
                raise SystemExit('Conflicting native asset: ' + asset)
        else:
            if not flat.is_file():
                raise SystemExit('Missing native asset: ' + asset)
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(flat, destination)
    native = runtime / 'runtimes/linux-x64/native'
    names = ('libnative_device.so', 'libnative_device_libaio.so', 'libbftree_garnet.so', 'libdiskann_garnet.so', 'liblua54.so')
    for name in names:
        if not (native / name).is_file():
            raise SystemExit('Required native asset missing: ' + name)
    roots = []
    endpoint_manifest = runtime / 'QueryVisualizationAcceptance.staticwebassets.endpoints.json'
    if not endpoint_manifest.is_file():
        raise SystemExit('Published entry static asset endpoints manifest missing.')
    endpoints = json.loads(endpoint_manifest.read_text()).get('Endpoints', [])
    routes = {endpoint.get('Route') for endpoint in endpoints}
    for name in ('manager.css', 'interaction.js', 'execution-plan.css', 'query-trace.css'):
        if '_content/Seamless.Management.Components/' + name not in routes:
            raise SystemExit('Published UI endpoint missing: ' + name)
    roots.append({'manifest': endpoint_manifest.name, 'sha256': digest(endpoint_manifest), 'endpointCount': len(endpoints)})
    for path in runtime.glob('*.staticwebassets.runtime.json'):
        values = json.loads(path.read_text()).get('ContentRoots', [])
        for value in values:
            if re.match(r'^[A-Za-z]:', value) or '\\' in value:
                raise SystemExit('Windows static asset content root: ' + path.name)
            if not Path(value).exists():
                raise SystemExit('Missing static asset content root: ' + path.name)
        roots.append({'manifest': path.name, 'contentRoots': values})
    for name in ('manager.css', 'interaction.js'):
        if not (runtime / 'wwwroot/_content/Seamless.Management.Components' / name).is_file():
            raise SystemExit('Published UI asset missing: ' + name)
    files = [{'path': str(path.relative_to(runtime)), 'sha256': digest(path), 'bytes': path.stat().st_size}
             for path in sorted(runtime.rglob('*')) if path.is_file()]
    write(run / 'linux-runtime-proof.json', {'sourceSha256': digest(run / 'probe/Program.cs'),
          'native': [{'name': name, 'sha256': digest(native / name)} for name in names], 'staticAssets': roots, 'files': files})
elif mode == 'host':
    ready = json.loads(Path(sys.argv[3]).read_text())
    pid = int(ready['pid'])
    process = Path('/proc') / str(pid)
    if pid in prepared.get('ProtectedProcessIds', [90899]) or sys.argv[4] not in process.joinpath('cgroup').read_text():
        raise SystemExit('The ready PID is not in the owned host scope.')
    if process.stat().st_uid != os.getuid() or Path(ready['root']).name != 'visual-plan-' + prepared['Nonce']:
        raise SystemExit('Unexpected host owner or root.')
    write(run / ('host-' + Path(sys.argv[3]).parent.name + '.json'), {
        'pid': pid, 'startTicks': process.joinpath('stat').read_text().rsplit(')', 1)[1].split()[19],
        'observedAtUtc': datetime.now(timezone.utc).isoformat(), 'runtimeSha256': digest(run / 'runtime/QueryVisualizationAcceptance.dll')})
else:
    raise SystemExit('Unknown verification mode.')
