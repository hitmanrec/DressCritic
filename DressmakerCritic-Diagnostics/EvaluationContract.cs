using System;
using System.Collections.Generic;
using System.Linq;

namespace DressmakerCritic.Diagnostics
{
    [Serializable] public sealed class EvaluationBrief
    {
        public string status = "missing", summary, sourceUrl, coverage;
        public List<string> requests = new List<string>(), constraints = new List<string>(), uncertainties = new List<string>();
        public static EvaluationBrief From(BriefReferenceMatch match)
        {
            if (match == null || match.entry == null) return new EvaluationBrief { status = match?.status ?? "missing" };
            return new EvaluationBrief { status = match.status, summary = match.entry.summary, sourceUrl = match.entry.sourceUrl,
                coverage = match.entry.dialogueCoverage, requests = (match.entry.requests ?? new string[0]).ToList(),
                constraints = (match.entry.prohibitions ?? new string[0]).ToList(), uncertainties = (match.entry.uncertainties ?? new string[0]).ToList() };
        }
    }
    [Serializable] public sealed class EvaluationOrder
    {
        public string association = "none: no order selected for evaluation";
        public AssetIdentity definition;
        public int? budget;
        public List<OrderRequirement> deterministicRequirements = new List<OrderRequirement>();
        public EvaluationBrief brief = new EvaluationBrief();
    }
    [Serializable] public sealed class EvaluationImage
    {
        public string file, sha256, source, binding;
        public int width, height;
    }
    [Serializable] public sealed class EvaluationAssetName { public string guid, assetName, runtimeType; }
    [Serializable] public sealed class CustomerEvidence
    {
        public string status = "unknown", source, customerId;
        public List<string> preferences = new List<string>();
    }
    [Serializable] public sealed class EvaluationInput
    {
        public string schemaVersion = "2.0.0", captureId, capturedAtUtc;
        public GarmentSnapshot garment;
        public EvaluationOrder order = new EvaluationOrder();
        public CustomerEvidence customer = new CustomerEvidence();
        public List<EvaluationImage> images = new List<EvaluationImage>();
        public List<EvaluationAssetName> assetNames = new List<EvaluationAssetName>();
        public string editorCapabilities = "unknown: unlocked membership is not evidence of owned stock or supported editing operations";
        public bool actionRecommendationsAllowed = false;
        public string deterministicScores = "unknown: game scoring methods have not been evaluated";
        public List<string> limitations = new List<string>();
    }
    [Serializable] public sealed class EvaluationReadiness
    {
        public List<string> blockers = new List<string>(), limitations = new List<string>();
        public bool canEvaluate;
        public static EvaluationReadiness Check(EvaluationInput input, string model)
        {
            var result = new EvaluationReadiness();
            if (input.garment == null || input.garment.garmentStatus != "available") result.blockers.Add("garment-unavailable");
            if (input.images.Count == 0) result.blockers.Add("image-unavailable");
            if (input.images.Any(i => string.IsNullOrEmpty(i.file) || string.IsNullOrEmpty(i.sha256) || i.width < 1 || i.height < 1 || i.width > 2048 || i.height > 2048)) result.blockers.Add("image-metadata-incomplete");
            if (input.images.Any(i => i.binding != "history-display-chain-verified" && i.binding != "active-state-verified" && i.binding != "history-thumbnail-reference-verified")) result.blockers.Add("image-garment-identity-unverified");
            if (input.order.association != "none: no order selected for evaluation" && input.order.definition == null) result.limitations.Add("explicit-order-unavailable");
            if (string.IsNullOrWhiteSpace(model)) result.blockers.Add("local-model-not-configured");
            if (input.order.association != "none: no order selected for evaluation" && input.order.brief.status != "verified-guid" && input.order.brief.status != "asset-key")
                result.limitations.Add("semantic-order-compliance-unknown");
            result.limitations.Add("deterministic-game-compliance-not-evaluated");
            result.limitations.Add("edit-recommendations-disabled-until-editor-capabilities-verified");
            result.canEvaluate = result.blockers.Count == 0;
            return result;
        }
    }
    [Serializable] public sealed class SelectionDiagnostic
    {
        public int requestedSavedIndex, resolvedSavedIndex, requestedOrderIndex;
        public string source, progressStatus, stateType;
    }
    [Serializable] public sealed class BackendDiagnostic { public string model, endpoint; public bool configured; }
    [Serializable] public sealed class RequestDiagnostic { public string model, systemPrompt, reasoningEffort, responseFormat; public int imageCount, requestBytes, timeoutSeconds, maxOutputTokens; }
    [Serializable] public sealed class CaptureStage
    {
        public string name, status, detail;
        public double elapsedMs;
    }
    [Serializable] public sealed class CaptureFile
    {
        public string file, sha256;
        public int bytes;
    }
    [Serializable] public sealed class CaptureManifest
    {
        public string schemaVersion = "1.0.0", captureId, startedAtUtc, finishedAtUtc;
        public string pluginVersion = "0.10.0";
        public bool complete;
        public List<CaptureStage> stages = new List<CaptureStage>();
        public List<CaptureFile> files = new List<CaptureFile>();
        public EvaluationReadiness readiness;
    }
}
