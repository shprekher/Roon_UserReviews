# Roon File Reviews

**Your own album notes, right where Roon shows its reviews.**

Drop a `user_review.txt` into an album's folder and Roon offers it as **User review.** When Roon has no online review for the album, your local review is shown instead. If an online review is available, it stays the default, and you can choose **User review.** under *Read more*.

Edit the file, reopen the album, and the new text shows up.

Version **0.1.0** · Apache-2.0 · macOS only · experimental

Repository: [https://github.com/shprekher/Roon\_UserReviews](https://github.com/shprekher/Roon_UserReviews)

> **Read this first.** This is an unofficial experiment. Roon Labs didn't make it. It works by editing one of Roon's own program files. Get it wrong and Roon may not start. It currently supports **one exact Roon build** (2.74, build 1712, on macOS), and a Roon update can break it without warning.

> At the moment it's **source-only**: you compile it yourself, and there's no one-click installer. A packaged installer is planned but not built or tested yet. Take this on only if you're comfortable in a terminal, will make a backup first, and accept that it might not work on your machine. You can ask Codex, Claude Code or OpenCode to help follow these build instructions on a supported Mac.

> The review reader runs inside your Core and makes no network requests of its own. See [SECURITY.md](SECURITY.md) for the trusted-library assumptions and storage limitations.

## What it does

- Finds an album's folder from Roon's own track data, not by guessing from the title.

- Reads the file named exactly `user_review.txt` in that folder, or in a shared parent folder for multi-disc albums.

- Passes your text to Roon's review panel without rewriting it: Unicode, paragraphs, spacing and line breaks are preserved. Roon controls how the text is displayed. It accepts UTF-8 (with or without a BOM), up to **256 KiB**.

- Keeps online reviews first and adds yours as a separate, selectable source. If you deliberately pick your local review, that choice is respected.

- Leaves Roon behaving exactly as normal when the file is missing, blank, invalid, unreadable, or ambiguous (more than one candidate). It never falls back to some stale saved copy.

- Runs inside the Core, so there's no helper app, no watcher, no per-album setup, and no need to stay connected to another computer.

## What it leaves alone

- It does not write to Roon's database.

- It does not touch your music files, tags, or cover art.

- It does not create album overlays or open LevelDB.

- Roon keeps doing its normal database work in the background; this simply adds nothing to it.

The only thing you manage is a plain text file in your album folder.

## Will it work on my setup?

One Core build and one macOS install have actually been tested.

| Component | Status |
| --- | --- |
| Roon Server | **2.74, build 1712** only (exact file hash below) |
| Core operating system | **macOS 26.5.2, build 25F84** (the one install tested) |
| Clients | Native protocol checks passed; iPad and Android phone tested |
| Windows client → Mac Core | Expected to work, not tested |
| Windows / Linux Core, Roon OS / ROCK / Nucleus | **Not supported** |
| Build tools | .NET SDK **10.0.402**; Python **3.9+** (standard library only) |


The patcher refuses to touch any `Roon.Broker.Core.dll` whose SHA-256 isn't exactly:

```
a61a72bc683db159a13fd08a14921f781cf7c2685a7c9ec193bcae075db2612d
```

If your Roon is a different build, the patcher refuses to touch it. That refusal is deliberate as other builds are not tested.

## Installation (advanced)

This part is for people who are happy building from source on the machine that runs their Core. Nothing here ships a Roon file, a patched file, a database, music, or review text. You supply the Roon assemblies from your own lawfully installed copy.

Work directly on the **Core Mac**, on local storage. Don't patch an app sitting on a network (SMB) share.

1. Install the .NET 10.0.402 SDK and Python 3.9+.

2. Make an official Roon database backup, and keep a separate copy of the whole Roon application. **Quit Roon and its server completely**, and leave them stopped through the copy, build, and install steps. Note your Roon version.

3. Find the server's `MonoBundle` folder. In a combined Roon app it's usually `/Applications/Roon.app/Contents/RoonServer.app/Contents/MonoBundle`. A standalone server may lay things out differently, so check yours rather than guessing.

4. Copy the matching `MonoBundle` DLLs into the ignored `private/assemblies/` folder. Keep those private; never attach them to a bug report.

5. From this repository, build and test a local patch candidate. Dependency restoration needs internet access:

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

   It never overwrites an existing output. The report written next to the candidate records its hash and verifies that unrelated method bodies were left unchanged. Keep both files private. The candidate contains Roon's proprietary code.

6. Point `TARGET` at the exact installed broker DLL you found, and do a dry run first:

   ```sh
   TARGET='/Applications/Roon.app/Contents/RoonServer.app/Contents/MonoBundle/Roon.Broker.Core.dll'
   python3 scripts/assembly.py install --target "$TARGET" \
     --candidate artifacts/Roon.Broker.Core.patched.dll \
     --report artifacts/Roon.Broker.Core.patched.dll.json \
     --backup backups/first-install
   ```

   Look at the result, then repeat with `--apply --acknowledge-unsupported`. Before applying the patch, the tool checks that you're on local macOS, that Roon is stopped, that the file is the exact stock build, and that the backup and candidate are intact. It never starts or stops Roon itself and never edits the database. **This installer has passed synthetic tests, but it has not yet been run against a real Roon install.** If macOS blocks it on permissions or signing, stop and investigate. Don't disable Gatekeeper, strip quarantine, force-sign the app, or route around any protection.

7. Start Roon normally. Open a test album and check that the online review is still the default, that **User review.** appears as an option, and that picking it shows your text. If Roon won't start or playback breaks, quit it and follow the [rollback steps](docs/INSTALLATION.md).

## Everyday use

```
Music/
└── Example Artist — Example Album/
    ├── 01 - Example Track.flac
    ├── cover.jpg
    └── user_review.txt       ← your own UTF-8 review
```

For a multi-disc album, put **one** file in the shared album folder, not one in each disc folder. After editing, reopen the album or review screen. A new album works as soon as Roon has imported its tracks, with no command, scan, approval, restart, or database write needed.

Only use text you have the right to use. On purpose, this repository contains no album reviews, artwork, screenshots, or library exports.

## Good to know

- **It needs local tracks.** Streaming-only albums and mixed or ambiguous layouts may just keep Roon's normal behavior. Only the exact lowercase filename `user_review.txt` is recognized.

- **Roon updates can remove or break the patch.** Never put an old patched file over a newer Roon build. Stop and wait for a version that's been checked against it.

- This isn't a sandbox, a supported plug-in, or a promise against data loss.

## Credits and license

Original author and maintainer: Alexander Shprekher ([@shprekher](https://github.com/shprekher)).

Copyright 2026 Alexander Shprekher and Roon File Reviews contributors.

The original source is offered under the [Apache License 2.0](LICENSE). The IL patcher builds on [Mono.Cecil](https://github.com/jbevain/cecil) by Jb Evain, which stays MIT-licensed.
