# Roon File Reviews

**Your album-folder text, in Roon's native Review panel.**

Version **0.1.0** · Apache-2.0 · experimental, source-only · macOS Roon Server

**Source release for advanced users:** compilation is currently required. No
ready-to-run installer or patched Roon binaries are provided. A downloadable
installer is planned as a separate follow-up, after packaging and live
installation/rollback testing; no release date is promised.

Repository: <https://github.com/shprekher/Roon_UserReviews>

Place your own `user_review.txt` beside an album's tracks. The patched server
reads it on each review request and offers **User review.** under **Read more**.
Online reviews remain the default; deliberate local-source selection is honored.

> **Unofficial and unsupported.** This modifies a proprietary Roon assembly
> using undocumented internals, not Roon's supported extension API. An incorrect
> patch can prevent Roon from starting. Read [SECURITY.md](SECURITY.md) and
> [the audit](docs/AUDIT.md), make an official Roon backup, and retain the original
> DLL before proceeding. No association with or endorsement by Roon is claimed.

## What it does

- Resolves album folders from Roon's own local track data, not guessed titles.
- Reads the exact filename `user_review.txt`, including a common album parent
  for multi-disc releases. Multiple candidate files are treated as ambiguous.
- Preserves Unicode, paragraphs, whitespace, and line endings; accepts UTF-8
  with an optional encoding BOM, up to **256 KiB**.
- Leaves stock behavior unchanged when a file is absent, blank, invalid,
  unreadable, or ambiguous. No stale database fallback is used.
- Runs inside the Core: **no registration, registry, watcher, background helper,
  per-album deployment, or development-Mac connection**.
- Does not write album overlays, edit tags, modify FLAC/JPG files, or access
  LevelDB. Roon itself continues its normal database activity.

## Compatibility

| Component | Verified / limitation |
|---|---|
| Roon Server | **2.74, build 1712**, exact original DLL hash below |
| Core operating system | **macOS 26.5.2, build 25F84** |
| Clients | Native protocol and existing iPad pilot display verified; latest label visual confirmation pending |
| Windows client → Mac Core | Expected, not independently tested |
| Windows / Linux Core, Roon OS / ROCK / Nucleus | **Not supported** |
| Development | .NET SDK **10.0.402**; Python **3.9+** standard library |

Only one Core build and operating-system installation have been live-tested.
Synthetic CI tests are not evidence of compatibility with another Roon release.
The patcher refuses any original `Roon.Broker.Core.dll` whose SHA-256 is not:

```text
a61a72bc683db159a13fd08a14921f781cf7c2685a7c9ec193bcae075db2612d
```

## Installation (advanced users)

Run locally on the **Core Mac**, not against SMB-mounted application files.
No Roon DLL, patched DLL, database, music, or review content is distributed here.
Supply assemblies from your own lawfully installed, matching Roon installation.

1. Review Roon's current terms and the audit's manual-review items. Install the
   .NET 10.0.402 SDK and Python 3.9+ from their official distributors. Verify their
   downloads according to those distributors' instructions.
2. Make an official Roon database backup. Fully **quit Roon and its server**;
   leave it stopped throughout copying, building, and installation. Record the
   installed Roon version and keep a separate copy of the original application.
3. Locate the server's `MonoBundle` directory. For a combined Roon application,
   it is commonly `/Applications/Roon.app/Contents/RoonServer.app/Contents/MonoBundle`.
   Standalone server layouts can differ; verify your installation, do not guess.
4. Copy the matching `MonoBundle` DLLs to the ignored `private/assemblies/`
   directory. Keep these inputs private and never attach them to an issue.
5. From this repository, build and test **offline candidates**:

   ```sh
   dotnet restore bridge/Patcher/Patcher.csproj --locked-mode
   dotnet run --project bridge/Tests/Tests.csproj
   dotnet build bridge/Runtime/Runtime.csproj
   dotnet run --project bridge/Patcher/Patcher.csproj -- \
     private/assemblies/Roon.Broker.Core.dll \
     bridge/Runtime/bin/Debug/net10.0/RoonReviews.Runtime.dll \
     artifacts/Roon.Broker.Core.patched.dll
   dotnet run --project bridge/IntegrationTests/IntegrationTests.csproj -- \
     artifacts/Roon.Broker.Core.patched.dll private/assemblies
   ```

   Existing outputs are never overwritten. The report next to the candidate
   records its hash and verifies unrelated method bodies are unchanged. Keep
   both files private: the candidate contains proprietary Roon code.
