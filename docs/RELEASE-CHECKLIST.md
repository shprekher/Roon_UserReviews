# v0.1.0 publication checklist

**Status: owner-authorized experimental source-only publication, 2026-10-26.**
Repository: <https://github.com/shprekher/Roon_UserReviews>
Compilation is required. A downloadable installer is deferred to a separate stage;
publication does not mark unfinished validation or legal review as completed.

## Prepared / automated

- [x] Separate source-only tree; private installation and backups preserved.
- [x] Version 0.1.0, full Apache-2.0 license, NOTICE, attribution and contribution files.
- [x] Explicit unofficial/internal-API, database, signing and recovery warnings.
- [x] Tested-version table; unsupported Windows/Linux Core stated honestly.
- [x] Active runtime retained exactly; offline rebuild matches deployed v5.2 hash.
- [x] 50 public bridge assertions and 50 private integration assertions pass.
- [x] Public installer safety tests; no database tools included.
- [x] NuGet lock files, dependency inventory and advisory review.
- [x] Pinned, read-only CI with no publish/upload/proprietary-input steps.
- [x] Allowlisted source packager; source/archive content and integrity checks.
- [x] Gitleaks 8.30.1 source/config scans: zero findings; source-focused scope and
      limitations documented in the audit. Final source archive must be rescanned
      after any later edit.
- [x] 20 Python safety/content tests pass locally; no live installation was altered.
- [x] Symlink behavior documented proportionately. Decision recorded 2026-10-26:
      retain the current code for the trusted-library model; optional link
      rejection is not required before publication on this basis.

## Owner review and remaining validation

- [ ] Confirm authority to license all original source under Apache-2.0; review
      the generic contributor copyright notice and identify the publishing owner.
- [ ] Review Roon's current EULA, anti-circumvention/interoperability constraints,
      support/signing implications and trademark use; obtain legal advice if needed.
- [ ] Review the separate storage-availability/special-file and blocking-I/O
      limitations. These are not resolved by the documented symlink decision.
- [x] Owner requested two audits and authorized source-only publication afterward.
- [x] Repository owner/name configured; GitHub private vulnerability reporting
      enabled and verified through the repository API. See SECURITY.md.
- [ ] Independently validate the new local installer/rollback on a disposable,
      lawfully licensed matching Roon installation. It has not been live-tested.
- [ ] Confirm current **User review.** client display and source selection;
      Windows-client behavior and other Core OS/builds are not validated.
- [x] Publication recheck: 47 allowlisted files, matching staged content, 50 bridge
      assertions and 20 Python tests pass; no known Mono.Cecil advisory reported.
      Gitleaks source and destination-history scans report zero findings. Future
      commits/tags and release assets must continue to pass the same checks.
- [x] Use publisher `shprekher` with GitHub's account-specific no-reply address,
      rather than the locally configured personal email. Preserve the destination's
      existing placeholder README/license commit.
- [ ] Run the included CI on the future repository; local checks are not a claim
      that GitHub Actions has already executed.
- [x] Initial source checksum/tag are unsigned and labeled accordingly. SHA-256
      establishes integrity, not cryptographic publisher authentication.
- [x] Explicit approval received for GitHub source publication and release;
      do not upload Roon DLLs, reviews, private data, or an untested packaged installer.

## Recheck commands (local only)

```sh
dotnet restore bridge/Patcher/Patcher.csproj --locked-mode
dotnet build --no-restore bridge/Patcher/Patcher.csproj
dotnet run --project bridge/Tests/Tests.csproj
python3 -m unittest discover -s tests -v
dotnet list bridge/Patcher/Patcher.csproj package --vulnerable --include-transitive
python3 scripts/release_check.py
python3 scripts/package_release.py --output dist/roon-file-reviews-0.1.0-source.tar.gz
python3 scripts/release_check.py --archive dist/roon-file-reviews-0.1.0-source.tar.gz
```

The archive/checksum are unsigned and local. If an output already exists, use a
new reviewed output path; the packager does not overwrite an earlier candidate.
