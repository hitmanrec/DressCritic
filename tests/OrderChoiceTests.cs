using System;
using System.Collections.Generic;
using DressmakerCritic.Diagnostics;

static class OrderChoiceTests
{
    class Definition { public string prettyName = "Fixture order"; }
    class Record { public Definition questDefinition = new Definition(); }
    class Progress { public List<Record> activeCommissions = new List<Record>(); }
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); Console.WriteLine("PASS " + text); }
    public static void Run()
    {
        var progress = new Progress(); progress.activeCommissions.Add(new Record()); progress.activeCommissions.Add(new Record());
        var choices = AcceptedOrderAccess.List(progress);
        Check(choices.Count == 2 && choices[1].index == 1, "all accepted orders can be explicitly selected");
        Check(AcceptedOrderAccess.IsCurrent(progress, progress, choices[1]), "selected order record identity validated");
        Check(!AcceptedOrderAccess.IsCurrent(new Progress(), progress, choices[1]), "save switch invalidates order choice");
        progress.activeCommissions[1] = new Record();
        Check(!AcceptedOrderAccess.IsCurrent(progress, progress, choices[1]), "replaced order cannot be silently selected");
        Check(AcceptedOrderAccess.List(null).Count == 0, "unavailable orders do not invent a choice");
        var order = new ProgressSnapshot { orderStatus = "selected", order = new AssetIdentity { status = "fields", prettyName = "Do not auto bind" } };
        string text = GarmentCardText.Build(new GarmentSnapshot { garmentStatus = "absent" }, order);
        Check(text.Contains("ЗАКАЗ НЕ ВЫБРАН") && !text.Contains("Do not auto bind"), "Russian report never auto-selects current order");
        var garment = new GarmentSnapshot { garmentStatus = "available", panelsStatus = "truncated", panelCount = 4 };
        garment.panels.Add(new GarmentPanel { index = 2, fabric = new AssetIdentity { status = "depth-limit" } });
        text = GarmentCardText.Build(garment, null);
        Check(text.Contains("ПАНЕЛИ ПО ОТДЕЛЬНОСТИ") && text.Contains("#3:") && text.Contains("неизвестно") && text.Contains("список неполный"), "detailed Russian report preserves unknown and partial panel data");
    }
}
