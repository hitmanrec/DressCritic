using System;
using System.IO;
using System.Text.Json;
using DressmakerCritic.Diagnostics;

// Uses production request construction and validation; no independent scoring implementation.
if (args.Length != 3) throw new ArgumentException("request[-context] INPUT MODEL | validate CONTENT CAPTURE_ID | validate-context INPUT CONTENT");
if (args[0] == "request" || args[0] == "request-context")
{
    string path = Path.GetFullPath(args[1]);
    string text = File.ReadAllText(path).TrimStart('\uFEFF');
    var input = JsonSerializer.Deserialize<EvaluationInput>(text, new JsonSerializerOptions { IncludeFields = true });
    var request = ModelRequest.Build(input, args[2], image => File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(path), image.file)),
        args[0] == "request-context" ? EvaluationTask.Context : EvaluationTask.Jury);
    Console.Write(DiagnosticJson.Serialize(request));
}
else if (args[0] == "validate")
{
    string text = LocalEvaluator.NormalizeRemarks(File.ReadAllText(args[1]), EvaluationTask.Jury);
    Console.Write(DiagnosticJson.Serialize(JuryContract.ReadJury(text, args[2])));
}
else if (args[0] == "validate-context")
{
    var input = JsonSerializer.Deserialize<EvaluationInput>(File.ReadAllText(args[1]).TrimStart('\uFEFF'), new JsonSerializerOptions { IncludeFields = true });
    var result = new JuryAssessment { captureId = input.captureId };
    JuryContract.ApplyContext(LocalEvaluator.NormalizeRemarks(File.ReadAllText(args[2]), EvaluationTask.Context), input, result);
    Console.Write(DiagnosticJson.Serialize(result));
}
else throw new ArgumentException("Unknown command");
