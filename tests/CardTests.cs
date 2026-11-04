using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DressmakerCritic.Diagnostics;

static class CardTests
{
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
    public static void Run()
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var options = new JsonSerializerOptions { IncludeFields = true };
        string prefix = Path.Combine(root, "DressmakerCritic-Diagnostics/exports/captures");
        var input = JsonSerializer.Deserialize<SnapshotTests.Input>(File.ReadAllText(Path.Combine(prefix, "20261104-122108-178-1e6bac1a/editor-runtime.json")), options);
        var garment = new GarmentSnapshotBuilder(input.objects).Build(input.timeUtc, input.scene);
        string text = GarmentCardText.Build(garment, null);
        Check(text.Contains("Сшито: 7/14") && text.Contains("На манекене: 14/14") && text.Contains("Burlap: 14"), "card shows actual partial garment construction and fabric");
        var context = JsonSerializer.Deserialize<ContextRuntime>(File.ReadAllText(Path.Combine(prefix, "20261104-125434-693-79d9b746/context-runtime.json")), options);
        var order = ProgressSnapshotBuilder.Build(context);
        text = GarmentCardText.Build(garment, order, true);
        Check(text.Contains("Elegant >= 50") && text.Contains("Daywear >= 50") && text.Contains("Simple >= 25"), "card displays known order requirements");
        Check(text.Contains("ВЫБРАННЫЙ ВАМИ ЗАКАЗ") && text.Contains("Вердикт о выполнении заказа не вычисляется") && text.Contains("визуальная оценка пока недоступны"), "card does not assign a historical garment to current order");
        Check(text.Contains("Выкройки: 72 | список полный") && text.Contains("Игровые очки, художественное задание и визуальная оценка пока недоступны"), "card separates unlocked membership and unknown scores");
        var finished = JsonSerializer.Deserialize<GarmentSnapshot>(File.ReadAllText(Path.Combine(root, "docs/examples/garment-snapshot.json")), options);
        text = GarmentCardText.Build(finished, order, true);
        Check(text.Contains("Платье завершено: да") && text.Contains("АКСЕССУАРЫ: 7") && text.Contains("неизвестно (детали недоступны)"), "finished dress shown with unknown accessory identities");
        text = GarmentCardText.Build(new GarmentSnapshot { garmentStatus = "absent" }, order, true);
        Check(text.Contains("Нет активного платья") && text.Contains("Undercover Servant") && !text.Contains("Вырезано: 0/"), "no garment still shows order without fake empty garment");
        text = GarmentCardText.Build(new GarmentSnapshot { garmentStatus = "available", panelsStatus = "truncated", panelCount = 100 }, new ProgressSnapshot { orderStatus = "unavailable", progressAccessStatus = "blocked" });
        Check(text.Contains("Панели: список неполный; прочитано 0") && text.Contains("ЗАКАЗ НЕ ВЫБРАН"), "card exposes incomplete data and blocked progress");
    }
}
