using System;
using System.Collections.Generic;
using System.Linq;
using DressmakerCritic.Diagnostics;

static class EvidenceTests
{
    class AssetFixture { public string _guid = "accessory-id"; public string prettyName = "Pin"; }
    struct VectorFixture { public float x, y, z; public VectorFixture(int n) { x=n; y=n+1; z=n+2; } }
    class AccessoryFixture { public AssetFixture definition = new AssetFixture(); public VectorFixture placedPos = new VectorFixture(1); public VectorFixture placedRot = new VectorFixture(1); public VectorFixture surfaceNormal = new VectorFixture(1); public bool placed = true; public float scale = 1; }
    class StateFixture { public List<AccessoryFixture> accessoryStates = new List<AccessoryFixture>{ new AccessoryFixture() }; }
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); Console.WriteLine("PASS " + text); }
    public static void Run()
    {
        int[] size = ImageEvidence.Fit(3840, 2160, 2048);
        Check(size[0] == 2048 && size[1] == 1152, "image dimensions preserve aspect and cap resolution");
        bool refused = false; try { ImageEvidence.Fit(0, 1, 2048); } catch (ArgumentOutOfRangeException) { refused = true; }
        Check(refused, "empty image is rejected");
        var state = new StateFixture(); var roots = new List<ValueNode>(); var graph = new FieldSnapshot(4096);
        FocusedGarmentFields.AddAccessories(roots, graph, state, "GameManager.activeDress.state");
        var root = graph.Read(state); root.member = "GameManager.activeDress.state"; roots.Add(root);
        var garment = new GarmentSnapshotBuilder(roots).Build("test", "test");
        Check(garment.accessoryCount == 1 && garment.accessories[0].definition.guid == "accessory-id" && garment.accessories[0].placedPos.x == 1 && garment.accessories[0].placedPos.z == 3, "focused accessory roots preserve definition and coordinates before depth cutoff");
    }
}
