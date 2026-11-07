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
    [BepInPlugin("community.dressmakercritic.diagnostics", "Dressmaker Critic", "0.15.6")]
    [BepInDependency("yuu.dressmaker.mods", "0.7.8")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private ConfigEntry<int> savedRecord, orderIndex, inferenceTimeout;
        private ConfigEntry<string> model, endpoint;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private IDisposable command;
        private volatile bool busy;
        private readonly EvaluationPanel panel = new EvaluationPanel();
        private volatile string status = "";
        private void Awake()
        {
            savedRecord = Config.Bind("Selection", "SavedDressIndex", -2, "-2: unique display-state or thumbnail reference then active fallback; -1: active dress; otherwise zero-based completedQuests record index. Does not modify the game.");
            orderIndex = Config.Bind("Selection", "OrderIndex", -1, "Active dress only: -1 means no order; otherwise explicit accepted-order index. Ignored for history dresses, which use their own saved quest record.");
            model = Config.Bind("Inference", "Model", "", "Exact model ID from your local multimodal server; blank prevents inference.");
            endpoint = Config.Bind("Inference", "Endpoint", "http://127.0.0.1:11434/v1/chat/completions", "Loopback OpenAI-compatible vision endpoint only. No hosted API or embedded credentials.");
            inferenceTimeout = Config.Bind("Inference", "TimeoutSeconds", 300, "Per-request inference deadline, clamped to 30-600 seconds; cold model loading may take longer than a warm request.");
            Logger.LogInfo("Dressmaker Critic 0.15.6: native left-page layout, Photo Mode button styling and tag list clipping.");
        }
        private void Start()
        {
            command = DebugConsoleApi.Register("critic-export", "Capture one complete evaluation diagnostic package", _ => { BeginCapture(); return "Evaluation diagnostic capture started"; });
        }
        private void Update()
        {
            panel.Tick(BeginCapture, busy);
        }
        private async void BeginCapture()
        {
            if (busy) return;
            long displayTicket = panel.Begin();
            busy = true; status = "Collecting garment data, image and evaluation blockers.";
            CaptureSession capture = null;
            object state = null;
            try
            {
                string id = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                capture = new CaptureSession(Path.Combine(Paths.ConfigPath, "DressmakerCritic", "captures", id), id);
                var input = new EvaluationInput { captureId = id, capturedAtUtc = capture.manifest.startedAtUtc };
                object progress = null, selectedRecord = null; Component manager = null;
                string progressStatus = "not-read", source = "GameManager.activeDress.state";
                string selectedModel = model.Value, selectedEndpoint = endpoint.Value;
                int selectedSaved = savedRecord.Value, selectedOrder = orderIndex.Value;
                int requestedSaved = selectedSaved;
                int timeoutSeconds = Math.Min(600, Math.Max(30, inferenceTimeout.Value));
                capture.Stage("history-ui-layout", () => {
                    capture.Write("ui-layout.json", panel.CaptureLayout());
                    capture.Write("ui-layout-original.json", panel.OriginalLayout());
                });
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
                        var display = GameRuntimeReader.DisplayBinding(progress);
                        capture.Write("display-binding.json", display);
                        selectedSaved = display.historyIndex ?? HistoryImageBinding.Find(progress, texture) ?? -1;
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
                if (selectedSaved >= 0)
                {
                    capture.Stage("historical-order", () => {
                        var historical = HistoryOrder.Capture(selectedRecord, state, selectedSaved, Application.version);
                        var definition = FieldSnapshot.ResolveFields(selectedRecord, "questDefinition") as UnityEngine.Object;
                        historical.assetKey = definition == null ? null : definition.name;
                        capture.Write("history-order.json", historical);
                        panel.SetHistoryOrigin(displayTicket, state, historical);
                        if (historical.status != "record-state-verified") throw new InvalidOperationException(historical.status);
                        if (historical.origin == "off-rack") return;
                        input.order.association = historical.association;
                        input.order.definition = historical.definition;
                        input.order.budget = historical.budget;
                        input.order.deterministicRequirements = historical.requirements;
                        input.customer.customerId = historical.recipientId ?? historical.questGiverId;
                        input.order.brief = EvaluationBrief.From(BriefReferenceCatalog.Bundled.Find(historical.definition?.guid,
                            historical.assetKey, historical.definition?.prettyName, Application.version));
                    });
                }
                if (selectedSaved < 0 && selectedOrder >= 0) input.order.association = "explicit accepted-order index " + selectedOrder;
                capture.Stage("orders-and-inventory", () => {
                    var context = RuntimeContextCapture.Capture(GameRuntimeReader.GameType("PlayerProgress"), input.capturedAtUtc, SceneManager.GetActiveScene().name, selectedOrder >= 0 ? (int?)selectedOrder : null);
                    capture.Write(selectedSaved >= 0 ? "active-orders-raw.json" : "order-raw.json", context);
                    var normalized = ProgressSnapshotBuilder.Build(context, selectedOrder >= 0 ? (int?)selectedOrder : null);
                    capture.Write(selectedSaved >= 0 ? "active-order.json" : "order.json", normalized);
                    capture.Write("inventory.json", normalized.unlocked);
                    // Active commissions are only an explicit development option for an active dress.
                    // A saved dress always uses its own historical record, never today's commission.
                    if (selectedSaved < 0 && selectedOrder >= 0)
                    {
                        input.order.association = "explicit accepted-order index " + selectedOrder;
                        if (normalized.orderStatus != "selected") throw new InvalidOperationException("Explicit order unavailable");
                        input.order.definition = normalized.order; input.order.budget = normalized.budget;
                        input.order.deterministicRequirements = normalized.completionRequirements;
                        var record = FieldSnapshot.ListItemAt(FieldSnapshot.ResolveFields(progress, "activeCommissions"), selectedOrder);
                        input.customer.customerId = FieldSnapshot.ResolveFields(record, "questDefinition", "questGiverCharacter", "_guid") as string;
                        var definition = FieldSnapshot.ResolveFields(record, "questDefinition") as UnityEngine.Object;
                        input.order.brief = EvaluationBrief.From(BriefReferenceCatalog.Bundled.Find(normalized.order?.guid, definition == null ? null : definition.name, normalized.order?.prettyName, Application.version));
                    }
                });
                capture.Stage("targeted-api-metadata", () => capture.Write("targeted-api.json", GameRuntimeReader.Metadata()));
                capture.Stage("saved-garment-candidates", () => capture.Write("history-candidates.json", GameRuntimeReader.HistorySnapshots(progress, input.capturedAtUtc, SceneManager.GetActiveScene().name, FieldSnapshot.ResolveFields(manager, "<CurrentScene>k__BackingField"))));
                capture.Stage("viewer-selection-probe", () => capture.Write("viewer-selection.json", GameRuntimeReader.SelectionProbe(progress)));
                string imageBinding = "unverified";
                capture.Stage("images", () => {
                    var beforeDisplay = GameRuntimeReader.DisplayBinding(progress, state);
                    var visual = GameImageCapture.Capture(capture.directory, manager, state, selectedSaved >= 0 || state == null, FieldSnapshot.ResolveFields(selectedRecord, "thumbnailTex") as Texture);
                    var afterDisplay = GameRuntimeReader.DisplayBinding(progress, state);
                    capture.Write("display-binding-after-image.json", afterDisplay);
                    bool displayVerified = selectedSaved >= 0 && beforeDisplay.verified && afterDisplay.verified &&
                        beforeDisplay.historyIndex == selectedSaved && afterDisplay.historyIndex == selectedSaved &&
                        beforeDisplay.displayDressId == afterDisplay.displayDressId && beforeDisplay.cameraId == afterDisplay.cameraId &&
                        beforeDisplay.viewerTextureId == afterDisplay.viewerTextureId;
                    imageBinding = displayVerified ? "history-display-chain-verified" : visual.directActiveBinding ? "active-state-verified" : visual.exactHistoryTexture && FieldSnapshot.ResolveFields(selectedRecord, "thumbnailDirty") is bool && !(bool)FieldSnapshot.ResolveFields(selectedRecord, "thumbnailDirty") && ReferenceEquals(FieldSnapshot.ResolveFields(selectedRecord, "dress"), state) ? "history-thumbnail-reference-verified" : "unverified";
                    input.limitations.Add(visual.metadata.uiStatus); input.limitations.Add(visual.metadata.viewsStatus);
                    foreach (string file in visual.metadata.files)
                        input.images.Add(new EvaluationImage { file = file, sha256 = CaptureSession.Hash(Path.Combine(capture.directory, file)), width = visual.metadata.width, height = visual.metadata.height,
                            source = visual.metadata.source, binding = imageBinding });
                    object afterState = selectedSaved >= 0 ? FieldSnapshot.ResolveFields(selectedRecord, "dress") : FieldSnapshot.ResolveFields(manager, "activeDress", "state");
                    var afterRoots = GameRuntimeReader.GarmentGraph(afterState, source, FieldSnapshot.ResolveFields(manager, "<CurrentScene>k__BackingField"));
                    var after = new GarmentSnapshotBuilder(afterRoots).Build(input.capturedAtUtc, SceneManager.GetActiveScene().name, source);
                    if (!ReferenceEquals(afterState, state) || DiagnosticJson.Serialize(after) != DiagnosticJson.Serialize(input.garment))
                    {
                        foreach (var image in input.images) image.binding = "unverified";
                        imageBinding = "unverified";
                        input.limitations.Add("garment-state-changed-during-render");
                        capture.Write("garment-after-render.json", after);
                    }
                });
                capture.Stage("accessory-render-probe", () => capture.Write("accessory-render.json", GameRuntimeReader.AccessoryRenderProbe(progress)));
                capture.Stage("fixed-view-rendering", () => {
                    FixedViewCapture.Capture(capture.directory, state, progress);
                    input.images.Clear();
                    var axes = new[] { "plus-z", "minus-z", "plus-x", "minus-x" };
                    var labels = new[] { "front (+Z)", "back (-Z)", "side A (+X)", "side B (-X)" };
                    for (int i = 0; i < axes.Length; i++)
                    {
                        string file = "fixed-axis-" + axes[i] + ".png";
                        input.images.Add(new EvaluationImage { file = file, sha256 = CaptureSession.Hash(Path.Combine(capture.directory, file)),
                            width = 768, height = 768, source = "fixed mannequin-local orthographic camera", view = labels[i], binding = imageBinding });
                    }
                    // Capture still writes fixed-view PNGs for the model; the jury panel no longer shows a redundant preview.
                    var afterFixed = new GarmentSnapshotBuilder(GameRuntimeReader.GarmentGraph(state, source, FieldSnapshot.ResolveFields(manager, "<CurrentScene>k__BackingField")))
                        .Build(input.capturedAtUtc, SceneManager.GetActiveScene().name, source);
                    if (DiagnosticJson.Serialize(afterFixed) != DiagnosticJson.Serialize(input.garment))
                    {
                        capture.Write("garment-after-fixed-views.json", afterFixed);
                        throw new InvalidOperationException("Garment fields changed during fixed-view rendering");
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
                JuryAssessment juryResult = null;
                if (capture.manifest.readiness.canEvaluate)
                {
                    await capture.StageAsync("local-jury-inference", async () => {
                        var request = ModelRequest.Build(input, selectedModel, image => File.ReadAllBytes(Path.Combine(capture.directory, image.file)));
                        string json = DiagnosticJson.Serialize(request);
                        capture.Write("jury-request-audit.json", new RequestDiagnostic { model = selectedModel, imageCount = input.images.Count,
                            reasoningEffort = request.reasoning_effort, maxOutputTokens = request.max_tokens, responseFormat = request.response_format.type,
                            timeoutSeconds = timeoutSeconds, requestBytes = System.Text.Encoding.UTF8.GetByteCount(json), systemPrompt = request.messages[0].content[0].text });
                        using (var handler = new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false })
                        using (var client = new HttpClient(handler) { Timeout = System.Threading.Timeout.InfiniteTimeSpan })
                        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
                        {
                            timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
                            string assessment = await LocalEvaluator.Run(client, selectedEndpoint, json, input, timeout.Token,
                                response => File.WriteAllText(Path.Combine(capture.directory, "jury-model-response.json"), response));
                            juryResult = JuryContract.ReadJury(assessment, input.captureId);
                            capture.Write("assessment.json", juryResult);
                        }
                    });
                }
                else capture.manifest.stages.Add(new CaptureStage { name = "local-jury-inference", status = "blocked", detail = string.Join(",", capture.manifest.readiness.blockers) });
                if (juryResult != null && JuryContract.NeedsContext(input))
                {
                    await capture.StageAsync("local-context-inference", async () => {
                        var request = ModelRequest.Build(input, selectedModel, image => File.ReadAllBytes(Path.Combine(capture.directory, image.file)), EvaluationTask.Context);
                        string json = DiagnosticJson.Serialize(request);
                        capture.Write("context-request-audit.json", new RequestDiagnostic { model = selectedModel, imageCount = input.images.Count,
                            reasoningEffort = request.reasoning_effort, maxOutputTokens = request.max_tokens, responseFormat = request.response_format.type,
                            timeoutSeconds = timeoutSeconds, requestBytes = System.Text.Encoding.UTF8.GetByteCount(json), systemPrompt = request.messages[0].content[0].text });
                        using (var handler = new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false })
                        using (var client = new HttpClient(handler) { Timeout = System.Threading.Timeout.InfiniteTimeSpan })
                        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
                        {
                            timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
                            string response = await LocalEvaluator.Run(client, selectedEndpoint, json, input, timeout.Token,
                                raw => File.WriteAllText(Path.Combine(capture.directory, "context-model-response.json"), raw), EvaluationTask.Context);
                            JuryContract.ApplyContext(response, input, juryResult);
                            capture.Write("assessment.json", juryResult);
                        }
                    });
                }
                else capture.manifest.stages.Add(new CaptureStage { name = "local-context-inference", status = "skipped", detail = juryResult == null ? "Jury assessment unavailable" : "No verified order or customer context; jury remains independent" });
                var juryStage = capture.manifest.stages.LastOrDefault(s => s.name == "local-jury-inference");
                var contextStage = capture.manifest.stages.LastOrDefault(s => s.name == "local-context-inference");
                panel.Complete(displayTicket, state, juryResult, juryResult == null
                    ? (capture.manifest.readiness.canEvaluate ? "status.failed" : "status.blocked")
                    : (contextStage?.status == "failed" ? "status.done.context-failed" : "status.done"));
                capture.Stage("history-ui-result-layout", () => capture.Write("ui-layout-after.json", panel.CaptureLayout()));
                status = "Package: " + id + "\n" + (capture.manifest.readiness.canEvaluate ? "Check jury/context stages in manifest.json." : "Blockers: " + string.Join(", ", capture.manifest.readiness.blockers));
                if (juryResult == null && juryStage != null) Logger.LogWarning("Jury stage: " + juryStage.status + " " + (juryStage.detail ?? ""));
                if (contextStage != null && contextStage.status == "failed") Logger.LogWarning("Context stage failed; jury result kept.");
            }
            catch (Exception e) { panel.Complete(displayTicket, state, null, "status.prepare-failed"); status = "Capture failed: " + e.GetType().Name; Logger.LogError(e); }
            finally
            {
                try { capture?.Finish(); } catch (Exception e) { Logger.LogError(e); }
                busy = false;
            }
        }
        private void OnDestroy() { lifetime.Cancel(); command?.Dispose(); panel.Dispose(); }
    }
}
