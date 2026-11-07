using System;
using System.Collections.Generic;
using System.Linq;
using DressmakerCritic.Diagnostics;

sealed class TagScoreRequirement
{
    public object tag;
    public float targetScore = 50;
}
static class HistoryOrderTests
{
    sealed class Asset { public string _guid, prettyName; }
    sealed class Definition
    {
        public string _guid, prettyName;
        public int budget = 250;
        public object questGiverCharacter;
        public List<object> completionRequirements = new List<object>();
        public string Trap => throw new Exception("getter invoked");
    }
    sealed class Record { public object dress, questDefinition, dressRecipient; }
    sealed class DisplayDress { public object state; }
    sealed class TextBacking
    {
        public string m_text;
        public string text => throw new Exception("TMP getter must not be invoked");
    }
    sealed class Book
    {
        public DisplayDress _displayDress;
        public int _oldQuestIndex;
        public TextBacking oldQuestTitle, oldQuestText;
    }
    // Deliberately not a claimed game class; proves unknown goal parameters survive.
    sealed class UnresearchedGoalFixture
    {
        public Asset parameter = new Asset { _guid = "parameter-id", prettyName = "Parameter" };
        public float amount = 3;
        public UnresearchedGoalFixture self;
        public string Trap => throw new Exception("goal getter invoked");
    }
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
    public static void Run()
    {
        // Exact IDs/titles from 0.15.3 identity-evidence.json, records 0, 10 and 11.
        Check(HistoryOrder.Classify("e8367d99b82cdda30a34589a064254d6", "0.7.0") == "commission", "real Librarian history definition identifies commission origin");
        Check(HistoryOrder.Classify(HistoryOrder.OffRackGuid, "0.7.0") == "off-rack", "real shared off-rack GUID is classified without title guessing");
        Check(HistoryOrder.Classify(HistoryOrder.OffRackGuid, "0.8.0") == "unknown" && HistoryOrder.Classify("unseen-guid", "0.7.0") == "unknown" && HistoryOrder.Classify(null, "0.7.0") == "unknown", "unknown IDs, versions and missing definitions never become off-rack");
        object state = new object();
        var definition = new Definition { _guid = "e8367d99b82cdda30a34589a064254d6", prettyName = "Librarian Workwear",
            questGiverCharacter = new Asset { _guid = "giver", prettyName = "Giver" } };
        definition.completionRequirements.Add(new TagScoreRequirement { tag = new Asset { _guid = "tag-id", prettyName = "Elegant" } });
        var record = new Record { dress = state, questDefinition = definition, dressRecipient = new Asset { _guid = "recipient", prettyName = "Recipient" } };
        var result = HistoryOrder.Capture(record, state, 0, "0.7.0");
        Check(result.status == "record-state-verified" && result.origin == "commission" && result.definition.guid == definition._guid && result.budget == 250, "historical record normalizes its own definition and budget");
        Check(result.requirements.Single().tag.prettyName == "Elegant" && result.requirements.Single().targetScore == 50, "historical requirements retain focused tag identity and threshold");
        Check(result.questGiverId == "giver" && result.recipientId == "recipient", "recipient and quest giver remain separate identities, not inferred preferences");
        Check(result.requirementCount == 1 && result.requirementsStatus == "complete" && result.requirementFields.Count == 1,
            "focused historical goals report their own count and completeness");
        var unfamiliar = new UnresearchedGoalFixture(); unfamiliar.self = unfamiliar;
        definition.completionRequirements.Add(unfamiliar);
        result = HistoryOrder.Capture(record, state, 0, "0.7.0");
        string raw = DiagnosticJson.Serialize(result.requirementFields);
        Check(result.requirements.Count == 2 && raw.Contains("parameter-id") && raw.Contains("Parameter") && raw.Contains("referenceId"),
            "unknown goal nested parameters and cycles survive bounded field capture");
        Check(result.requirements[1].targetScore == null && result.requirements[1].threshold == null,
            "unfamiliar parameters are not invented as game scores");
        Check(HistoryOrder.DescribeRequirement(result.requirements[1], result.requirementFields[1]).Contains("amount=3"),
            "unfamiliar scalar goal parameters remain visible without guessed semantics");
        definition.completionRequirements = Enumerable.Range(0, 40).Select(_ => (object)unfamiliar).ToList();
        result = HistoryOrder.Capture(record, state, 0, "0.7.0");
        Check(result.requirementCount == 40 && result.requirements.Count == 32 && result.requirementsStatus == "truncated",
            "historical goal collection cap is explicit");
        definition.completionRequirements = null;
        Check(HistoryOrder.Capture(record, state, 0, "0.7.0").requirementsStatus == "unavailable", "missing goal collection is not an empty goal list");
        definition.completionRequirements = new List<object>();
        Check(HistoryOrder.Capture(record, state, 0, "0.7.0").requirementsStatus == "complete", "known empty goal collection is complete");
        var book = new Book { _displayDress = new DisplayDress { state = state }, _oldQuestIndex = 0,
            oldQuestTitle = new TextBacking { m_text = "Librarian Workwear" }, oldQuestText = new TextBacking { m_text = "A dress for library work." } };
        var displayed = HistoryBriefCapture.Read(book, record, state, 0, 0);
        var brief = HistoryBriefCapture.Resolve(displayed, new EvaluationBrief(), 0, definition._guid);
        var input = new EvaluationInput(); input.order.association = "history record 0"; input.order.definition = result.definition; input.order.brief = brief;
        Check(displayed.status == "record-display-verified" && brief.summary == book.oldQuestText.m_text && JuryContract.HasOrderContext(input),
            "verified displayed brief admits context independently of catalog coverage");
        book._oldQuestIndex = 8;
        Check(HistoryBriefCapture.Read(book, record, state, 0, 0).status == "record-display-verified",
            "UI page numbering is not assumed to match completedQuests ordering");
        Check(input.customer.status == "unknown" && !JuryContract.HasCustomerContext(input), "displayed brief never manufactures customer preferences");
        Check(HistoryBriefCapture.Read(book, record, state, 0, null).status == "display-unverified", "unverified display cannot provide semantic brief");
        Check(HistoryBriefCapture.Read(book, record, new object(), 0, 0).status == "record-display-mismatch", "brief bound to another garment is rejected");
        Check(HistoryBriefCapture.Read(book, record, state, 1, 0).status == "record-display-mismatch", "stale history index rejects displayed brief");
        book.oldQuestTitle.m_text = "A different commission";
        displayed = HistoryBriefCapture.Read(book, record, state, 0, 0);
        Check(displayed.status == "title-mismatch-or-unavailable" && !HistoryBriefCapture.Resolve(displayed, null, 0, definition._guid).IsVerified(),
            "stale displayed title cannot turn candidate text into verified context");
        book.oldQuestTitle.m_text = definition.prettyName; book.oldQuestText.m_text = new string('x', 4097);
        Check(HistoryBriefCapture.Read(book, record, state, 0, 0).status == "text-limit", "oversized displayed brief fails without truncation into trusted evidence");
        book.oldQuestText.m_text = "";
        Check(HistoryBriefCapture.Read(book, record, state, 0, 0).status == "text-unavailable", "empty brief remains unavailable");
        Check(ReferenceEquals(record.dress, state) && ReferenceEquals(unfamiliar.self, unfamiliar) && definition.budget == 250,
            "order and UI extraction leave source fields unchanged");
        Check(HistoryOrder.Capture(record, new object(), 0, "0.7.0").status == "record-state-mismatch", "unrelated garment state cannot acquire historical order");
        Check(HistoryOrder.Capture(null, state, 0, "0.7.0").origin == "unknown", "missing history record stays unknown");
        record.questDefinition = null;
        Check(HistoryOrder.Capture(record, state, 0, "0.7.0").status == "definition-unavailable", "missing definition is not proof of off-rack sale");
        record.questDefinition = new Definition { _guid = HistoryOrder.OffRackGuid, prettyName = "A translated title" };
        Check(HistoryOrder.Capture(record, state, 10, "0.7.0").origin == "off-rack", "translated display title does not change known off-rack identity");
        record.questDefinition = new Definition { _guid = "unknown", prettyName = "Off rack dress" };
        Check(HistoryOrder.Capture(record, state, 10, "0.7.0").origin == "unknown", "matching off-rack title alone is insufficient evidence");
    }
}
