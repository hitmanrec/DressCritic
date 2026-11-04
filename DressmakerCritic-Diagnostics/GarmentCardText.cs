using System;
using System.Linq;
using System.Text;

namespace DressmakerCritic.Diagnostics
{
    // Product text derived from facts only. It never computes game scores or
    // associates a finished garment with the selected order.
    public static class GarmentCardText
    {
        private static string Flag(bool? value) => value.HasValue ? value.Value ? "yes" : "no" : "unknown";
        private static string Asset(AssetIdentity value) => value == null || value.status == "unavailable" ? "unknown" : value.status == "null" ? "none" :
            !string.IsNullOrEmpty(value.prettyName) ? value.prettyName : !string.IsNullOrEmpty(value.guid) ? "ID " + value.guid : "unknown (details unavailable)";
        private static string Count(int known, int? total, int unknown) => known + "/" + (total.HasValue ? total.ToString() : "?") + (unknown > 0 ? " (unknown: " + unknown + ")" : "");
        public static string Build(GarmentSnapshot garment, ProgressSnapshot progress)
        {
            var text = new StringBuilder();
            text.AppendLine("Read-only snapshot - " + (garment?.capturedAtUtc ?? "time unknown"));
            text.AppendLine("Screen: " + (garment?.screen ?? "unknown"));
            text.AppendLine();
            if (garment == null || garment.garmentStatus != "available")
                text.AppendLine(garment?.garmentStatus == "absent" ? "No active garment. Open a garment in the game's viewer/editor, then refresh." : "Garment data unavailable. Refresh after opening the garment.");
            else
            {
                text.AppendLine(garment.source == "GameManager.activeDress.state" ? "ACTIVE GARMENT" : "EXPLICITLY SELECTED SAVED GARMENT");
                text.AppendLine("Sewing complete: " + Flag(garment.sewingComplete) + " | Finished: " + Flag(garment.finished));
                foreach (var component in garment.components)
                    text.AppendLine(component.slot + ": " + Asset(component.definition) + " | variant " + (component.variant?.ToString() ?? "unknown"));
                text.AppendLine();
                text.AppendLine("CONSTRUCTION");
                text.AppendLine("Cut: " + Count(garment.panels.Count(p => p.hasBeenCut == true), garment.panelCount, garment.panels.Count(p => p.hasBeenCut == null)));
                text.AppendLine("On mannequin: " + Count(garment.panels.Count(p => p.placedOnMannequin == true), garment.panelCount, garment.panels.Count(p => p.placedOnMannequin == null)));
                text.AppendLine("Stitched: " + Count(garment.panels.Count(p => p.hasBeenStitched == true), garment.panelCount, garment.panels.Count(p => p.hasBeenStitched == null)));
                text.AppendLine("Panel list: " + garment.panelsStatus + " | observed " + garment.panels.Count);
                text.AppendLine("Fabrics (observed panels):");
                foreach (var group in garment.panels.GroupBy(p => Asset(p.fabric))) text.AppendLine("  " + group.Key + ": " + group.Count());
                text.AppendLine("Accessories: " + (garment.accessoryCount?.ToString() ?? "unknown") + " | observed " + garment.accessories.Count + " | " + garment.accessoriesStatus);
                foreach (var group in garment.accessories.GroupBy(a => Asset(a.definition))) text.AppendLine("  " + group.Key + ": " + group.Count());
                text.AppendLine("Accessory details/colours may be unavailable. No visual assessment yet.");
            }
            text.AppendLine();
            text.AppendLine("CURRENT ORDER - REFERENCE ONLY");
            text.AppendLine("This garment has not been linked to this order. No compliance verdict is computed.");
            if (progress?.orderStatus == "none") text.AppendLine("No accepted order selected.");
            else if (progress == null || progress.orderStatus != "selected") text.AppendLine("Order data unavailable: " + (progress?.progressAccessStatus ?? "unknown"));
            else
            {
                text.AppendLine(Asset(progress.order) + " | Budget: " + (progress.budget?.ToString() ?? "unknown"));
                foreach (var requirement in progress.completionRequirements)
                {
                    string target = requirement.type == "TagScoreRequirement" ? Asset(requirement.tag) : requirement.type == "QualityRequirement" ? "Game quality" : "Uninterpreted game requirement";
                    string value = requirement.threshold?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? requirement.targetScore?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unknown";
                    text.AppendLine("  " + target + " " + (requirement.comparison == "AtLeast" ? ">=" : requirement.comparison ?? "unknown comparison") + " " + value + (requirement.gated == false ? "" : " (conditional/unknown gate)"));
                }
                text.AppendLine("Requirement list: " + progress.completionRequirementsStatus);
            }
            text.AppendLine("Actual game scores and artistic brief interpretation are unavailable.");
            text.AppendLine();
            text.AppendLine("UNLOCKED MEMBERSHIP");
            if (progress?.unlocked != null)
            {
                foreach (var section in new[] { new { name = "Patterns", data = progress.unlocked.patterns }, new { name = "Fabrics", data = progress.unlocked.fabrics }, new { name = "Accessories", data = progress.unlocked.accessories } })
                    text.AppendLine(section.name + ": " + (section.data?.totalCount?.ToString() ?? "unknown") + " | " + (section.data?.status ?? "unavailable"));
            }
            text.AppendLine("Unlocks do not prove owned stock, affordability, compatibility or possible edits.");
            return text.ToString();
        }
    }
}
