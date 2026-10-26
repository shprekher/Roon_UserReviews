# Security policy and threat model

## Status and reporting

This is experimental unsupported software, **not security-certified**. Only
v0.1.x is a candidate for best-effort fixes; there is no guaranteed response time.
GitHub private vulnerability reporting is enabled for this repository. Use
**Security → Report a vulnerability**, or open
<https://github.com/shprekher/Roon_UserReviews/security/advisories/new>.
Do not publish exploitable details or private logs in an issue. Include a minimal
synthetic reproducer, affected version/hash, impact, and OS version. Never send
Roon binaries, patched DLLs, passwords, review prose, database copies, listening
history, broker identities, account details, or local paths identifying people.

## Trust boundary

The bridge executes in the Roon Core process with its filesystem privileges.
It reads local album-folder text and serves it to Core clients. It does not add
an authentication boundary, encrypt existing Roon traffic, or sandbox file reads.
The music library and every client able to access Core must be trusted. Anyone
able to write a review file can change what clients read. Files must not contain
secrets; limit music-folder write permissions accordingly.

### Known runtime limitations

- **Symlink behavior — low risk in the intended trusted-library setup:** the
  bridge does not create or require symlinks; its ordinary file-open operation
  follows them by default. Normal review files work as intended. Someone who
  already controls library contents, or imported content containing an unexpected
  link, could make `user_review.txt` point to readable UTF-8 content elsewhere
  and expose it to Core clients. This is not a standalone remote-entry
  vulnerability and grants no extra filesystem permissions beyond the Core's
  account. The impact can be greater in untrusted/shared-writable libraries.
  Lexical `..` rejection is not physical containment. Trust library writers and
  inspect unfamiliar linked content; protection against hostile library content
  is not claimed.
- **I/O availability:** synchronous reads execute on Roon's library thread.
  A stale mount, slow storage, FIFO, or special file can block the thread even
  though allocation/read length is bounded. No I/O timeout is enforced.
- **Folder scope:** a common parent spanning unusually merged albums could be
  broader than the intended album directory; storage-root comparison is lexical.
  Avoid such layouts and check source selection manually.
- **Rendering:** text is passed verbatim to Roon's own renderer. The project does
  not audit or sanitize that renderer. Supply trusted prose, not active markup.
- **Races:** file replacement during a read can yield a changing snapshot. The
  reader does not implement filesystem transactions or a stable-content hash.
- **Fail-open-to-stock is not crash containment:** catch blocks retain ordinary
  stock behavior for helper failures; they cannot guarantee recovery from CLR
  type-load faults, process failure, resource exhaustion, or blocking syscalls.

### Project decision — 2026-10-26

Retain the current runtime behavior and document its trusted-library assumption.
Symlink rejection/containment would be optional defense-in-depth work, not a
required fix or a standalone publication blocker for that intended setup. No
code changes are requested on this basis. This decision does not dismiss the
separate storage-availability limitations or resolve other release-review items.

## Installation and database safety

The patcher transforms only a local copy of one exact stock assembly. It changes
two method bodies, embeds the original project helper, and compares unrelated
method-body fingerprints. It does not prove semantic correctness of all Roon code.
Treat input/helper DLLs and generated reports as trusted local artifacts; hashes
establish integrity, **not publisher identity or a signature**.

The optional installer is local-only and dry-run by default. Applying/restoring
requires acknowledgement, stopped-process checks, expected hashes, verified DLL
backup, and an atomic same-directory replacement. It neither terminates Roon
nor disables a supervisor. A process can restart between checks; fully quit all
Roon components and ensure automatic launch is stopped. Do not run concurrent
installers. Interrupted operations may need the saved original DLL restored.

**No public-release tool opens or writes Roon's database.** Historical private
tools did stage/write overlays and replace a stopped database. Those tools,
their LevelDB dependency, serializer, and review-registration pipeline are excluded.
Existing historical overlays remain untouched; never restore an old whole database
merely to uninstall this bridge. Roon itself still writes its normal database,
so this project cannot promise immunity from corruption or data loss. Make an
official Roon backup and a separate original-application backup before installing.

Patching can invalidate application signing and conflict with licensing or vendor
support. Do not bypass Gatekeeper, quarantine, permissions, or signature checks.
Do not expose internal Core ports or protocol relays to untrusted networks.

## Supply chain and publication

No proprietary assemblies or generated patched output may be committed, cached
in public CI, or attached to releases. CI uses synthetic tests, read-only token
permissions, pinned action commits, and no publishing job. Lock Mono.Cecil with
NuGet content hashes; rerun advisory scans before publication and updates.

The included secret/content checker is defense in depth, not a proof that no
secrets exist. Review the exact archive and Git index/history with an independent
secret scanner before approval. Never publish the operational parent workspace.
