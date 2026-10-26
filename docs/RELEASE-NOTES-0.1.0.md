# Roon File Reviews 0.1.0

**Experimental source-only release — 2026-10-26. Compilation required.**

Repository: <https://github.com/shprekher/Roon_UserReviews>
No downloadable installer or patched Roon DLL is provided. A packaged installer
will be a separate future update, after installation and rollback testing.

Reads an album-folder `user_review.txt` directly inside a version-checked patched
Roon Server, exposing a native **User review.** source while online reviews stay
first. New albums and file lifecycle changes need no registration, watcher, or
project database writes. The runtime is the existing verified v5.2 implementation.

Supported live-tested target: Roon 2.74 build 1712 on macOS 26.5.2 (25F84).
Windows/Linux Core and other Roon builds are not supported. Public CI uses only
synthetic test contracts; the optional public installer is not live-tested.

This archive contains original source, licensing/attribution, documentation,
tests, pinned CI, package locks, and a build-dependency inventory. It contains
no Roon binaries, generated patched binaries, reviews, music/artwork, databases,
local configuration, captured protocol data, or historical database writers.

Review SECURITY.md, docs/AUDIT.md, and the manual publication checklist first.
The trust model requires trusted library writers and clients. Symlinks are not
created or required, but standard file opening follows them; this is documented
as a low-priority risk in that setup, with code retained unchanged by the
2026-10-26 project decision. Link rejection is optional future hardening, not an
automatic publication blocker. Blocking storage remains a separate limitation.
Apache-2.0 grants no rights to proprietary Roon code and does not override its terms.

The source archive checksum and release tag are unsigned integrity evidence,
not cryptographic publisher authentication. Publication is not a claim that the
remaining manual checks or the public installer's live tests have been completed.
