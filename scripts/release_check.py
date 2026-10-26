#!/usr/bin/env python3
# SPDX-License-Identifier: Apache-2.0
"""Allowlist/content checks for the public source tree or an unextracted archive."""
import argparse
import re
from pathlib import Path, PurePosixPath
import subprocess
import tarfile

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = 'release-manifest.txt'
PATTERNS = {
    'private-key': re.compile(r'-----BEGIN (?:RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----'),
    'github-token': re.compile(r'\b(?:gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{30,})\b'),
    'aws-key': re.compile(r'\b(?:AKIA|ASIA)[A-Z0-9]{16}\b'),
    'credential-url': re.compile(r'(?:https?|smb)://[^\s/@:]+:[^\s/@]+@'),
    'personal-home-path': re.compile(r'/(?:Users|home)/[A-Za-z0-9_.-]+'),
    'private-lan-address': re.compile(r'\b(?:192\.168\.\d{1,3}\.\d{1,3}|10\.\d{1,3}\.\d{1,3}\.\d{1,3}|172\.(?:1[6-9]|2\d|3[01])\.\d{1,3}\.\d{1,3})\b'),
}
FORBIDDEN_SUFFIXES = {'.dll', '.exe', '.pdb', '.bin', '.ldb', '.sqlite', '.sqlite3', '.log', '.jsonl', '.plist', '.pem', '.key'}
LOCAL_DIRS = {'bin', 'obj', '__pycache__', '.git', 'private', 'artifacts', 'backups', 'dist'}


def allowlist(root=ROOT):
    rows = [line.strip() for line in (root / MANIFEST).read_text().splitlines() if line.strip() and not line.startswith('#')]
    if len(set(rows)) != len(rows):
        raise ValueError('Duplicate release allowlist entry')
    for name in rows:
        path = PurePosixPath(name)
        if path.is_absolute() or '..' in path.parts or str(path) != name:
            raise ValueError('Invalid release allowlist path')
    return set(rows)


def check_bytes(name, data):
    path = PurePosixPath(name)
    if (set(path.parts) & {'private', 'artifacts', 'backups', 'data', 'tools', 'vendor', '.git', 'node_modules'} or
            path.name in {'user_review.txt', '.DS_Store'} or
            path.name.startswith(('.env', 'config.local'))):
        raise ValueError('Private/local material is never releasable: ' + name)
    if PurePosixPath(name).suffix.lower() in FORBIDDEN_SUFFIXES or len(data) > 256 * 1024:
        raise ValueError('Forbidden binary/data/oversized release file: ' + name)
    if b'\0' in data:
        raise ValueError('Binary content is forbidden: ' + name)
    text = data.decode('utf-8')
    for kind, pattern in PATTERNS.items():
        if pattern.search(text):
            raise ValueError(kind + ' detected in ' + name + ' (value not printed)')


def check_tree(root=ROOT):
    allowed = allowlist(root)
    for name in sorted(allowed):
        path = root / name
        if path.is_symlink() or not path.is_file() or path.resolve() != path.absolute():
            raise ValueError('Missing/nonregular/symlinked source: ' + name)
        check_bytes(name, path.read_bytes())
    if (root / '.git').exists():
        result = subprocess.run(['git', '-C', str(root), 'ls-files', '-z', '--cached', '--others', '--exclude-standard'],
                                capture_output=True, check=True, timeout=30)
        names = set(result.stdout.decode().strip('\0').split('\0')) - {''}
    else:
        names = {str(p.relative_to(root)) for p in root.rglob('*') if p.is_file() and not any(part in LOCAL_DIRS for part in p.relative_to(root).parts)}
    if names - allowed:
        raise ValueError('Unallowlisted source files: ' + ', '.join(sorted(names - allowed)))
    return len(allowed)


def check_archive(archive, root=ROOT):
    allowed = allowlist(root)
    prefix = 'roon-file-reviews-' + (root / 'VERSION').read_text().strip()
    seen = set()
    with tarfile.open(archive, 'r:gz') as tar:
        for item in tar:
            parts = PurePosixPath(item.name).parts
            if not item.isfile() or not parts or parts[0] != prefix or '..' in parts:
                raise ValueError('Unsafe or non-file archive entry')
            name = str(PurePosixPath(*parts[1:]))
            if name not in allowed or name in seen:
                raise ValueError('Unexpected/duplicate archive member: ' + name)
            if item.size > 256 * 1024 or item.pax_headers:
                raise ValueError('Oversized archive member or private extended metadata')
            source = tar.extractfile(item)
            data = source.read(256 * 1024 + 1)
            check_bytes(name, data)
            if data != (root / name).read_bytes():
                raise ValueError('Archive differs from reviewed source: ' + name)
            seen.add(name)
    if seen != allowed:
        raise ValueError('Archive is incomplete')
    return len(seen)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--archive', type=Path)
    args = parser.parse_args()
    count = check_archive(args.archive) if args.archive else check_tree()
    print('PASS: ' + str(count) + ' allowlisted UTF-8 source files; no detected forbidden content')


if __name__ == '__main__': main()
