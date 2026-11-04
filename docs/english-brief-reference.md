# Bundled English brief reference — v0.6.1

The plugin embeds Reference/brief-reference.en.json inside its DLL. Installation
still copies one plugin DLL; no network requests or separate data installation
are needed at runtime. The .en.json resource has WithCulture=false so it stays in
the main DLL rather than an uninstalled satellite assembly.

## Actual coverage

Five manually researched entries for game 0.7.0:

| Internal asset key | English title | Client | Runtime GUID mapping |
| --- | --- | --- | --- |
| PrudenceModest | Undercover Servant | Prudence | f205a54432cf7a4bda9bf1ab59049a6d verified in real exports |
| RoseWork | Librarian Workwear | Rose | not yet verified |
| PrudenceFuneral | Flamboyant Funeral | Prudence | not yet verified |
| PriyaFuneral | Fierce Funeral | Priya | not yet verified |
| DorothyBerty | Pigeon Adoption | Dorothy | not yet verified |

The English prose is original paraphrase, not a copied dialogue transcript. Each
entry contains summary, requests, prohibitions, uncertainties, source URL,
review date and explicit excerpt-only coverage. Requirement-derived colour limits
are labelled as deterministic constraints rather than inferred visual rules.
No fabric/pattern recommendations, planner recipes or complete source dataset
are bundled. Unknown quests have no invented descriptions. This is **not** a
complete 78-quest reference or a complete narrative corpus.

## Sources and handling

English pages consulted on 2026-11-04:

- https://dressmakerwiki.com/commissions/undercover-servant
- https://dressmakerwiki.com/commissions/librarian-workwear
- https://dressmakerwiki.com/commissions/flamboyant-funeral
- https://dressmakerwiki.com/commissions/fierce-funeral
- https://dressmakerwiki.com/commissions/pigeon-adoption

The public pages identify game 0.7.0 / developer Build 446. Asset keys for these
five facts were cross-checked through the site's supported /dbdata endpoint;
that dataset is not copied into the repository or redistributed by the mod.
The live Undercover Servant GUID/title was cross-checked against our own captures.
The wiki is unofficial. Its terms do not grant redistribution of its writing or
full game dialogues: https://dressmakerwiki.com/terms . Only our original short
English paraphrases and the minimal identifiers/provenance are embedded. We do
not bulk scrape or mirror the site's pages, JSON, images, recipes or cross-links.
Sources remain linked for users to consult the actual excerpts themselves.

## Matching and UI

After explicitly choosing an accepted order, refresh the card. A Russian reference
heading is followed by the English entry, its uncertainties and source. Report
exports preserve that same English content. Proper names/prose are not translated.
No order is selected automatically and no compliance or aesthetic score is derived
from the reference. The live runtime requirements remain a separate source of
truth; the summary never replaces their thresholds.

Lookup is exact and version-scoped: verified GUID first, exact asset key next,
then exact English title as an **unverified candidate**. Conflicting known GUIDs
and keys are rejected. No fuzzy/case-insensitive title match is used; an unknown
or mismatched game version yields unavailable. The native Unity asset name is
read only for the exact selected quest definition as an internal-key candidate.
No arbitrary property getter, progress write or online lookup occurs.

98 offline checks pass, including resource loading, real GUID lookup, asset-key
matching, explicit title uncertainty, conflict/version rejection, missing entries
and provenance text. Full plugin compilation and the new card section remain
unverified against game DLLs in this cloud environment. Existing rendering/text
capture limitations from v0.6.0 are unchanged.

To expand: independently research and write an English entry with a citation,
retain unresolved details, verify its internal key, and record a GUID only after
runtime evidence exists. Do not fill missing quests from title guesses or treat
short public excerpts as complete transcripts. A broad collection of original
English dialogue would require an authorized source; it has not been collected.