6. Set `TARGET` to the exact installed broker DLL you located. First dry-run:

   ```sh
   TARGET='/Applications/Roon.app/Contents/RoonServer.app/Contents/MonoBundle/Roon.Broker.Core.dll'
   python3 scripts/assembly.py install --target "$TARGET" \
     --candidate artifacts/Roon.Broker.Core.patched.dll \
     --report artifacts/Roon.Broker.Core.patched.dll.json \
     --backup backups/first-install
   ```

   After reviewing the result, repeat with `--apply --acknowledge-unsupported`.
   The tool checks local macOS execution, stopped processes, the stock hash,
   a verified original-DLL backup, and candidate integrity before an atomic swap.
   It never stops/restarts Roon or edits its database. **The public local installer
   has synthetic tests but has not been exercised against a live Roon app.**
   Permission/signing failures require investigation; do not disable Gatekeeper,
   remove quarantine, force-sign the application, or bypass macOS protections.
7. Start Roon normally. Open a test album and check the default online source,
   the **User review.** entry, and explicit local selection. If startup or
   playback fails, stop Roon and follow [rollback](docs/INSTALLATION.md).

## Everyday use

```text
Music/
└── Example Artist — Example Album/
    ├── 01 - Example Track.flac
    ├── cover.jpg
    └── user_review.txt       ← your own UTF-8 review
```

For multi-disc albums, use one file in the common album directory, **not** one
in every disc folder. Reopen the album/review screen after editing. New albums
work once Roon has imported their tracks. No command, scan, approval, restart,
or database write by this project is needed for routine file changes.

Write or supply text you have permission to use. This repository deliberately
contains no album reviews, artwork, screenshots, or music-library exports.

## Limitations and security

- Use a **trusted local music library**. Ordinary review files work as intended;
  the reader also follows symlinks by default, although the project neither
  creates nor requires them. This is a low-priority risk in a trusted private
  library, not a standalone remote-entry vulnerability. Link rejection is optional
  future hardening, not a publication prerequisite for this trust model. See
  [SECURITY.md](SECURITY.md) for shared/untrusted-library implications.
- Separately, network filesystem stalls and special files can block Roon's library
  thread. Size limits do not impose an I/O timeout; the symlink decision does not
  change this storage-availability limitation.
- The local review is visible to clients that can access the Core. Do not put
  secrets or private notes in review files. Treat clients/library writers as trusted.
- Local tracks are required; streaming-only albums and mixed/ambiguous layouts
  can retain stock behavior. Only the exact lowercase filename is recognized.
- Automatic Roon updates may remove the patch or make it incompatible. Never
  put an old patched DLL over a newer build. Stop and wait for a verified adapter.
- No public end-to-end CI runs proprietary Roon. UI caching may require reopening
  the album or reconnecting a client; the project does not control client caches.
- This is not a sandbox, a supported plug-in, or a guarantee against data loss.

## Development and release

```sh
dotnet run --project bridge/Tests/Tests.csproj
dotnet restore bridge/Patcher/Patcher.csproj --locked-mode
dotnet build --no-restore bridge/Patcher/Patcher.csproj
python3 -m unittest discover -s tests -v
python3 scripts/release_check.py
python3 scripts/package_release.py --output dist/roon-file-reviews-0.1.0-source.tar.gz
```

The runtime source matches the privately deployed v5.2 implementation. The public
test doubles contain no Roon implementation; integration tests need local private
assemblies. See [architecture](docs/ARCHITECTURE.md), [contributing](CONTRIBUTING.md),
[dependencies](THIRD_PARTY_NOTICES.md), and [release checklist](docs/RELEASE-CHECKLIST.md).

## License

Original project source is offered under [Apache License 2.0](LICENSE).
Mono.Cecil remains MIT-licensed. Apache-2.0 grants **no rights to Roon's software,
trademarks, third-party reviews, or music**. See [NOTICE](NOTICE).
