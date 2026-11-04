# Dressmaker Critic 0.7.0

The plugin now has one capture/evaluation path. Temporary facts UI, readable
reports and per-feature buttons have been removed. Existing captured data remains
for regression tests; it is not rewritten.

Use the root [installation/workflow](../README.md) and
[audit/blockers/configuration](../docs/AUDIT.md). `build.sh` compiles only;
`build_and_install.sh` also copies the plugin into the game. Both require an
actual authorized game directory and the Proton-compatible ModLoader.

The reusable core consists of read-only state extraction, normalized garment/order
facts, sourced English brief lookup, verified image references, bounded local
multimodal requests and strict assessment validation. No gameplay methods are
invoked to mutate progress, garments or orders. The temporary offscreen camera
is owned and destroyed by the mod; original cameras are not changed.

One command/button captures the complete machine-readable diagnostic transaction.
No inference is sent for an unpaired historical viewer image or unconfigured model.
Native integration still needs game verification; no game DLLs are shipped.
