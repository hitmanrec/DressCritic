using System;
using System.Collections.Generic;
using System.Linq;
using DressmakerCritic.Diagnostics;

static class EvidenceTests
{
    class TextFixture { public string m_text = "Observed dialogue"; public string other = "Not text"; public string Text => throw new Exception("getter invoked"); }
    class AssetFixture { public string _guid = "accessory-id"; public string prettyName = "Pin"; }
    class VectorFixture { public float x = 1, y = 2, z = 3; }
    class AccessoryFixture { public AssetFixture definition = new AssetFixture(); public VectorFixture placedPos = new VectorFixture(); public VectorFixture placedRot = new VectorFixture(); public VectorFixture surfaceNormal = new VectorFixture(); public bool placed = true; public float scale = 1; }
    class StateFixture { public List<AccessoryFixture> accessoryStates = new List<AccessoryFixture>{ new AccessoryFixture() }; }
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); Console.WriteLine("PASS " + text); }
    public static void Run()
    {
        var text = new BriefEvidence(); text.ReadTextFields(new TextFixture(), "fixture");
        Check(text.observations.Count == 1 && text.observations[0].text == "Observed dialogue", "candidate text reads fields only, not getters");
        text = new BriefEvidence(); text.ReadTextFields(new TextFixture { m_text = new string('x', 5000) }, "fixture");
        Check(text.observations[0].text.Length == 4096 && text.observations[0].truncated, "dialogue evidence string bound");
        for (int i = 0; i < 40; i++) text.ReadTextFields(new TextFixture(), "fixture");
        Check(text.observations.Count == 32 && text.status == "text-limit", "dialogue evidence collection bound");
        int[] size = ImageEvidence.Fit(3840, 2160, 2048);
        Check(size[0] == 2048 && size[1] == 1152, "image dimensions preserve aspect and cap resolution");
        bool refused = false; try { ImageEvidence.Fit(0, 1, 2048); } catch (ArgumentOutOfRangeException) { refused = true; }
        Check(refused, "empty image is rejected");
        Check(new ImageEvidence().pairing.StartsWith("unpaired:"), "image metadata does not invent garment/order pairing");
        var state = new StateFixture(); var roots = new List<ValueNode>(); var graph = new FieldSnapshot(4096);
        FocusedGarmentFields.AddAccessories(roots, graph, state, "GameManager.activeDress.state");
        var root = graph.Read(state); root.member = "GameManager.activeDress.state"; roots.Add(root);
        var garment = new GarmentSnapshotBuilder(roots).Build("test", "test");
        Check(garment.accessoryCount == 1 && garment.accessories[0].definition.guid == "accessory-id" && garment.accessories[0].placedPos.x == 1 && garment.accessories[0].placedPos.z == 3, "focused accessory roots preserve definition and coordinates before depth cutoff");
    }
}
