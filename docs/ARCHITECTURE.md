# Architecture A

```text
Roon Core on macOS
└── locally patched Roon.Broker.Core.dll
    └── native album review request
        ├── resolve album → track IDs → local track folders
        ├── read one unambiguous user_review.txt
        ├── validate UTF-8 and bounded size
        └── wrap native response: online sources first, local source selectable
```

`RuntimeBridge.Prepare` runs on Roon's existing library thread before the cloud
request. It reads `GetTrackIdsByAlbumId`, `_GenerateTrack`, `Local.UserVisiblePath`,
and, if needed, `Local.ClumpingPath` plus `StorageManager.GetPathPrefix`.
All distinct track folders are checked, plus the common multi-disc parent unless
it is a known shared storage root. No album overlay or stored review is read.

`ReadReview` reads at most 256 KiB + 1 byte and decodes strict UTF-8. A UTF-8 BOM
is an encoding marker, not prose. Blank text, UTF-16, invalid UTF-8, forbidden
control characters, and oversized content are rejected. Missing/invalid/ambiguous
inputs leave the original callback untouched. Ordinary exceptions are caught,
but blocking I/O and CLR failures are residual risks documented in SECURITY.md.

`Delivery` merges only a successful provider response or `NotFound`; other errors
keep their existing semantics. Provider objects, images, attribution, URLs, and
nonblank review order are preserved. A deliberate local preference is honored;
the private source ID is stripped before the cloud request in every case.
Callback invocation is outside the merge catch to avoid calling a client twice.

`RegisterSource` advertises source ID `RoonUserReview`, English, display label
**User review.** This creates no database registration and no per-album state.

## Build-time transformation

The patcher accepts a single pinned original assembly hash. It transplants the
small original helper into that assembly and inserts calls in two known methods.
It verifies unrelated method-body fingerprints before emitting a new offline
file and integrity report. No proprietary implementation is distributed here;
the user's resulting patched DLL remains private.

The original runtime source is retained byte-for-byte. A local release rebuild
produced exactly the same patched DLL hash as the previously deployed v5.2:

```text
2a35d831b24810e9a61b996513e6f5ec1323de59c000d174d5161a555817ffda
```

Build flags, toolchain, or references can affect output; this observation is not
a promise of identical output in every environment. The version `0.1.0` is the
public project version, not a Roon version or a new runtime behavior version.

## Excluded historical systems

Earlier private experiments used review overlays, an approval catalogue, a
registration watcher, protocol relay, and a serializer/LevelDB writer. They are
not part of this distribution. Do not reintroduce those dependencies to support
new albums: folder files are the only state of the active architecture.
