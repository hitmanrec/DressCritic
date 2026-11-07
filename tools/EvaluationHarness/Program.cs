using System;
using System.IO;
using System.Text.Json;
using DressmakerCritic.Diagnostics;

// Uses production request construction and validation; no independent scoring implementation.
if (args.Length != 3) throw new ArgumentException("request INPUT MODEL | validate CONTENT CAPTURE_ID");
if (args[0] == "request")
{
    string path = Path.GetFullPath(args[1]);
    string text = File.ReadAllText(path).TrimStart('\uFEFF');
    var input = JsonSerializer.Deserialize<EvaluationInput>(text, new JsonSerializerOptions { IncludeFields = true });
    var request = ModelRequest.Build(input, args[2], image => File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(path), image.file)));
    Console.Write(DiagnosticJson.Serialize(request));
}
else if (args[0] == "validate")
{
    string text = LocalEvaluator.NormalizeRemarks(File.ReadAllText(args[1]), EvaluationTask.Jury);
    Console.Write(DiagnosticJson.Serialize(JuryContract.ReadJury(text, args[2])));
}
else throw new ArgumentException("Unknown command");
