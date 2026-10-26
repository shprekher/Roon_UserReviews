# Public-release audit — v0.1.0

Prepared 2026-10-25; publication status updated 2026-10-26.
**Owner-authorized experimental source-only release, following a second audit.**
This is a source/security/provenance review, not legal advice, a penetration
test, or certification that software is free from vulnerabilities.

Documentation assessment revised 2026-10-26: symlink behavior is a low-priority
risk in the intended trusted private library, rather than an automatic release
blocker. The project decision is to retain current code and document the trust
boundary. This revises the earlier framing, not the implementation or test results.

## Scope and evidence

The supplied operational workspace was inventoried across original source,
scripts, bridge tools, protocol client/catalogues, research tools, dependencies,
configuration, data, compiled output, and backups. At initial inventory it held
99 private assembly files, 120 artifact files, 433 data files, and 1,683 backup
files, in addition to source and caches. These are **not public-release files**.

First-party code was inspected for filesystem and process operations, database
writes, network calls, fixed identities, and validation/failure behavior. Text
files up to 8 MiB outside tool/dependency/build caches were scanned for common
credentials. No matching private keys, GitHub/AWS keys, credential-bearing URLs,
or quoted secret assignments were detected in that scan. This is not proof that
arbitrary secrets do not exist, especially in excluded binary data and caches.
The final allowlisted tree/archive receive separate content checks.

An independent **Gitleaks 8.30.1** default-rule scan of the 47-file public source
snapshot and an 88-file private source/configuration/vendor snapshot reported
**zero findings**, with redacted reporting enabled. The official scanner archive's
SHA-256 was verified against upstream release checksums before use. The scanner
itself and its private reports are not bundled. Binary databases, SDK caches,
music/reviews and backups were excluded from that source-focused scan; their
exclusion from the release is enforced separately by the allowlist and archive checks.

There was **no Git repository or history** in the supplied project, and no nested
Git repository in the inspected source/vendor directories. There is no available
commit history to scrub or attest to. The release tree is prepared independently;
no operational history is imported. If the owner later imports an older branch,
remote, tag, or repository, every reachable history must be scanned anew.

## Distribution decisions

| Original area | Public-release treatment | Reason |
|---|---|---|
| Active runtime, IL patcher, handwritten tests | Included, reviewed | Implements current Architecture A |
| Installed/copied/patched Roon DLLs; portable SDKs | Excluded | Proprietary binaries / separate licenses |
| Data, review drafts, rewritten reviews, images, music | Excluded | Copyright, privacy, library disclosure |
| Configuration, broker identities, LAN addresses, logs, journals, backups | Excluded | Personal and operational information |
| Vendored internal protocol client, generated catalogues, captured fixtures | Excluded | Not needed at runtime; unverified license/provenance and private data surface |
| Node dependencies / lockfile | Excluded | Not required by active bridge; advisory findings |
| Historical serializer, LevelDB staging/writer, approval CLI, watcher, old deployment scripts | Excluded, originals preserved privately | Not needed; risk of database mutation and installation-specific behavior |
| Research tools and legacy one-album patch | Excluded | Not part of active functionality / provenance and personal constants |

The earlier ignore file omitted several sensitive areas. The operational
workspace now has defensive exclusions and a do-not-publish warning. The release
uses an explicit file allowlist, source-content checks, and deterministic archive
generation. `.gitignore` alone is not relied on to define a public distribution.

## Licensing and attribution

- Full canonical Apache License 2.0, NOTICE, source license mapping, contribution
  terms, and MIT attribution for Mono.Cecil are included.
- Mono.Cecil 0.11.6 license was checked against the upstream tagged license;
  package version/content hash are locked. No NuGet transitives were resolved.
- Roon API/member names are referenced for interoperability. No Roon binary,
  decompiled method body, generated full API catalogue, or proprietary runtime
  implementation is bundled. Private test doubles are handwritten synthetic code.
- The excluded internal protocol client declares MIT in package metadata but has
  no license text in the supplied checkout. Existing documentation's MIT claim
  is not treated as sufficient clearance to redistribute that vendor tree.
- Original-source ownership/authorization, Roon's current EULA/contractual terms,
  interoperability/reverse-engineering rules in the publisher's jurisdiction,
  and trademark presentation require **owner/legal review**. Apache-2.0 cannot
  grant rights to Roon, copied review content, or another author's code.

## Dependency advisory review

NuGet's vulnerability listing reported **no known advisories** for the resolved
Mono.Cecil 0.11.6 dependency at audit time. A clean advisory result does not prove
that metadata parsing is safe against malicious DLLs; use trusted local inputs.

