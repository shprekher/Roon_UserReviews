# Contributing

Thanks for helping improve a deliberately small, experimental integration.
Discuss compatibility changes before implementing them. Do not submit proprietary
DLLs, decompiled Roon implementation, generated full API catalogues, music,
copyrighted reviews, real library identities, configuration, or unredacted logs.

## Checks

Use .NET SDK 10.0.402 and Python 3.9+. Run the development checks in
README. Public tests must work with synthetic fixtures and without a running
Core, credentials, network share, or proprietary assembly. Private integration
tests may use lawfully obtained local assemblies but must never upload them.

Keep online-first behavior, explicit source selection, verbatim file text,
stock-on-failure semantics, callback-once behavior, and zero project database
writes. Do not introduce per-album registration or a background watcher.

Use tests for fixes. Never weaken the stock-DLL hash gate to "support" another
build; a new adapter needs structural verification, offline tests, reversible
stopped-Core installation, and live/client acceptance on that build.

## Rights and review

By intentionally submitting a contribution for inclusion, you offer it under
Apache-2.0 as described in that license, unless otherwise agreed in writing.
You must have the right to submit it. Retain third-party notices and identify
third-party code and modifications. Maintainers should confirm contributor
identity/authorization without publishing personal information unnecessarily.

Keep commits focused. Do not add installation-specific identifiers or files
outside `release-manifest.txt`. Changes to that allowlist require content,
license, secret, and archive review. Do not attach patched DLLs to releases.

Be respectful, factual, and patient. Technical disagreement is welcome;
harassment and disclosure of another person's private information are not.
For security concerns, follow SECURITY.md rather than a public issue.
