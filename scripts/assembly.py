#!/usr/bin/env python3
# SPDX-License-Identifier: Apache-2.0
"""Local, stopped-Core DLL install/restore. Dry-run by default; no DB access.

This does not stop/start Roon, bypass signing, request elevation, or use a network.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import stat
import subprocess
import sys
import tempfile
import time

BASELINE = 'a61a72bc683db159a13fd08a14921f781cf7c2685a7c9ec193bcae075db2612d'


def sha(path):
    h = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def regular(path):
    path = Path(path).absolute()
    if path.is_symlink() or not stat.S_ISREG(path.stat().st_mode):
        raise ValueError('A regular file, not a symlink/device, is required')
    if path != path.resolve():
        raise ValueError('Symlinked file paths are not accepted')
    return path


def target_path(path):
    path = regular(path)
    if (path.name != 'Roon.Broker.Core.dll' or
            not any(p.endswith('.app') for p in path.parts) or
            'Contents' not in path.parts or 'MonoBundle' not in path.parts):
        raise ValueError('Expected the broker DLL inside a local Roon application bundle')
    if str(path).startswith('/Volumes/'):
        raise ValueError('Run locally on the Core Mac, not against a mounted share')
    return path


def stopped():
    for name in ('Roon', 'RoonServer', 'RoonAppliance', 'RAATServer'):
        result = subprocess.run(['/usr/bin/pgrep', '-x', name], capture_output=True, timeout=10)
        if result.returncode == 0:
            raise RuntimeError('Quit all Roon components first: ' + name + ' is running')
        if result.returncode != 1:
            raise RuntimeError('Could not determine whether Roon is stopped')


def require_local_macos(target):
    if sys.platform != 'darwin':
        raise RuntimeError('Applying an update is supported only on the Core Mac')
    if not os.statvfs(target).f_flag & getattr(os, 'ST_LOCAL', 0x1000):
        raise RuntimeError('Target must be on a local filesystem')


def atomic_replace(target, payload, expected):
    """Stage beside the DLL, then recheck process state and old bytes before rename."""
    fd, name = tempfile.mkstemp(prefix='.roon-file-reviews-', dir=target.parent)
    staged = Path(name)
    try:
        with os.fdopen(fd, 'wb') as output:
            output.write(payload)
            output.flush()
            os.fsync(output.fileno())
            os.fchmod(output.fileno(), stat.S_IMODE(target.stat().st_mode))
        stopped()
        if sha(target) != expected:
            raise RuntimeError('Target changed; refusing to overwrite a Roon update')
        os.replace(staged, target)
        if sha(target) != hashlib.sha256(payload).hexdigest():
            raise RuntimeError('Post-install verification failed; keep Roon stopped and restore the saved DLL')
    finally:
        if staged.exists():
            staged.unlink()  # Only the exact temporary file created above.


def install(target, candidate, report_path, backup, apply=False):
    target, candidate = target_path(target), regular(candidate)
    if target == candidate:
        raise ValueError('Candidate must be an offline output, not the installed DLL')
    report = json.loads(regular(report_path).read_text())
    candidate_hash = sha(candidate)
    if (report.get('adapter') != 'roon-2.74-build-1712' or
            report.get('baselineSHA256') != BASELINE or
            report.get('patchedSHA256') != candidate_hash or
            report.get('externalRuntimeDependency') is not False or
            report.get('sourceId') != 'RoonUserReview' or
            report.get('unrelatedMethodBodiesVerified', 0) < 1):
        raise ValueError('Candidate report does not describe the supported, locally built patch')
    if sha(target) != BASELINE:
        raise ValueError('Target is not the exact stock supported build; do not overwrite an unknown update/patch')
    result = {'operation': 'install', 'dry_run': not apply, 'database_writes': 0,
              'original_sha256': BASELINE, 'candidate_sha256': candidate_hash}
    if not apply:
        return result
    require_local_macos(target)
    stopped()
    time.sleep(1)
    stopped()
    backup = Path(backup).absolute()
    backup.mkdir(parents=True, exist_ok=False)
    os.chmod(backup, 0o700)
    payload = candidate.read_bytes()
    if hashlib.sha256(payload).hexdigest() != candidate_hash:
        raise RuntimeError('Candidate changed during preflight')
    original = target.read_bytes()
    if hashlib.sha256(original).hexdigest() != BASELINE:
        raise RuntimeError('Target changed during preflight')
    with (backup / 'original.dll').open('xb') as output:
        output.write(original)
        output.flush()
        os.fsync(output.fileno())
    if sha(backup / 'original.dll') != BASELINE:
        raise RuntimeError('Original DLL backup verification failed')
    journal = {'schema': 1, 'baseline_sha256': BASELINE, 'patched_sha256': candidate_hash}
    with (backup / 'install.json').open('x') as output:
        json.dump(journal, output, indent=2)
        output.flush()
        os.fsync(output.fileno())
    atomic_replace(target, payload, BASELINE)
    result['backup'] = str(backup)
    return result


def restore(target, backup, apply=False):
    target = target_path(target)
    backup = Path(backup)
    original = regular(backup / 'original.dll')
    journal = json.loads(regular(backup / 'install.json').read_text())
    if journal.get('schema') != 1 or journal.get('baseline_sha256') != BASELINE or sha(original) != BASELINE:
        raise ValueError('Backup is not the exact supported original assembly')
    current = sha(target)
    if current != journal.get('patched_sha256'):
        raise ValueError('Installed DLL is not the recorded patch; never overwrite a newer Roon build')
    if apply:
        require_local_macos(target)
        stopped()
        payload = original.read_bytes()
        if hashlib.sha256(payload).hexdigest() != BASELINE:
            raise RuntimeError('Backup changed during preflight')
        atomic_replace(target, payload, current)
    return {'operation': 'restore', 'dry_run': not apply, 'database_writes': 0, 'restored_sha256': BASELINE}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest='command', required=True)
    for name in ('install', 'restore'):
        child = sub.add_parser(name)
        child.add_argument('--target', type=Path, required=True)
        child.add_argument('--backup', type=Path, required=True)
        child.add_argument('--apply', action='store_true', help='Actually replace the DLL; default is dry-run')
        child.add_argument('--acknowledge-unsupported', action='store_true')
        if name == 'install':
            child.add_argument('--candidate', type=Path, required=True)
            child.add_argument('--report', type=Path, required=True)
    args = parser.parse_args()
    if args.apply and not args.acknowledge_unsupported:
        parser.error('--apply requires --acknowledge-unsupported; read SECURITY.md and make an official Roon backup')
    try:
        if args.command == 'install':
            result = install(args.target, args.candidate, args.report, args.backup, args.apply)
        else:
            result = restore(args.target, args.backup, args.apply)
        print(json.dumps(result))
    except (OSError, ValueError, RuntimeError, subprocess.SubprocessError) as error:
        parser.exit(1, str(error) + '\n')


if __name__ == '__main__':
    main()
