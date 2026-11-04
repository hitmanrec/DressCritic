using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DressmakerCritic.Diagnostics;

static class SelectionProbeTests
{
    class Record { public object dress; }
    class Progress { public List<Record> completedQuests = new List<Record>(); }
    class Owner { public object selected; public int selector = 7; public object Getter => throw new Exception("getter invoked"); }
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("PASS " + label); }
    public static void Run()
    {
        var state = new object(); var progress = new Progress();
        progress.completedQuests.Add(new Record { dress = null }); progress.completedQuests.Add(new Record { dress = state });
        var evidence = SelectionProbe.Read(new Owner { selected = state }, progress, "fixture");
        Check(evidence.references.Single().matchingHistoryIndices.SequenceEqual(new[] {1}), "selector probe records exact state reference without invoking getters");
        Check(SelectionProbe.Read(new Owner(), progress, "fixture").references.Count == 0, "null states are never false history matches");
        Check(HistoryImageBinding.FindState(progress, state) == 1, "display state resolves by exact unique history reference");
        progress.completedQuests.Add(new Record { dress = state });
        Check(HistoryImageBinding.FindState(progress, state) == null, "duplicate history state refuses binding");
        progress.completedQuests.RemoveAt(2);
        var mannequin = new object(); var previewTexture = new object();
        Check(HistoryImageBinding.DisplayChain(state,state,mannequin,mannequin,previewTexture,previewTexture,true), "complete visible preview chain verifies identity");
        Check(!HistoryImageBinding.DisplayChain(state,new object(),mannequin,mannequin,previewTexture,previewTexture,true) &&
              !HistoryImageBinding.DisplayChain(state,state,new object(),mannequin,previewTexture,previewTexture,true) &&
              !HistoryImageBinding.DisplayChain(state,state,mannequin,mannequin,previewTexture,new object(),true) &&
              !HistoryImageBinding.DisplayChain(state,state,mannequin,mannequin,previewTexture,previewTexture,false) &&
              !HistoryImageBinding.DisplayChain(state,state,null,null,previewTexture,previewTexture,true), "wrong state mannequin camera target and hidden preview refuse identity");
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        string dir = Path.Combine(root, "tests/fixtures/capture-700");
        using (var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "identity-evidence.json"))))
        {
            var viewers = doc.RootElement.GetProperty("viewers").EnumerateArray().ToArray();
            var active = viewers.Single(v => v.GetProperty("active").GetBoolean());
            int texture = active.GetProperty("textureId").GetInt32();
            Check(viewers.All(v => v.GetProperty("textureId").GetInt32() == texture) && active.GetProperty("textureType").GetString() == "UnityEngine.RenderTexture", "real game viewer reuses one shared RenderTexture");
            Check(doc.RootElement.GetProperty("history").EnumerateArray().All(h => h.GetProperty("thumbnailTextureId").ValueKind == JsonValueKind.Null || h.GetProperty("thumbnailTextureId").GetInt32() != texture), "real capture cannot be bound through stored thumbnail identity");
        }
        var input = new EvaluationInput { garment = new GarmentSnapshot { garmentStatus = "absent" } };
        input.images.Add(new EvaluationImage { file = "image.png", sha256 = new string('0',64), width = 2048, height = 2048, binding = "unverified" });
        var readiness = EvaluationReadiness.Check(input,"fixture-model");
        Check(readiness.blockers.Contains("garment-unavailable") && readiness.blockers.Contains("image-garment-identity-unverified") && !readiness.blockers.Contains("image-unavailable"), "unpaired diagnostic image is preserved without permitting model evaluation");
    }
}
