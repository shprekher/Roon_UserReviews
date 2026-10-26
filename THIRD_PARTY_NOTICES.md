# Third-party components and attribution

| Component | Version | Use | License | Distribution |
|---|---|---|---|---|
| Mono.Cecil | 0.11.6, exact NuGet version + lock hash | Offline IL patcher | MIT | Restored by developer; not embedded in Core |
| .NET SDK/runtime | SDK 10.0.402; target net10.0 | Build, tests, Roon's existing runtime | Microsoft .NET components carry MIT and other notices | Installed separately; not bundled |
| Python | 3.9+ | Optional local DLL tool, tests, source packaging | PSF-2.0 and bundled component licenses | Installed separately; not bundled |
| Roon assemblies | Exact supported installation | Local compilation and private integration | Proprietary, not Apache-2.0 | **Not distributed** |

Mono.Cecil upstream: <https://github.com/jbevain/cecil/tree/0.11.6>.
License verified against upstream tag `0.11.6/LICENSE.txt`; retained verbatim in
`LICENSES/Mono.Cecil-MIT.txt`. The dependency has no NuGet transitive dependencies
in the resolved net10.0 build. NuGet lock files retain package content hashes.

The original private workspace also used an internal binary-protocol client,
Node packages, a LevelDB writer, and review-research helpers. None is needed by
the active bridge, and **none is included or required by this source release**.
The internal client's package metadata says MIT but its license/author provenance
was not established from the supplied checkout. Do not vendor it without review.

CI actions are fetched by immutable commit SHA, not copied into this repository:
`actions/checkout` (MIT), `actions/setup-dotnet` (MIT), and
`actions/setup-python` (MIT). Review updates before changing their pins.

The handwritten test contracts model a minimal interface surface only. They
are not generated protocol catalogues, decompiled implementation, or replacement
Roon software. The runtime and patcher refer to private type/member names for
compatibility; these identifiers do not confer rights to proprietary binaries.

The owner must confirm the provenance and licensing authority for all original
source before first publication. This inventory is not legal advice or an
independent determination of Roon's contractual terms.
