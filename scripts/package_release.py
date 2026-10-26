#!/usr/bin/env python3
# SPDX-License-Identifier: Apache-2.0
"""Build a deterministic source-only archive. Never commits, tags, uploads or pushes."""
import argparse
import gzip
import hashlib
import io
from pathlib import Path
import tarfile

from release_check import ROOT, allowlist, check_archive, check_tree


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    check_tree()
    output = args.output.absolute()
    if output.exists() or output.with_suffix(output.suffix + '.sha256').exists():
        raise ValueError('Release output/checksum already exists; never overwrite')
    output.parent.mkdir(parents=True, exist_ok=True)
    prefix = 'roon-file-reviews-' + (ROOT / 'VERSION').read_text().strip()
    with output.open('xb') as raw:
        with gzip.GzipFile(filename='', fileobj=raw, mode='wb', mtime=0) as compressed:
            with tarfile.open(fileobj=compressed, mode='w', format=tarfile.USTAR_FORMAT) as tar:
                for name in sorted(allowlist()):
                    data = (ROOT / name).read_bytes()
                    info = tarfile.TarInfo(prefix + '/' + name)
                    info.size = len(data)
                    info.mode = 0o644
                    info.mtime = 0
                    info.uid = info.gid = 0
                    info.uname = info.gname = ''
                    tar.addfile(info, io.BytesIO(data))
    count = check_archive(output)
    digest = hashlib.sha256(output.read_bytes()).hexdigest()
    with output.with_suffix(output.suffix + '.sha256').open('x') as checksum:
        checksum.write(digest + '  ' + output.name + '\n')
    print('Prepared ' + output.name + ': ' + str(count) + ' source files, SHA-256 ' + digest)


if __name__ == '__main__': main()
