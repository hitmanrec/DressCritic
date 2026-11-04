using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using DressmakerMods;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DressmakerCritic.Diagnostics
{
    [BepInPlugin("community.dressmakercritic.diagnostics", "Dressmaker Critic", "0.7.1")]
    [BepInDependency("yuu.dressmaker.mods", "0.7.8")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private ConfigEntry<int> savedRecord, orderIndex;
        private ConfigEntry<string> model, endpoint;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private IDisposable command;
        private volatile bool busy;
        private volatile string status = "";
        private void Awake()
        {
            savedRecord = Config.Bind("Selection", "SavedDressIndex", -2, "-2: exact verified viewer binding then active fallback; -1: active dress; otherwise zero-based completedQuests record index. Does not modify the game.");
            orderIndex = Config.Bind("Selection", "OrderIndex", -1, "-1: visual assessment without order association; otherwise explicit zero-based accepted-order index.");
            model = Config.Bind("Inference", "Model", "", "Exact model ID from your local multimodal server; blank prevents inference.");
            endpoint = Config.Bind("Inference", "Endpoint", "http://127.0.0.1:11434/v1/chat/completions", "Loopback OpenAI-compatible vision endpoint only. No hosted API or embedded credentials.");
            Logger.LogInfo("Dressmaker Critic 0.7.1: evaluation pipeline; read-only game access.");
        }
        private void Start()
        {
            command = DebugConsoleApi.Register("critic-export", "Capture one complete evaluation diagnostic package", _ => { BeginCapture(); return "Evaluation diagnostic capture started"; });
        }
        private void OnGUI()
        {
            bool previous = GUI.enabled;
            try
            {
                GUI.enabled = previous && !busy;
                if (GUI.Button(new Rect(Math.Max(0, Screen.width - 312), 12, 300, 36), busy ? "Сбор пакета…" : "Диагностика оценщика")) BeginCapture();
                if (!string.IsNullOrEmpty(status)) GUI.Label(new Rect(Math.Max(0, Screen.width - 412), 52, 400, 100), status);
            }
            finally { GUI.enabled = previous; }
        }
        private async void BeginCapture()
        {
            if (busy) return;
            busy = true; status = "Собираются данные, изображение и причины блокировки оценки.";
            CaptureSession capture = null;
            try
            {
                string id = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                capture = new CaptureSession(Path.Combine(Paths.ConfigPath, "DressmakerCritic", "captures", id), id);
                var input = new EvaluationInput { captureId = id, capturedAtUtc = capture.manifest.startedAtUtc };
                object progress = null, state = null, selectedRecord = null; Component manager = null;
                string progressStatus = "not-read", source = "GameManager.activeDress.state";
                string selectedModel = model.Value, selectedEndpoint = endpoint.Value;
                int selectedSaved = savedRecord.Value, selectedOrder = orderIndex.Value;
                int requestedSaved = selectedSaved;
                capture.Stage("runtime", () => capture.Write("runtime.json", GameRuntimeReader.Environment()));
                capture.Stage("selection-and-identity", () => {
                    progress = ProgressAccess.TryRead(GameRuntimeReader.GameType("PlayerProgress"), out progressStatus);
                    capture.Write("identity-evidence.json", GameRuntimeReader.Identity(progress, progressStatus));
                    var managers = GameRuntimeReader.Find("GameManager");
                    if (managers.Length != 1) throw new InvalidOperationException("GameManager count=" + managers.Length);
                    manager = managers[0];
                    if (selectedSaved == -2)
                    {
                        var viewers = GameRuntimeReader.Find("OldDressView").Where(v => v.gameObject.activeInHierarchy).Take(2).ToArray();
                        object texture = viewers.Length == 1 ? FieldSnapshot.ResolveFields(FieldSnapshot.ResolveFields(viewers[0], "renderImage"), "m_Texture") : null;
                        selectedSaved = HistoryImageBinding.Find(progress, texture) ?? -1;
                    }
                    if (selectedSaved < -1) throw new InvalidOperationException("Invalid saved dress index");
                    if (selectedSaved >= 0)
                    {
                        var record = FieldSnapshot.ListItemAt(FieldSnapshot.ResolveFields(progress, "completedQuests"), selectedSaved);
                        if (record == null) throw new InvalidOperationException("Selected history record unavailable: " + selectedSaved);
                        selectedRecord = record; state = FieldSnapshot.ResolveFields(record, "dress"); source = "PlayerProgress.completedQuests[" + selectedSaved + "].dress";
                    }
                    else state = FieldSnapshot.ResolveFields(manager, "activeDress", "state");
                    capture.Write("selection.json", new SelectionDiagnostic { requestedSavedIndex = requestedSaved, resolvedSavedIndex = selectedSaved,
                        requestedOrderIndex = selectedOrder, source = source, progressStatus = progressStatus, stateType = state?.GetType().FullName });
                });
                capture.Stage("garment", () => {
                    var roots = GameRuntimeReader.GarmentGraph(state, source, FieldSnapshot.ResolveFields(manager, "<CurrentScene>k__BackingField"));
                    capture.Write("garment-raw.json", roots);
                    input.garment = new GarmentSnapshotBuilder(roots).Build(input.capturedAtUtc, SceneManager.GetActiveScene().name, source);
                    input.assetNames = GameRuntimeReader.AssetNames(state);
                    capture.Write("garment.json", input.garment);
                });
                if (selectedOrder >= 0) input.order.association = "explicit accepted-order index " + selectedOrder;
                capture.Stage("orders-and-inventory", () => {
                    var context = RuntimeContextCapture.Capture(GameRuntimeReader.GameType("PlayerProgress"), input.capturedAtUtc, SceneManager.GetActiveScene().name, selectedOrder >= 0 ? (int?)selectedOrder : null);
                    capture.Write("order-raw.json", context);
                    var normalized = ProgressSnapshotBuilder.Build(context, selectedOrder >= 0 ? (int?)selectedOrder : null);
                    capture.Write("order.json", normalized);
                    capture.Write("inventory.json", normalized.unlocked);
                    if (selectedOrder >= 0)
                    {
                        input.order.association = "explicit accepted-order index " + selectedOrder;
                        if (normalized.orderStatus != "selected") throw new InvalidOperationException("Explicit order unavailable");
                        input.order.definition = normalized.order; input.order.budget = normalized.budget;
                        input.order.deterministicRequirements = normalized.completionRequirements;
                        var record = FieldSnapshot.ListItemAt(FieldSnapshot.ResolveFields(progress, "activeCommissions"), selectedOrder);
                        var definition = FieldSnapshot.ResolveFields(record, "questDefinition") as UnityEngine.Object;
                        input.order.brief = EvaluationBrief.From(BriefReferenceCatalog.Bundled.Find(normalized.order?.guid, definition == null ? null : definition.name, normalized.order?.prettyName, Application.version));
                    }
                });
                capture.Stage("targeted-api-metadata", () => capture.Write("targeted-api.json", GameRuntimeReader.Metadata()));
                capture.Stage("saved-garment-candidates", () => capture.Write("history-candidates.json", GameRuntimeReader.HistorySnapshots(progress, input.capturedAtUtc, SceneManager.GetActiveScene().name, FieldSnapshot.ResolveFields(manager, "<CurrentScene>k__BackingField"))));
                capture.Stage("viewer-selection-probe", () => capture.Write("viewer-selection.json", GameRuntimeReader.SelectionProbe(progress)));
                capture.Stage("images", () => {
                    var visual = GameImageCapture.Capture(capture.directory, manager, state, selectedSaved >= 0 || state == null, FieldSnapshot.ResolveFields(selectedRecord, "thumbnailTex") as Texture);
                    input.limitations.Add(visual.metadata.uiStatus); input.limitations.Add(visual.metadata.viewsStatus);
                    foreach (string file in visual.metadata.files)
                        input.images.Add(new EvaluationImage { file = file, sha256 = CaptureSession.Hash(Path.Combine(capture.directory, file)), width = visual.metadata.width, height = visual.metadata.height,
                            source = visual.metadata.source, binding = visual.directActiveBinding ? "active-state-verified" : visual.exactHistoryTexture && FieldSnapshot.ResolveFields(selectedRecord, "thumbnailDirty") is bool && !(bool)FieldSnapshot.ResolveFields(selectedRecord, "thumbnailDirty") && ReferenceEquals(FieldSnapshot.ResolveFields(selectedRecord, "dress"), state) ? "history-thumbnail-reference-verified" : "unverified" });
                    object afterState = selectedSaved >= 0 ? FieldSnapshot.ResolveFields(selectedRecord, "dress") : FieldSnapshot.ResolveFields(manager, "activeDress", "state");
                    var afterRoots = GameRuntimeReader.GarmentGraph(afterState, source, FieldSnapshot.ResolveFields(manager, "<CurrentScene>k__BackingField"));
                    var after = new GarmentSnapshotBuilder(afterRoots).Build(input.capturedAtUtc, SceneManager.GetActiveScene().name, source);
                    if (!ReferenceEquals(afterState, state) || DiagnosticJson.Serialize(after) != DiagnosticJson.Serialize(input.garment))
                    {
                        foreach (var image in input.images) image.binding = "unverified";
                        input.limitations.Add("garment-state-changed-during-render");
                        capture.Write("garment-after-render.json", after);
                    }
                });
                bool backendValid = capture.Stage("backend-configuration", () => capture.Write("backend.json", new BackendDiagnostic {
                    model = selectedModel, endpoint = LocalEvaluator.ValidateEndpoint(selectedEndpoint).AbsoluteUri,
                    configured = !string.IsNullOrWhiteSpace(selectedModel) }));
                capture.Write("evaluation-input.json", input);
                capture.manifest.readiness = EvaluationReadiness.Check(input, selectedModel);
                if (!backendValid) { capture.manifest.readiness.blockers.Add("invalid-model-endpoint"); capture.manifest.readiness.canEvaluate = false; }
                capture.Write("readiness.json", capture.manifest.readiness);
                capture.Write("manifest.json", capture.manifest);
                // All Unity reads/rendering above run synchronously on the game thread.
                // Only local HTTP and file I/O run after this await; no background game scans.
                if (backendValid)
                {
                    await capture.StageAsync("local-server-metadata", async () => {
                        using (var handler = new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false })
                        using (var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(3) })
                        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
                        {
                            timeout.CancelAfter(TimeSpan.FromSeconds(3));
                            string metadata = await LocalEvaluator.Probe(client, selectedEndpoint, timeout.Token);
                            File.WriteAllText(Path.Combine(capture.directory, "local-models.json"), metadata);
                        }
                    });
                }
                if (capture.manifest.readiness.canEvaluate)
                {
                    await capture.StageAsync("local-inference", async () => {
                        var request = ModelRequest.Build(input, selectedModel, image => File.ReadAllBytes(Path.Combine(capture.directory, image.file)));
                        string json = DiagnosticJson.Serialize(request);
                        capture.Write("request-audit.json", new RequestDiagnostic { model = selectedModel, imageCount = input.images.Count,
                            requestBytes = System.Text.Encoding.UTF8.GetByteCount(json), systemPrompt = request.messages[0].content[0].text });
                        using (var handler = new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false })
                        using (var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(90) })
                        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
                        {
                            timeout.CancelAfter(TimeSpan.FromSeconds(90));
                            string assessment = await LocalEvaluator.Run(client, selectedEndpoint, json, input, timeout.Token,
                                response => File.WriteAllText(Path.Combine(capture.directory, "model-response.json"), response));
                            File.WriteAllText(Path.Combine(capture.directory, "assessment.json"), assessment);
                        }
                    });
                }
                else capture.manifest.stages.Add(new CaptureStage { name = "local-inference", status = "blocked", detail = string.Join(",", capture.manifest.readiness.blockers) });
                status = "Пакет: " + id + "\n" + (capture.manifest.readiness.canEvaluate ? "Проверьте этап local-inference в manifest.json." : "Блокеры: " + string.Join(", ", capture.manifest.readiness.blockers));
            }
            catch (Exception e) { status = "Сбой сбора: " + e.GetType().Name; Logger.LogError(e); }
            finally
            {
                try { capture?.Finish(); } catch (Exception e) { Logger.LogError(e); }
                busy = false;
            }
        }
        private void OnDestroy() { lifetime.Cancel(); command?.Dispose(); }
    }
}
