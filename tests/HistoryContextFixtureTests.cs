using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using DressmakerCritic.Diagnostics;

static class HistoryContextFixtureTests
{
    static readonly JsonSerializerOptions Options = new JsonSerializerOptions { IncludeFields = true };
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("PASS " + label); }
    static JsonDocument Dress(string name)
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../tests/fixtures/dress-exports", name + ".dress.gz"));
        using (var file = File.OpenRead(root))
        using (var gzip = new GZipStream(file, CompressionMode.Decompress))
        using (var text = new StreamReader(gzip)) return JsonDocument.Parse(text.ReadToEnd());
    }
    static bool SameShape(JsonElement native, GarmentSnapshot snapshot) => snapshot.components.All(c =>
        native.GetProperty(c.slot + "Definition").GetString().Split(',')[0] == c.definition.guid &&
        native.GetProperty(c.slot + "Variant").GetInt32() == c.variant);
    public static void Run()
    {
        foreach (var entry in new[] {
            ("20261108-201120-546-6cf8f74f", 7, "commission", 5, 11),
            ("20261108-201223-533-ef38beed", 10, "off-rack", 0, 0),
            ("20261108-201906-668-966b6e72", 5, "commission", 5, 20) })
        {
            var history = JsonSerializer.Deserialize<HistoryOrderEvidence>(RuntimeFixtures.Read(entry.Item1, "history-order.json"), Options);
            var input = JsonSerializer.Deserialize<EvaluationInput>(RuntimeFixtures.Read(entry.Item1, "evaluation-input.json"), Options);
            Check(history.status == "record-state-verified" && history.historyIndex == entry.Item2 && history.origin == entry.Item3 &&
                history.displayedBrief.status == "record-display-verified", "real history brief/state binding " + entry.Item1);
            Check(history.requirementCount == entry.Item4 && history.requirementsStatus == "complete" &&
                history.requirementFields.Count == entry.Item4 && input.garment.accessories.Count == entry.Item5, "real goals and accessory counts " + entry.Item1);
            Check(JuryContract.HasOrderContext(input) == (entry.Item3 == "commission") && !JuryContract.HasCustomerContext(input),
                "real order admission never fabricates customer preferences " + entry.Item1);
            if (entry.Item3 == "off-rack") continue;
            string response = JsonDocument.Parse(RuntimeFixtures.Read(entry.Item1, "context-model-response.json"))
                .RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            bool rejected = false;
            try { JuryContract.ApplyContext(LocalEvaluator.NormalizeRemarks(response, EvaluationTask.Context), input, new JuryAssessment()); }
            catch (InvalidDataException) { rejected = true; }
            Check(rejected, "real malformed context reply is rejected without guessing scope mappings " + entry.Item1);
            using (var schema = JsonDocument.Parse(DiagnosticJson.Serialize(new ContextOutputSchema(true, false))))
            {
                var root = schema.RootElement.GetProperty("schema");
                var customer = root.GetProperty("properties").GetProperty("customerCompatibility").GetProperty("properties");
                Check(!root.GetProperty("additionalProperties").GetBoolean() &&
                    customer.GetProperty("status").GetProperty("enum").EnumerateArray().Single().GetString() == "unknown" &&
                    customer.GetProperty("score").GetProperty("type").EnumerateArray().Single().GetString() == "null",
                    "context grammar forbids extra root keys and invented customer ratings " + entry.Item1);
            }
        }
        var raw = JsonSerializer.Deserialize<HistoryOrderEvidence>(RuntimeFixtures.Read("20261108-201906-668-966b6e72", "history-order.json"), Options);
        var wrapper = raw.requirementFields[4];
        var graph = new GarmentSnapshotBuilder(wrapper.children);
        var color = ProgressSnapshotBuilder.ReadRequirement(graph, wrapper.children.Single(n => n.member == "goal"));
        Check(color.type == "ColorVarietyRequirement" && color.minimumDistinctColors == 3 && color.targetScore == null &&
            HistoryOrder.DescribeRequirement(color).Contains("at least 3"), "real color goal minimumDistinctColors is normalized without estimating completion");
        var chilly = JsonSerializer.Deserialize<HistoryOrderEvidence>(RuntimeFixtures.Read("20261108-201120-546-6cf8f74f", "history-order.json"), Options);
        Check(chilly.requirements[3].comparison == "AtMost" && chilly.requirements[3].targetScore == 20 && chilly.requirements[3].tag.prettyName == "Cute",
            "real upper-bound tag requirement remains AtMost, not a positive style preference");
        var historyStates = JsonDocument.Parse(RuntimeFixtures.Read("20261108-201120-546-6cf8f74f", "history-candidates.json"))
            .RootElement.GetProperty("garments").EnumerateArray().Select(g => JsonSerializer.Deserialize<GarmentSnapshot>(g.GetRawText(), Options)).ToList();
        using (var dress = Dress("commission-labelled"))
        {
            Check(!SameShape(dress.RootElement, historyStates.Single(g => g.source == "PlayerProgress.completedQuests[7].dress")) &&
                SameShape(dress.RootElement, historyStates.Single(g => g.source == "PlayerProgress.completedQuests[9].dress")),
                "commission-labelled native export is a different history shape than its paired capture");
            Check(!dress.RootElement.TryGetProperty("questDefinition", out _) && !dress.RootElement.TryGetProperty("completionRequirements", out _),
                "native dress state alone does not carry historical order association");
        }
        foreach (var pair in new[] { ("off-rack", 10, 0), ("accessories", 5, 20) })
            using (var dress = Dress(pair.Item1))
            {
                var state = historyStates.Single(g => g.source == "PlayerProgress.completedQuests[" + pair.Item2 + "].dress");
                Check(SameShape(dress.RootElement, state) && dress.RootElement.GetProperty("accessoryStates").GetArrayLength() == pair.Item3,
                    "native export and captured history agree on shape/accessory count " + pair.Item1);
            }
        var probeInput = JsonSerializer.Deserialize<EvaluationInput>(RuntimeFixtures.Read("20261108-201120-546-6cf8f74f", "evaluation-input.json"), Options);
        foreach (string name in new[] { "metadata-only", "grounding-revision" })
        {
            string path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../tests/fixtures/context-2b-probe", name + ".json"));
            var result = new JuryAssessment();
            JuryContract.ApplyContext(File.ReadAllText(path), probeInput, result);
            Check(result.customerCompatibility.status == "unknown" && result.customerCompatibility.score == null && result.orderCompliance.score.HasValue,
                "real structured probe passes format only; semantic acceptance is not implied " + name);
        }
    }
}
