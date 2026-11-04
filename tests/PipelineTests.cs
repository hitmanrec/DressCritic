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
    const string Good = "{\"schemaVersion\":\"1.0.0\",\"visualQuality\":{\"status\":\"assessed\",\"score\":70,\"observations\":[\"Fixture observation\"],\"uncertainties\":[\"One view\"]},\"briefCompliance\":{\"status\":\"unknown\",\"scope\":\"provided-summary-only\",\"observations\":[],\"uncertainties\":[\"No brief\"]},\"deterministicGameRequirements\":{\"status\":\"not-evaluated\"},\"limitations\":[\"No recommendations\"]}";
    class Server : HttpMessageHandler
    {
        public int calls;
        public string response = Good;
        public HttpStatusCode status = HttpStatusCode.OK;
        public bool delay;
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
                var parts = doc.RootElement.GetProperty("messages")[1].GetProperty("content");
                Check(parts.GetArrayLength() == 2 && parts[1].GetProperty("image_url").GetProperty("url").GetString().StartsWith("data:image/png;base64,"), "actual multimodal HTTP body includes image and machine facts");
                Check(!parts[0].TryGetProperty("image_url", out _), "text request part omits null image fields");
            }
            return new HttpResponseMessage(status) { Content = new StringContent(JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = response } } } })) };
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
        string png = Path.Combine(root, "DressmakerCritic-Diagnostics/exports/captures/image-20261104-141507-684-23ac5b7e/image.png");
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
        Refuse(() => AssessmentValidator.Validate(Good.Replace("\"score\":70", "\"score\":101"), input), "reject out-of-range subjective score");
        Refuse(() => AssessmentValidator.Validate(Good.Replace("\"status\":\"not-evaluated\"", "\"status\":\"passed\""), input), "reject invented deterministic compliance");
        Refuse(() => AssessmentValidator.Validate(Good.Replace("provided-summary-only", "full-client-intent"), input), "cannot overstate brief scope");
        Refuse(() => AssessmentValidator.Validate(Good.Replace("\"status\":\"unknown\",\"scope\"", "\"status\":\"consistent\",\"scope\""), input), "cannot judge unavailable brief");
        Refuse(() => AssessmentValidator.Validate(Good.Replace("\"limitations\":[", "\"recommendations\":[\"Invent an editor tool\"],\"limitations\":["), input), "reject extra recommendation fields");
        Refuse(() => LocalEvaluator.ValidateEndpoint("https://example.com/v1/chat/completions"), "remote model endpoint rejected");
        Refuse(() => LocalEvaluator.ValidateEndpoint("http://user:password@127.0.0.1:11434/v1/chat/completions"), "embedded credential endpoint rejected");
        using (var handler = new Server()) using (var client = new HttpClient(handler))
        {
            string raw = null;
            string result = await LocalEvaluator.Run(client, "http://127.0.0.1:11434/v1/chat/completions", request, input, CancellationToken.None, value => raw = value);
            Check(result == Good && handler.calls == 1 && raw.Contains("choices"), "end-to-end injected local server response is preserved and validated");
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
