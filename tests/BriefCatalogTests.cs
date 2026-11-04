using System;
using DressmakerCritic.Diagnostics;
static class BriefCatalogTests
{
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
    public static void Run()
    {
        var catalog = BriefReferenceCatalog.Bundled;
        Check(catalog.Count == 5 && catalog.Coverage.Contains("Five"), "bundled reference loads and declares limited coverage");
        var match = catalog.Find("f205a54432cf7a4bda9bf1ab59049a6d", null, null, "0.7.0");
        Check(match.status == "verified-guid" && match.entry.assetKey == "PrudenceModest", "real runtime order GUID finds English reference");
        Check(catalog.Find(null, "RoseWork", null, "0.7.0").entry.englishTitle == "Librarian Workwear", "exact asset-key mapping");
        Check(catalog.Find(null, null, "Pigeon Adoption", "0.7.0").status == "title-only-unverified", "name fallback explicitly unverified");
        Check(catalog.Find(null, null, "pigeon adoption", "0.7.0").entry == null, "no fuzzy or case-insensitive name guessing");
        Check(catalog.Find("wrong-guid", "PrudenceModest", "Undercover Servant", "0.7.0").status == "identity-conflict", "conflicting GUID/key rejected");
        Check(catalog.Find("wrong-guid", null, "Undercover Servant", "0.7.0").status == "identity-conflict", "title fallback cannot bypass known GUID conflict");
        Check(catalog.Find(null, "RoseWork", null, "0.8.0").entry == null, "wrong game version fails closed");
        Check(catalog.Find(null, "UnknownQuest", "Unknown title", "0.7.0").status == "missing", "unresearched quests do not get invented summaries");
        Check(match.entry.summary.Contains("household work") && match.entry.sourceUrl.StartsWith("https://dressmakerwiki.com/") && match.entry.dialogueCoverage.Contains("excerpt-only"), "English reference preserves evidence and coverage");
    }
}
