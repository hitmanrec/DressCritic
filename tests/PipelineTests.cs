using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DressmakerCritic.Diagnostics;

static class PipelineTests
{
    static string Good => JuryTests.Good;
    class Server : HttpMessageHandler
    {
        public int calls;
        public string response = Good;
        public HttpStatusCode status = HttpStatusCode.OK;
        public bool delay;
        public string finishReason;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            calls++;
            if (request.Method == HttpMethod.Get)
            {
                Check(request.RequestUri.AbsolutePath == "/v1/models" && request.Content == null, "local metadata probe sends no garment data");
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"data\":[{\"id\":\"fixture-model\"}]}") };
            }
            if (delay) await Task.Delay(Timeout.Infinite, cancellation);
            string json = await request.Content.ReadAsStringAsync();
            using (var doc = JsonDocument.Parse(json))
            {
                Check(doc.RootElement.GetProperty("reasoning_effort").GetString() == "none", "request disables optional model reasoning to reserve output for final JSON");
                var parts = doc.RootElement.GetProperty("messages")[1].GetProperty("content");
                Check(parts.GetArrayLength() == 3 && parts[2].GetProperty("image_url").GetProperty("url").GetString().StartsWith("data:image/png;base64,"), "actual multimodal HTTP body includes image and machine facts");
                Check(!parts[0].TryGetProperty("image_url", out _), "text request part omits null image fields");
            }
            return new HttpResponseMessage(status) { Content = new StringContent(JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = response, reasoning = "Fixture private analysis must never be used as an assessment." }, finish_reason = finishReason } } })) };
        }
    }
    class HistoryRecord { public object dress = new object(); public object thumbnailTex; public bool thumbnailDirty; }
    class HistoryFixture { public List<HistoryRecord> completedQuests = new List<HistoryRecord>(); }
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("PASS " + label); }
    static void Refuse(Action action, string label) { bool rejected = false; try { action(); } catch { rejected = true; } Check(rejected, label); }
    static EvaluationInput Input() => new EvaluationInput { captureId = "fixture", garment = new GarmentSnapshot { garmentStatus = "available" }, images = new List<EvaluationImage> { new EvaluationImage { file = "image.png", width = 2048, height = 2048, binding = "active-state-verified" } } };
    public static async Task Run()
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        string png = Path.Combine(root, "tests/fixtures/transport.png");
        byte[] image = File.ReadAllBytes(png);
        var input = Input(); input.images[0].sha256 = CaptureSession.Hash(png);
        Check(!EvaluationReadiness.Check(input, "").canEvaluate, "unconfigured model blocks evaluation");
        input.images[0].binding = "unverified";
        Check(EvaluationReadiness.Check(input, "fixture-model").blockers.Contains("image-garment-identity-unverified"), "viewer ambiguity is a hard blocker, not a guessed association");
        Refuse(() => ModelRequest.Build(input, "fixture-model", _ => image), "cannot build request for mismatched/unpaired input");
        input.images[0].binding = "active-state-verified";
        input.images[0].sha256 = "wrong";
        Refuse(() => ModelRequest.Build(input, "fixture-model", _ => image), "cannot evaluate image bytes that differ from captured hash");
        input.images[0].sha256 = CaptureSession.Hash(png);
        input.images[0].file = "../outside.png";
        Refuse(() => ModelRequest.Build(input, "fixture-model", _ => image), "request cannot load image paths outside a capture");
        input.images[0].file = "image.png";
        string request = DiagnosticJson.Serialize(ModelRequest.Build(input, "fixture-model", _ => image));
        AssessmentValidator.Validate(Good, input);
        Refuse(() => AssessmentValidator.Validate(Good.Replace("\"score\":7", "\"score\":11"), input), "reject out-of-range jury score");
        Refuse(() => AssessmentValidator.Validate(Good.Replace("\"limitations\":[", "\"recommendations\":[\"Invent an editor tool\"],\"limitations\":["), input), "reject extra recommendation fields");
        Refuse(() => LocalEvaluator.ValidateEndpoint("https://example.com/v1/chat/completions"), "remote model endpoint rejected");
        Refuse(() => LocalEvaluator.ValidateEndpoint("http://user:password@127.0.0.1:11434/v1/chat/completions"), "embedded credential endpoint rejected");
        using (var handler = new Server()) using (var client = new HttpClient(handler))
        {
            string raw = null;
            string result = await LocalEvaluator.Run(client, "http://127.0.0.1:11434/v1/chat/completions", request, input, CancellationToken.None, value => raw = value);
            Check(result == Good && handler.calls == 1 && raw.Contains("choices"), "end-to-end injected local server response is preserved and validated");
            handler.response = File.ReadAllText(Path.Combine(root, "tests/fixtures/jury-string-remarks.json"));
            string recovered = await LocalEvaluator.Run(client, "http://127.0.0.1:11434/v1/chat/completions", request, input, CancellationToken.None, value => raw = value);
            Check(JuryContract.ReadJury(recovered, "http").summary.average == 6.625 &&
                JsonDocument.Parse(raw).RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() == handler.response,
                "HTTP normalizes real response while audit retains original model output");
            input.order.association = "explicit accepted-order index 0";
            input.order.definition = new AssetIdentity { status = "fields", prettyName = "Fixture order" };
            input.order.brief.status = "asset-key"; input.order.brief.summary = "Fixture brief"; input.order.brief.sourceUrl = "https://source.invalid/fixture";
            handler.response = JuryTests.UnknownContext;
            string contextRequest = DiagnosticJson.Serialize(ModelRequest.Build(input, "fixture-model", _ => image, EvaluationTask.Context));
            string contextResult = await LocalEvaluator.Run(client, "http://127.0.0.1:11434/v1/chat/completions", contextRequest, input, CancellationToken.None, task: EvaluationTask.Context);
            Check(contextResult == JuryTests.UnknownContext, "separate HTTP context response uses context contract, never jury contract");
            handler.response = ""; handler.finishReason = "length";
            bool truncated = false;
            try { await LocalEvaluator.Run(client, "http://127.0.0.1:11434/v1/chat/completions", request, input, CancellationToken.None); }
            catch (InvalidDataException e) { truncated = e.Message.Contains("token limit"); }
            Check(truncated, "real capture empty final content with length finish is diagnosed before JSON parsing");
            handler.response = Good; // Even apparently valid truncated output must be refused.
            bool lengthRejected = false;
            try { await LocalEvaluator.Run(client, "http://127.0.0.1:11434/v1/chat/completions", request, input, CancellationToken.None); }
            catch (InvalidDataException e) { lengthRejected = e.Message.Contains("token limit"); }
            Check(lengthRejected, "truncated completion cannot become a successful assessment");
            handler.response = ""; handler.finishReason = "stop";
            bool emptyRejected = false;
            try { await LocalEvaluator.Run(client, "http://127.0.0.1:11434/v1/chat/completions", request, input, CancellationToken.None); }
            catch (InvalidDataException e) { emptyRejected = e.Message.Contains("no final assessment"); }
            Check(emptyRejected, "reasoning-only reply never supplies scores");
            handler.finishReason = null;
            handler.response = "not JSON"; bool failed = false;
            try { await LocalEvaluator.Run(client, "http://127.0.0.1:11434/v1/chat/completions", request, input, CancellationToken.None); } catch { failed = true; }
            Check(failed, "invalid model reply fails closed");
            Check((await LocalEvaluator.Probe(client, "http://127.0.0.1:11434/v1/chat/completions", CancellationToken.None)).Contains("fixture-model"), "available local model IDs can be diagnosed without auto-selection");
            handler.delay = true;
            using (var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(25)))
            {
                bool cancelled = false;
                try { await LocalEvaluator.Run(client, "http://127.0.0.1:11434/v1/chat/completions", request, input, timeout.Token); } catch (OperationCanceledException) { cancelled = true; }
                Check(cancelled, "cancelled capture does not leave an unbounded model request");
            }
        }
        var texture = new object();
        var history = new HistoryFixture();
        history.completedQuests.Add(new HistoryRecord { thumbnailTex = texture });
        Check(HistoryImageBinding.Find(history, texture) == 0, "exact clean thumbnail object can identify one saved record");
        Check(HistoryImageBinding.Find(history, new object()) == null, "texture similarity/name cannot identify a record");
        history.completedQuests[0].thumbnailDirty = true;
        Check(HistoryImageBinding.Find(history, texture) == null, "stale thumbnail does not bind to newer structure");
        history.completedQuests[0].thumbnailDirty = false;
        history.completedQuests.Add(new HistoryRecord { thumbnailTex = texture });
        Check(HistoryImageBinding.Find(history, texture) == null, "shared thumbnail across records remains ambiguous");
        string dir = Path.Combine(Path.GetTempPath(), "dresscritic-pipeline-" + Guid.NewGuid().ToString("N"));
        var capture = new CaptureSession(dir, "fixture");
        capture.Stage("failing-render", () => throw new InvalidOperationException("fixture"));
        capture.Stage("next-stage", () => capture.Write("evaluation-input.json", input));
        capture.Finish();
        using (var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "manifest.json"))))
        {
            var data = doc.RootElement;
            Check(data.GetProperty("complete").GetBoolean() && data.GetProperty("stages")[0].GetProperty("status").GetString() == "failed" && data.GetProperty("stages")[1].GetProperty("status").GetString() == "ok", "stage failure preserves remaining diagnostic evidence");
            Check(data.GetProperty("files")[0].GetProperty("sha256").GetString() == CaptureSession.Hash(Path.Combine(dir, "evaluation-input.json")) && !File.Exists(Path.Combine(dir, ".capture-in-progress")), "manifest hashes actual artifacts and marks completion");
        }
        Directory.Delete(dir, true);
    }
}
