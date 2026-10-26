# SPDX-License-Identifier: Apache-2.0
import importlib.util
from pathlib import Path
import unittest
import io
import tarfile
import tempfile

SPEC = importlib.util.spec_from_file_location('release_check', Path(__file__).resolve().parents[1] / 'scripts/release_check.py')
guard = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(guard)


class ReleaseGuardTests(unittest.TestCase):
    def test_proprietary_binary_blocked(self):
        with self.assertRaises(ValueError): guard.check_bytes('bad.dll', b'MZ')

    def test_binary_even_with_source_suffix_blocked(self):
        with self.assertRaises(ValueError): guard.check_bytes('bad.cs', b'hello\0world')

    def test_token_blocked(self):
        token = ('gh' + 'p_' + 'a' * 36).encode()
        with self.assertRaises(ValueError): guard.check_bytes('bad.md', token)

    def test_personal_path_blocked(self):
        path = ('/Us' + 'ers/' + 'private-person/file').encode()
        with self.assertRaises(ValueError): guard.check_bytes('bad.md', path)

    def test_synthetic_text_passes(self):
        guard.check_bytes('example.md', b'Original synthetic test text.')

    def test_source_manifest_and_content(self):
        self.assertGreater(guard.check_tree(), 20)

    def test_review_and_local_config_names_blocked(self):
        for name in ['user_review.txt', 'config.local.json', 'private/example.md', '.env']:
            with self.assertRaises(ValueError): guard.check_bytes(name, b'text')

    def test_archive_links_and_traversal_blocked(self):
        prefix = 'roon-file-reviews-' + (guard.ROOT / 'VERSION').read_text().strip()
        with tempfile.TemporaryDirectory() as temp:
            for i, (name, kind) in enumerate([(prefix + '/../outside.md', tarfile.REGTYPE),
                                            (prefix + '/README.md', tarfile.SYMTYPE)]):
                archive = Path(temp) / (str(i) + '.tar.gz')
                with tarfile.open(archive, 'w:gz') as tar:
                    info = tarfile.TarInfo(name); info.type = kind
                    tar.addfile(info, io.BytesIO(b''))
                with self.assertRaises(ValueError): guard.check_archive(archive)


if __name__ == '__main__': unittest.main()
