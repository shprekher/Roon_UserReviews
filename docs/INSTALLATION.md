# Installation safety, validation, and rollback

Read the README first. This is an expert, exact-build source release, not a
signed one-click installer. Do not install until the release owner has resolved
the manual-review checklist. The live installation used a private deployment
procedure; the new public local helper has synthetic tests only.

## Before changing anything

- Check Roon 2.74 build 1712, macOS compatibility, and the original assembly hash.
- Back up Roon using its official backup feature and preserve the original
  application separately. Keep backups private and accessible without Roon.
- Fully quit all Roon components; the helper refuses active known processes.
  Stop normal automatic relaunch using supported UI settings. A socket timeout
  or disconnected client is **not** proof that Core has stopped.
- Work on the Core Mac, on local storage. Never patch an app through SMB.
- Read the candidate report and inspect the source you build. A hash/report
  supplied with an arbitrary downloaded DLL does not establish authenticity.
- Do not work around signing, permission, or security-policy failures. Determine
  whether installation is permitted by the system and Roon's terms first.

## Validate after installation

1. Start Core normally and ensure normal connection and playback are healthy.
2. Use an album with local tracks and an online review. Supply only your own
   temporary test prose in `user_review.txt` if there is no existing file.
3. Reopen the album. Confirm the online source remains default and **User review.**
   is selectable under Read more with exact text.
4. Edit the test file, reopen the panel, and check updated text. Delete only the
   temporary file you created; confirm stock behavior. Restore a valid file and
   verify it reappears. Do not overwrite an existing user's review to test.
5. For multi-disc albums, use one file in the common album folder. Two candidate
   files should retain stock behavior rather than choosing arbitrarily.

No runtime verification step calls a database writer. Diagnostics/screenshots
must not expose copyrighted prose, account data, or private library details.

## Restore the original DLL

Fully quit Roon again. Use the **same target** and original-DLL backup directory
recorded for this installation:

```sh
python3 scripts/assembly.py restore --target "$TARGET" --backup backups/first-install
```

This is a dry-run. After reviewing the result, repeat with
`--apply --acknowledge-unsupported`. Start Roon normally and check connection,
playback, and ordinary online reviews. This restores only the assembly; it does
not delete review files or modify any database or music metadata.

The tool refuses to restore over a different DLL hash. If Roon has updated since
installation, **do not** force restoration of the old build. Use Roon's official
repair/reinstall procedure for the current release and consult vendor guidance.

If a failure occurred before the swap, the original DLL can already be in place.
Verify its hash before attempting anything else. Keep Roon stopped if hashes or
startup are uncertain; do not restore an old whole database just to remove this
bridge. Retain the backup and journal for narrow manual recovery.

## Updates

The patcher intentionally supports no unknown build. An update may replace the
bridge; it does not auto-repatch, downgrade, or fall back to an older DLL.
Reverification of a new stock build is a separate development task.
