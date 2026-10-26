# SPDX-License-Identifier: Apache-2.0
import importlib.util
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

SPEC = importlib.util.spec_from_file_location('assembly', Path(__file__).resolve().parents[1] / 'scripts/assembly.py')
assembly = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(assembly)


class AssemblySafetyTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name).resolve()
        self.target = self.root / 'Example.app/Contents/MonoBundle/Roon.Broker.Core.dll'
        self.target.parent.mkdir(parents=True)
        self.target.write_bytes(b'synthetic stock DLL, not executable')
        self.candidate = self.root / 'candidate.dll'
        self.candidate.write_bytes(b'synthetic patched DLL, not executable')
        self.baseline = assembly.sha(self.target)
        self.report = self.root / 'candidate.json'
        self.report.write_text(json.dumps({'adapter': 'roon-2.74-build-1712', 'baselineSHA256': self.baseline,
            'patchedSHA256': assembly.sha(self.candidate), 'externalRuntimeDependency': False,
            'sourceId': 'RoonUserReview', 'unrelatedMethodBodiesVerified': 1}))
        self.backup = self.root / 'backup'
        self.basepatch = patch.object(assembly, 'BASELINE', self.baseline)
        self.basepatch.start()

    def tearDown(self):
        self.basepatch.stop()
        self.tmp.cleanup()

    def install(self, apply=False):
        return assembly.install(self.target, self.candidate, self.report, self.backup, apply)

    def test_default_is_read_only(self):
        before = self.target.read_bytes()
        self.assertTrue(self.install()['dry_run'])
        self.assertEqual(self.target.read_bytes(), before)
        self.assertFalse(self.backup.exists())

    def test_unknown_version_refused(self):
        self.target.write_bytes(b'new Roon version')
        with self.assertRaises(ValueError): self.install()

    def test_tampered_candidate_refused(self):
        self.candidate.write_bytes(b'modified')
        with self.assertRaises(ValueError): self.install()

    def test_symlink_refused(self):
        alias = self.root / 'alias.dll'
        alias.symlink_to(self.candidate)
        with self.assertRaises(ValueError):
            assembly.install(self.target, alias, self.report, self.backup)

    def test_non_application_target_refused(self):
        other = self.root / 'Roon.Broker.Core.dll'
        other.write_bytes(self.target.read_bytes())
        with self.assertRaises(ValueError):
            assembly.install(other, self.candidate, self.report, self.backup)

    @patch.object(assembly, 'require_local_macos')
    @patch.object(assembly, 'stopped', side_effect=RuntimeError('Core running'))
    def test_running_core_refused(self, stopped, macos):
        with self.assertRaises(RuntimeError): self.install(True)
        self.assertEqual(assembly.sha(self.target), self.baseline)
        self.assertFalse(self.backup.exists())

    @patch.object(assembly, 'require_local_macos')
    @patch.object(assembly, 'stopped')
    @patch.object(assembly.time, 'sleep')
    def test_install_and_restore_only_dll(self, sleep, stopped, macos):
        marker = self.root / 'Database/keep.txt'
        marker.parent.mkdir(); marker.write_bytes(b'unchanged synthetic database')
        result = self.install(True)
        self.assertEqual(result['database_writes'], 0)
        self.assertEqual(self.target.read_bytes(), self.candidate.read_bytes())
        self.assertEqual(assembly.sha(self.backup / 'original.dll'), self.baseline)
        dry = assembly.restore(self.target, self.backup)
        self.assertTrue(dry['dry_run'])
        assembly.restore(self.target, self.backup, True)
        self.assertEqual(assembly.sha(self.target), self.baseline)
        self.assertEqual(marker.read_bytes(), b'unchanged synthetic database')

    @patch.object(assembly, 'require_local_macos')
    @patch.object(assembly, 'stopped')
    @patch.object(assembly.time, 'sleep')
    def test_restore_refuses_updated_core(self, sleep, stopped, macos):
        self.install(True)
        self.target.write_bytes(b'new Roon version')
        with self.assertRaises(ValueError): assembly.restore(self.target, self.backup, True)

    @patch.object(assembly, 'require_local_macos')
    @patch.object(assembly.time, 'sleep')
    def test_restarted_core_blocks_atomic_swap(self, sleep, macos):
        with patch.object(assembly, 'stopped', side_effect=[None, None, RuntimeError('restarted')]):
            with self.assertRaises(RuntimeError): self.install(True)
        self.assertEqual(assembly.sha(self.target), self.baseline)
        self.assertFalse(list(self.target.parent.glob('.roon-file-reviews-*')))


if __name__ == '__main__': unittest.main()
