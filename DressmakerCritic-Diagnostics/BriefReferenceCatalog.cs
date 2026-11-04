using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace DressmakerCritic.Diagnostics
{
    [DataContract] public sealed class BriefReferenceData
    {
        [DataMember] public string schemaVersion;
        [DataMember] public string language;
        [DataMember] public string gameVersion;
        [DataMember] public string coverage;
        [DataMember] public List<BriefReference> entries;
    }
    [DataContract] public sealed class BriefReference
    {
        [DataMember] public string assetKey, englishTitle, client, verifiedGuid, summary;
        [DataMember] public string[] requests, prohibitions, uncertainties;
        [DataMember] public string sourceUrl, sourceKind, reviewedAt, dialogueCoverage, mappingEvidence;
    }
    public sealed class BriefReferenceMatch
    {
        public string status;
        public BriefReference entry;
    }
    public sealed class BriefReferenceCatalog
    {
        private readonly BriefReferenceData data;
        private static readonly Lazy<BriefReferenceCatalog> bundled = new Lazy<BriefReferenceCatalog>(Load);
        public static BriefReferenceCatalog Bundled => bundled.Value;
        public int Count => data.entries.Count;
        public string Coverage => data.coverage;
        private BriefReferenceCatalog(BriefReferenceData data) { this.data = data; }
        private static BriefReferenceCatalog Load()
        {
            using (var stream = typeof(BriefReferenceCatalog).Assembly.GetManifestResourceStream("DressmakerCritic.BriefReference.en.json"))
            {
                if (stream == null || stream.Length > 131072) throw new InvalidDataException("Missing/oversized brief reference");
                var data = (BriefReferenceData)new DataContractJsonSerializer(typeof(BriefReferenceData)).ReadObject(stream);
                if (data.schemaVersion != "1.0.0" || data.language != "en" || string.IsNullOrEmpty(data.gameVersion) || data.entries == null ||
                    data.entries.Any(e => string.IsNullOrEmpty(e.assetKey) || string.IsNullOrEmpty(e.summary) || string.IsNullOrEmpty(e.sourceUrl)) ||
                    data.entries.GroupBy(e => e.assetKey, StringComparer.Ordinal).Any(g => g.Count() > 1))
                    throw new InvalidDataException("Invalid brief reference");
                return new BriefReferenceCatalog(data);
            }
        }
        public BriefReferenceMatch Find(string guid, string assetKey, string englishTitle, string gameVersion)
        {
            if (gameVersion != data.gameVersion) return new BriefReferenceMatch { status = "version-mismatch" };
            var byGuid = string.IsNullOrEmpty(guid) ? null : data.entries.FirstOrDefault(e => !string.IsNullOrEmpty(e.verifiedGuid) && e.verifiedGuid == guid);
            var byKey = string.IsNullOrEmpty(assetKey) ? null : data.entries.FirstOrDefault(e => e.assetKey == assetKey);
            if (byGuid != null && byKey != null && !ReferenceEquals(byGuid, byKey)) return new BriefReferenceMatch { status = "identity-conflict" };
            var exact = byGuid ?? byKey;
            if (exact != null)
            {
                if (!string.IsNullOrEmpty(guid) && !string.IsNullOrEmpty(exact.verifiedGuid) && guid != exact.verifiedGuid)
                    return new BriefReferenceMatch { status = "identity-conflict" };
                return new BriefReferenceMatch { status = byGuid != null ? "verified-guid" : "asset-key", entry = exact };
            }
            var titles = data.entries.Where(e => e.englishTitle == englishTitle).ToArray();
            if (titles.Length == 1 && !string.IsNullOrEmpty(guid) && !string.IsNullOrEmpty(titles[0].verifiedGuid) && guid != titles[0].verifiedGuid)
                return new BriefReferenceMatch { status = "identity-conflict" };
            return new BriefReferenceMatch { status = titles.Length == 1 ? "title-only-unverified" : "missing", entry = titles.Length == 1 ? titles[0] : null };
        }
    }
}
