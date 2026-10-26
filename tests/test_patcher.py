# SPDX-License-Identifier: Apache-2.0
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]


class PatcherFailClosedTests(unittest.TestCase):
    def run_patcher(self, original, helper, output):
        return subprocess.run([os.environ.get('DOTNET', 'dotnet'), 'run', '--no-build', '--project',
            str(ROOT / 'bridge/Patcher/Patcher.csproj'), '--', str(original), str(helper), str(output)],
            capture_output=True, text=True, timeout=30)

    def test_unknown_original_produces_no_candidate(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp).resolve()
            original, helper, output = (root / n for n in ('original.dll', 'helper.dll', 'out.dll'))
            original.write_bytes(b'Not a supported Roon assembly')
            helper.write_bytes(b'Synthetic input only')
            result = self.run_patcher(original, helper, output)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn('Unsupported original', result.stderr)
            self.assertFalse(output.exists())
            self.assertFalse(Path(str(output) + '.json').exists())

    def test_application_bundle_output_refused(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp).resolve()
            original, helper = root / 'original.dll', root / 'helper.dll'
            output = root / 'Example.app/Contents/out.dll'
            result = self.run_patcher(original, helper, output)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn('local scratch file', result.stderr)
            self.assertFalse(output.exists())

    def test_input_cannot_be_output(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp).resolve()
            original = root / 'original.dll'
            original.write_bytes(b'Keep this input unchanged')
            result = self.run_patcher(original, root / 'helper.dll', original)
            self.assertNotEqual(result.returncode, 0)
            self.assertEqual(original.read_bytes(), b'Keep this input unchanged')


if __name__ == '__main__': unittest.main()
