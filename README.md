# Ableton ALS 12 → 11 — Blazor WebAssembly

Minimal standalone Blazor WebAssembly app that converts an Ableton Live 12 `.als` file into the Live 11 structure entirely in the browser.

No backend is involved. The selected file remains in browser memory.

## Conversion rule

The converter intentionally mirrors the published `live_set` 0.2.3 downgrade algorithm:

1. Decompress `.als` as gzip.
2. Parse the contained XML.
3. Require `MinorVersion` beginning with `12.`.
4. Change root metadata to Live 11.3.21:
   - `Creator="Ableton Live 11.3.21"`
   - `MajorVersion="5"`
   - `MinorVersion="11.0_11300"`
   - `SchemaChangeCount="3"`
   - `Revision="5ac24cad7c51ea0671d49e6b4885371f15b57c1e"`
5. Remove XML elements named:
   - `ContentLanes`
   - `ExpressionLanes`
   - `InstrumentMeld`
   - `Roar`
   - `MxPatchRef`
6. Replace `AudioOut/Main` with `AudioOut/Master`.
7. Compress the resulting XML back to gzip and download it as `*_11.als`.

Reference implementation/documentation:
- https://www.mslinn.com/av_studio/live_set.html
- https://rubydoc.info/gems/live_set/LiveSet%3Amodify_als

## Run

Requires .NET 10 SDK.

```bash
dotnet restore
dotnet run
```

Everything after the static app has loaded runs locally in the browser.

## Publish as static site

```bash
dotnet publish -c Release
```

Deploy the generated `wwwroot` output to any static web host.

## Limits

This is a structural downgrade, not a compatibility layer. Live 12-only devices/features removed by the rule are lost. Plugin/device compatibility still depends on what Live 11 can actually load.

The UI accepts `.als` files up to 100 MB and processes them in memory.
