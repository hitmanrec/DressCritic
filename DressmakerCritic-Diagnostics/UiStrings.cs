using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using BepInEx;

namespace DressmakerCritic.Diagnostics
{
    // English is the built-in UI language. A future Russian (or other) patch drops a
    // locale overlay JSON next to the plugin config; scoring IDs and model prompts stay English.
    internal static class UiStrings
    {
        private static readonly Dictionary<string, string> En = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tab"] = "Jury",
            ["tab.open"] = "Jury ▾",
            ["title"] = "Salon · Fashion House Jury",
            ["evaluate"] = "Rate dress",
            ["evaluating"] = "Judging…",
            ["close"] = "Close",
            ["houses"] = "Houses",
            ["order"] = "Order",
            ["origin.off-rack"] = "Off-rack dress · no commission",
            ["origin.commission"] = "Historical commission",
            ["origin.unknown"] = "Dress origin not verified",
            ["snapshot.notice"] = "Snapshot at evaluation start. A new rating is required for a different or edited dress.",
            ["mean"] = "Jury mean",
            ["divided"] = "The jury is divided.",
            ["order.context"] = "Order context",
            ["order.compliance"] = "Order fit",
            ["order.brief-unavailable"] = "The linked brief could not be verified; extraction details were saved.",
            ["order.targets"] = "Game targets",
            ["order.targets-note"] = "Targets only; completion was not recalculated. Other goal parameters are retained in the export.",
            ["customer.prefs"] = "Client preferences",
            ["context.note"] = "Game requirements were not scored. Context is not part of the jury mean.",
            ["limitations.show"] = "Show limitations",
            ["limitations.hide"] = "Hide limitations",
            ["insufficient"] = "Insufficient evidence",
            ["verdict.exceptional"] = "Exceptional work",
            ["verdict.strong"] = "Strong work",
            ["verdict.competent"] = "Competent work",
            ["verdict.uneven"] = "Uneven work",
            ["verdict.weak"] = "Weak work",
            ["house.harmony"] = "Maison Orvelle",
            ["house.character"] = "Bellombre",
            ["house.avant_garde"] = "Virelai",
            ["house.atelier"] = "Maison Aveline",
            ["motto.harmony"] = "Every detail in its place",
            ["motto.character"] = "A look must leave an impression",
            ["motto.avant_garde"] = "The familiar is only a starting point",
            ["motto.atelier"] = "Conviction in every line",
            ["status.preparing"] = "Preparing the garment image and evidence…",
            ["status.judging"] = "The jury is reviewing the snapshot. You may close this panel; evaluation continues.",
            ["status.blocked"] = "Evaluation did not start. Reasons are saved in the diagnostic package.",
            ["status.done"] = "Snapshot evaluation complete.",
            ["status.done.context-failed"] = "Jury ready. Context scoring failed; the jury result was kept.",
            ["status.failed"] = "The local model did not return a valid assessment. Diagnostics were saved.",
            ["status.prepare-failed"] = "Could not prepare the evaluation. Diagnostics were saved.",
            ["subject.history"] = "History dress snapshot #{0} · {1}",
            ["subject.active"] = "Active dress snapshot · {0}",
        };

        private static Dictionary<string, string> overlay;
        private static bool overlayTried;

        public static string Get(string key)
        {
            EnsureOverlay();
            if (overlay != null && overlay.TryGetValue(key, out string localized) && !string.IsNullOrEmpty(localized))
                return localized;
            return En.TryGetValue(key, out string english) ? english : key;
        }

        public static string Format(string key, params object[] args) => string.Format(Get(key), args);

        public static string House(string id) => Get("house." + id);
        public static string Motto(string id) => Get("motto." + id);
        public static string Verdict(string value)
        {
            string key = "verdict." + (value ?? "");
            return En.ContainsKey(key) || (overlay != null && overlay.ContainsKey(key)) ? Get(key) : Get("insufficient");
        }

        // Optional overlay: BepInEx/config/DressmakerCritic/locale.json
        // { "tab": "Жюри", "evaluate": "Оценить платье", ... }
        private static void EnsureOverlay()
        {
            if (overlayTried) return;
            overlayTried = true;
            try
            {
                string path = Path.Combine(Paths.ConfigPath, "DressmakerCritic", "locale.json");
                if (!File.Exists(path)) return;
                string json = File.ReadAllText(path);
                using (var reader = System.Runtime.Serialization.Json.JsonReaderWriterFactory.CreateJsonReader(
                    System.Text.Encoding.UTF8.GetBytes(json), new System.Xml.XmlDictionaryReaderQuotas()))
                {
                    var root = XElement.Load(reader);
                    overlay = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var el in root.Elements())
                        if (!string.IsNullOrEmpty(el.Name.LocalName))
                            overlay[el.Name.LocalName] = el.Value;
                }
            }
            catch { overlay = null; }
        }
    }
}
