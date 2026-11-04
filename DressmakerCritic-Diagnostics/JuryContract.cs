using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.Serialization.Json;
using System.Xml;
using System.Xml.Linq;

namespace DressmakerCritic.Diagnostics
{
    public enum EvaluationTask { Jury, Context }
    [Serializable] public sealed class CriticVote
    {
        public string houseId, status;
        public double? score;
        public List<string> remarks = new List<string>();
    }
    [Serializable] public sealed class ContextJudgement
    {
        public string status = "unknown", scope;
        public double? score;
        public List<string> remarks = new List<string>();
    }
    [Serializable] public sealed class JurySummary
    {
        public string status, verdict;
        public double? average, spread;
        public bool? divided;
    }
    [Serializable] public sealed class JuryAssessment
    {
        public string schemaVersion = "2.0.0", rubricVersion = "1.0.0", captureId;
        public List<CriticVote> jury = new List<CriticVote>();
        public JurySummary summary;
        public ContextJudgement orderCompliance = new ContextJudgement { scope = "provided-summary-only", remarks = new List<string> { "Order context has not been evaluated." } };
        public ContextJudgement customerCompatibility = new ContextJudgement { scope = "verified-customer-preferences-only", remarks = new List<string> { "Customer preferences have not been evaluated." } };
        public string deterministicGameRequirements = "not-evaluated";
        public List<string> limitations = new List<string>();
    }
    public static class JuryContract
    {
        private static readonly string[] ids = { "harmony", "character", "avant_garde", "atelier" };
        public static readonly IReadOnlyList<string> HouseIds = Array.AsReadOnly(ids);
        public const string Rubric = "All four judge the whole garment on a shared 0-10 scale. Harmony prioritizes composition, color/material balance and coherent details. " +
            "Character prioritizes expressive mood and a legible, distinctive idea. Avant-garde prioritizes boldness and unconventional combinations, not provable worldwide originality. " +
            "Atelier prioritizes elegance, proportions and real-clothing plausibility within the game's editing limits. Never demand unavailable edits. " +
            "Different tastes must be explained by visible evidence, not random bonuses, fixed offsets or forced disagreement. " +
            "Anchors: 0-2 severely unresolved, 3-4 uneven, 5-6 competent, 7-8 strong, 9 exceptional, 10 rare outstanding work. " +
            "Use at most one decimal. Each remarks array contains one or two short sentences, one sentence per item. No edit advice. Use unknown/null if evidence is insufficient.";
        internal static XElement Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > 65536) throw new InvalidDataException("Assessment size limit");
            using (var reader = JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(json), new XmlDictionaryReaderQuotas { MaxDepth = 16, MaxStringContentLength = 65536, MaxArrayLength = 65536 }))
            {
                reader.MoveToContent();
                return XElement.Load(reader);
            }
        }
        internal static void Members(XElement value, params string[] names)
        {
            if (value?.Attribute("type")?.Value != "object" || value.Elements().Count() != names.Length || names.Any(n => value.Elements(n).Count() != 1) || value.Elements().Any(e => !names.Contains(e.Name.LocalName)))
                throw new InvalidDataException("Unexpected assessment members");
        }
        internal static string Text(XElement element)
        {
            if (element?.Attribute("type")?.Value != "string") throw new InvalidDataException("Expected string");
            return element.Value;
        }
        internal static List<string> Strings(XElement element, int min, int max, int length)
        {
            if (element?.Attribute("type")?.Value != "array" || element.Elements().Count() < min || element.Elements().Count() > max) throw new InvalidDataException("Invalid remark count");
            var values = element.Elements().Select(Text).ToList();
            if (values.Any(v => string.IsNullOrWhiteSpace(v) || v.Length > length)) throw new InvalidDataException("Invalid remark length");
            if (values.Any(v => new[] { "One short evidence-based sentence.", "Why evidence is insufficient.", "Reason." }.Contains(v.Trim(), StringComparer.OrdinalIgnoreCase)))
                throw new InvalidDataException("Model copied prompt placeholder text instead of assessing the garment");
            return values;
        }
        internal static double? Score(XElement element, string status)
        {
            if (status == "unknown")
            { if (element?.Attribute("type")?.Value != "null") throw new InvalidDataException("Unknown must have null score"); return null; }
            decimal score;
            if (element?.Attribute("type")?.Value != "number" || !decimal.TryParse(element.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out score) || score < 0 || score > 10 || decimal.Round(score, 1) != score)
                throw new InvalidDataException("Expected score 0-10 with at most one decimal");
            return (double)score;
        }
        public static JuryAssessment ReadJury(string json, string captureId)
        {
            var root = Parse(json); Members(root, "schemaVersion", "jury", "limitations");
            if (Text(root.Element("schemaVersion")) != "2.0.0") throw new InvalidDataException("Unknown jury version");
            var rows = root.Element("jury");
            if (rows?.Attribute("type")?.Value != "array" || rows.Elements().Count() != 4) throw new InvalidDataException("Exactly four critics required");
            var result = new JuryAssessment { captureId = captureId, limitations = Strings(root.Element("limitations"), 0, 16, 512) };
            foreach (var row in rows.Elements())
            {
                Members(row, "houseId", "status", "score", "remarks");
                string id = Text(row.Element("houseId")), status = Text(row.Element("status"));
                if (!HouseIds.Contains(id) || result.jury.Any(v => v.houseId == id) || (status != "assessed" && status != "unknown")) throw new InvalidDataException("Invalid critic identity/status");
                result.jury.Add(new CriticVote { houseId = id, status = status, score = Score(row.Element("score"), status), remarks = Strings(row.Element("remarks"), 1, 2, 240) });
            }
            result.jury = result.jury.OrderBy(v => Array.IndexOf(ids, v.houseId)).ToList();
            result.summary = Aggregate(result.jury);
            return result;
        }
        public static JurySummary Aggregate(List<CriticVote> votes)
        {
            if (votes.Count != 4 || HouseIds.Any(id => votes.Count(v => v.houseId == id) != 1)) throw new InvalidDataException("Invalid jury");
            if (votes.Any(v => (v.status != "assessed" && v.status != "unknown") || (v.status == "unknown" && v.score.HasValue) ||
                (v.status == "assessed" && (!v.score.HasValue || double.IsNaN(v.score.Value) || double.IsInfinity(v.score.Value) || v.score < 0 || v.score > 10 || decimal.Round((decimal)v.score.Value, 1) != (decimal)v.score.Value))))
                throw new InvalidDataException("Invalid vote value");
            if (votes.Any(v => v.status != "assessed" || !v.score.HasValue)) return new JurySummary { status = "insufficient-evidence", verdict = "insufficient-evidence" };
            decimal mean = votes.Sum(v => (decimal)v.score.Value) / 4m;
            decimal spread = votes.Max(v => (decimal)v.score.Value) - votes.Min(v => (decimal)v.score.Value);
            return new JurySummary { status = "complete", average = (double)mean, spread = (double)spread, divided = spread >= 3m,
                verdict = mean >= 9 ? "exceptional" : mean >= 7 ? "strong" : mean >= 5 ? "competent" : mean >= 3 ? "uneven" : "weak" };
        }
        public static bool HasOrderContext(EvaluationInput input) => input.order.association != "none: no order selected for evaluation" && input.order.definition != null &&
            (input.order.brief.status == "verified-guid" || input.order.brief.status == "asset-key") && !string.IsNullOrWhiteSpace(input.order.brief.summary) && !string.IsNullOrWhiteSpace(input.order.brief.sourceUrl);
        public static bool HasCustomerContext(EvaluationInput input) => input.customer.status == "verified" && !string.IsNullOrWhiteSpace(input.customer.customerId) && !string.IsNullOrWhiteSpace(input.customer.source) && input.customer.preferences.Any(p => !string.IsNullOrWhiteSpace(p));
        public static bool NeedsContext(EvaluationInput input) => HasOrderContext(input) || HasCustomerContext(input);
        private static ContextJudgement Context(XElement node, string scope, bool available)
        {
            Members(node, "status", "scope", "score", "remarks");
            string status = Text(node.Element("status"));
            if (!new[] { "consistent", "inconsistent", "unknown" }.Contains(status) || Text(node.Element("scope")) != scope || (!available && status != "unknown")) throw new InvalidDataException("Unjustified context judgement");
            return new ContextJudgement { status = status, scope = scope, score = Score(node.Element("score"), status), remarks = Strings(node.Element("remarks"), 1, 2, 240) };
        }
        public static void ApplyContext(string json, EvaluationInput input, JuryAssessment result)
        {
            var root = Parse(json); Members(root, "schemaVersion", "orderCompliance", "customerCompatibility", "limitations");
            if (Text(root.Element("schemaVersion")) != "2.0.0") throw new InvalidDataException("Unknown context version");
            var order = Context(root.Element("orderCompliance"), "provided-summary-only", HasOrderContext(input));
            var customer = Context(root.Element("customerCompatibility"), "verified-customer-preferences-only", HasCustomerContext(input));
            var limits = Strings(root.Element("limitations"), 0, 16, 512);
            result.orderCompliance = order; result.customerCompatibility = customer; result.limitations.AddRange(limits);
        }
    }
}
