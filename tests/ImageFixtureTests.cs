using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using DressmakerCritic.Diagnostics;
static class ImageFixtureTests
{
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("PASS " + label); }
    public static void Run()
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        string captures = Path.Combine(root, "DressmakerCritic-Diagnostics/exports/captures");
        var options = new JsonSerializerOptions { IncludeFields = true };
        foreach (string name in new[] { "brief-20261104-141325-378-de9f96f6", "brief-20261104-141420-253-a4e9fe2c" })
        {
            var evidence = JsonSerializer.Deserialize<BriefEvidence>(File.ReadAllText(Path.Combine(captures, name, "brief-evidence.json")), options);
            evidence.ClassifyDialogueCoverage();
            Check(evidence.status == "no-dialogue-text" && evidence.observations.Count == 1 && evidence.observations[0].text == "Rose", "real speaker-only evidence is not a client brief: " + name);
        }
        var dialogue = new BriefEvidence();
        dialogue.observations.Add(new TextEvidence { source = "FrontDesk.dialogueText.TMPro.TMP_Text.m_text", text = "Fixture dialogue" });
        dialogue.ClassifyDialogueCoverage();
        Check(dialogue.status == "candidate-text-only", "dialogue candidate still does not claim complete brief");
        string directory = Path.Combine(captures, "image-20261104-141507-684-23ac5b7e");
        var metadata = JsonSerializer.Deserialize<ImageEvidence>(File.ReadAllText(Path.Combine(directory, "image-evidence.json")), options);
        byte[] bytes = File.ReadAllBytes(Path.Combine(directory, "image.png"));
        Check(bytes.Take(8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}), "real capture has PNG signature");
        int width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16,4));
        int height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20,4));
        Check(width == metadata.width && height == metadata.height && width == 2048 && height == 2048, "real viewer image dimensions match metadata");
        Check(metadata.source.StartsWith("OldDressView.renderImage") && metadata.pairing.StartsWith("unpaired:") && metadata.files.Count == 1, "real single-view texture does not invent selected-garment pairing");
    }
}