The private vendored Node lockfile contained 357 distinct package names. The npm
advisory bulk endpoint plus exact installed-version range matching returned
**50 advisory/version matches across 11 packages**, including critical findings
for `handlebars` and findings in `ws`, `minimatch`, `brace-expansion`, `js-yaml`,
`flatted`, `picomatch`, `ajv`, `@babel/core`, `browserslist`, and
`baseline-browser-mapping`. These are not 50 independent reachable exploits;
many are development-only/transitive entries or repeated affected version ranges.
All are excluded from the public release. The private toolchain was not upgraded
because doing so would change an independently working installation.

The bundled private npm launcher was incomplete, so the audit used the npm
registry's HTTPS advisory API and the lockfile's installed versions directly,
not a successful `npm audit` invocation. See `dependency-audit.json` for the
sanitized machine-readable evidence. Rerun advisory checks before publication;
advisory data changes over time.

## Security findings and disposition

| ID | Severity / status | Finding and action |
|---|---|---|
| A01 | Critical distribution risk — mitigated in export | Proprietary DLLs, copyrighted review text, databases and personal records were adjacent to source. Allowlist excludes all of them. Never upload the parent workspace. |
| A02 | High — excluded | Historical tools could replace a stopped database / stage overlay writes. Not shipped. Public tooling has no DB path; original private files are not deleted. |
| A03 | Low in the intended trusted-library setup — documented; decision recorded | File opening follows symlinks by default; the bridge creates none and normal files work as intended. Control of library contents or imported linked content is required to redirect reads. Disclosure to Core clients can have greater impact in hostile/shared-writable libraries. Current behavior is retained; rejection/containment is optional hardening, not a standalone publication blocker for the declared trust model. |
| A04 | High availability risk — manual acceptance | Synchronous filesystem calls can block Core on network/special files. Byte bound is not a timeout. No untested runtime hardening was deployed during release preparation. |
| A05 | High — gated, residual | Unsupported IL changes may cause startup/type-load failures. Exact stock hash, structural lookup, unrelated-method checks, private integration tests and DLL-only rollback mitigate but do not eliminate it. |
| A06 | High — excluded | Vulnerable and incompletely attributed vendored Node/protocol tree is not shipped. Private use needs its own upgrade/provenance review. |
| A07 | Medium — documented | Overbroad common parents, symlinked path aliases, mixed track layouts and text-renderer behavior need trusted content and client validation. |
| A08 | Medium — mitigated / limited | Public installer defaults to dry-run, checks local macOS/stopped processes and hashes, backs up original bytes, and swaps atomically. Supervisor/concurrent-operation races and signature policy remain; public live installer test is pending. |
| A09 | Resolved for publication | GitHub private vulnerability reporting enabled and verified through the repository API on 2026-10-26; SECURITY.md links to its private reporting flow. |
| A10 | Medium — publication gate | No original Git history exists; future import or first staging must be checked for ignored/forced-added private files and independently scanned. |

## Functionality and verification

- The public runtime source is byte-for-byte the previously working v5.2 source.
- A private local rebuild with the public patcher produced the **exact deployed
  patched DLL SHA-256** `2a35d831b24810e9a61b996513e6f5ec1323de59c000d174d5161a555817ffda`.
  That binary remains private and outside the source archive.
- The patcher verified **46,841 unrelated method bodies** unchanged.
- **50 public synthetic bridge assertions** and **50 private transplanted-bridge
  assertions** passed. They cover source order, attribution, explicit selection,
  callback safety, strict UTF-8, bounded files, edits/deletion/restoration,
  multi-disc ambiguity, stock-on-failure behavior, and zero overlay reads.
- **20 Python safety tests** cover dry-run, tampering, running/restarted processes,
  symlink/non-app targets, DLL-only restoration, unknown builds, patcher rejection,
  and release-content checks. See the release checklist for final results.
- Prior live acceptance verified a no-overlay album across eight file lifecycle
  checks and all 103 existing review files byte-exact on v5.1. v5.2 changes only
  the label; its pilots and source registry passed native API checks. These are
  historical private acceptance results, **not a public CI/live installer test**.
- The Core OS version was read from its system version plist: macOS **26.5.2**,
  build **25F84**. Roon supported build is **2.74 / 1712** only.
- No live restart, database write, music/review change, push, remote creation,
  or publication was performed for this release-preparation task.

## Outstanding manual review

See [RELEASE-CHECKLIST.md](RELEASE-CHECKLIST.md). The owner authorized source-only
publication after a second audit. This does not establish legal clearance or
complete outstanding installer/client validation. A packaged installer is deferred
to a separate stage. The symlink trust-model decision is recorded; code is unchanged.

The second audit verified the 47-file source tree, staged index and current archive
were byte-identical, found no personal paths or local-only service references,
and passed 50 bridge assertions and 20 Python tests from a clean source export.
The archive's Gitleaks rescan reported zero findings. Generic macOS application
paths are examples, not dependencies on the developer's machine. Tool checks
conservatively reject all `/Volumes/` paths, including local external disks.

The destination repository had an initial placeholder README and Apache-2.0
license commit; publication preserves that history rather than replacing it.
The earlier no-history observation refers to the supplied operational workspace.
