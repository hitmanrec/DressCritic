using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DressmakerCritic.Diagnostics
{
    public static class GarmentCardText
    {
        private static string Flag(bool? value) => value.HasValue ? value.Value ? "да" : "нет" : "неизвестно";
        private static string Asset(AssetIdentity asset) => asset == null || asset.status == "unavailable" ? "неизвестно" : asset.status == "null" ? "нет" :
            !string.IsNullOrEmpty(asset.prettyName) ? asset.prettyName : !string.IsNullOrEmpty(asset.guid) ? "ID " + asset.guid : "неизвестно (детали недоступны)";
        public static string Completeness(string status) => status == "complete" ? "список полный" : status == "truncated" ? "список неполный" : "данные недоступны";
        private static string Slot(string slot) => slot == "bodice" ? "Лиф" : slot == "sleeve" ? "Рукава" : slot == "collar" ? "Воротник" : slot == "skirt" ? "Юбка" : "Деталь";
        private static string Number(double? value) => value?.ToString("0.###", CultureInfo.GetCultureInfo("ru-RU")) ?? "неизвестно";
        private static string Position(Coordinate value) => value == null || !value.x.HasValue || !value.y.HasValue || !value.z.HasValue ? "неизвестно" :
            Number(value.x) + "; " + Number(value.y) + "; " + Number(value.z) + (value.w.HasValue ? "; " + Number(value.w) : "");
        private static string Count(int known, int? total, int unknown) => known + "/" + (total?.ToString() ?? "?") + (unknown > 0 ? " (неизвестно: " + unknown + ")" : "");
        public static string Build(GarmentSnapshot garment, ProgressSnapshot progress, bool explicitOrder = false)
        {
            var text = new StringBuilder();
            DateTimeOffset time;
            text.AppendLine("Снимок: " + (DateTimeOffset.TryParse(garment?.capturedAtUtc, out time) ? time.ToOffset(TimeSpan.FromHours(5)).ToString("dd.MM.yyyy HH:mm:ss") + " (UTC+5)" : "время неизвестно"));
            string screen = garment?.screen;
            text.AppendLine("Экран: " + (screen == "FrontDesk" ? "Стол заказов" : screen == "Sketchbook" ? "Дизайн" : screen == "CuttingRoom" ? "Раскрой" : screen == "Mannequin" ? "Манекен" : screen == "Photo" ? "Фото" : screen == "Store" ? "Магазин" : "не определён"));
            if (garment == null || garment.garmentStatus != "available")
                text.AppendLine(garment?.garmentStatus == "absent" ? "Нет активного платья. Откройте платье или выберите его из сохранённых, затем обновите карточку." : "Данные платья недоступны. Обновите карточку после выбора платья.");
            else
            {
                text.AppendLine(); text.AppendLine(garment.source == "GameManager.activeDress.state" ? "ТЕКУЩЕЕ ПЛАТЬЕ" : "ВЫБРАННОЕ СОХРАНЁННОЕ ПЛАТЬЕ");
                text.AppendLine("Шитьё завершено: " + Flag(garment.sewingComplete) + " | Платье завершено: " + Flag(garment.finished));
                foreach (var component in garment.components) text.AppendLine(Slot(component.slot) + ": " + Asset(component.definition) + " | вариант " + (component.variant?.ToString() ?? "неизвестно"));
                text.AppendLine(); text.AppendLine("ГОТОВНОСТЬ");
                text.AppendLine("Вырезано: " + Count(garment.panels.Count(p => p.hasBeenCut == true), garment.panelCount, garment.panels.Count(p => p.hasBeenCut == null)));
                text.AppendLine("На манекене: " + Count(garment.panels.Count(p => p.placedOnMannequin == true), garment.panelCount, garment.panels.Count(p => p.placedOnMannequin == null)));
                text.AppendLine("Сшито: " + Count(garment.panels.Count(p => p.hasBeenStitched == true), garment.panelCount, garment.panels.Count(p => p.hasBeenStitched == null)));
                text.AppendLine("Панели: " + Completeness(garment.panelsStatus) + "; прочитано " + garment.panels.Count);
                text.AppendLine("Ткани прочитанных панелей:");
                foreach (var group in garment.panels.GroupBy(p => Asset(p.fabric))) text.AppendLine("  " + group.Key + ": " + group.Count());
                text.AppendLine(); text.AppendLine("ПАНЕЛИ ПО ОТДЕЛЬНОСТИ");
                foreach (var panel in garment.panels)
                    text.AppendLine("#" + (panel.index + 1) + ": " + Asset(panel.fabric) + " | вырезана: " + Flag(panel.hasBeenCut) + ", на манекене: " + Flag(panel.placedOnMannequin) + ", сшита: " + Flag(panel.hasBeenStitched) + " | долевая: " + Number(panel.grainQuality));
                text.AppendLine("Долевая — исходное значение игры; шкала пока не проверена.");
                text.AppendLine(); text.AppendLine("АКСЕССУАРЫ: " + (garment.accessoryCount?.ToString() ?? "неизвестно") + "; " + Completeness(garment.accessoriesStatus));
                foreach (var accessory in garment.accessories)
                    text.AppendLine("#" + (accessory.index + 1) + ": " + Asset(accessory.definition) + " | размещён: " + Flag(accessory.placed) + " | масштаб: " + Number(accessory.scale) + " | позиция: " + Position(accessory.placedPos) + " | поворот: " + Position(accessory.placedRot));
                text.AppendLine(); text.AppendLine("ПОЛНОТА ДАННЫХ");
                text.AppendLine("Идентификаторы тканей: " + garment.panels.Count(p => p.fabric?.guid != null) + "/" + garment.panels.Count + " прочитанных панелей.");
                text.AppendLine("Идентификаторы аксессуаров: " + garment.accessories.Count(a => a.definition?.guid != null) + "/" + garment.accessories.Count + " прочитанных записей.");
                text.AppendLine("Названия и позиции панелей, цвета и часть деталей аксессуаров пока могут быть неизвестны. Индексы действуют только в этом снимке.");
            }
            text.AppendLine(); text.AppendLine(explicitOrder ? "ВЫБРАННЫЙ ВАМИ ЗАКАЗ" : "ЗАКАЗ НЕ ВЫБРАН");
            if (!explicitOrder) text.AppendLine("Нажмите «Заказ», чтобы явно выбрать принятый заказ. Автоматической привязки нет.");
            else if (progress == null || progress.orderStatus != "selected") text.AppendLine("Выбранный заказ недоступен. Выберите заказ заново.");
            else
            {
                text.AppendLine(Asset(progress.order) + " | бюджет: " + (progress.budget?.ToString() ?? "неизвестно"));
                foreach (var requirement in progress.completionRequirements)
                {
                    string target = requirement.type == "TagScoreRequirement" ? Asset(requirement.tag) : requirement.type == "QualityRequirement" ? "Качество по игре" : "Неразобранное требование игры";
                    text.AppendLine("  " + target + " " + (requirement.comparison == "AtLeast" ? ">=" : "условие неизвестно") + " " + Number(requirement.threshold ?? requirement.targetScore) + (requirement.gated == false ? "" : " (условное требование; применимость не проверена)"));
                }
                text.AppendLine("Требования: " + Completeness(progress.completionRequirementsStatus));
            }
            text.AppendLine("Вердикт о выполнении заказа не вычисляется. Игровые очки, художественное задание и визуальная оценка пока недоступны.");
            text.AppendLine(); text.AppendLine("РАЗБЛОКИРОВАНО В СОХРАНЕНИИ");
            if (progress?.unlocked != null)
                foreach (var section in new[] { new { name = "Выкройки", data = progress.unlocked.patterns }, new { name = "Ткани", data = progress.unlocked.fabrics }, new { name = "Аксессуары", data = progress.unlocked.accessories } })
                    text.AppendLine(section.name + ": " + (section.data?.totalCount?.ToString() ?? "неизвестно") + " | " + Completeness(section.data?.status));
            text.AppendLine("Разблокировка не подтверждает наличие, доступность по цене, совместимость или возможность редактирования.");
            return text.ToString();
        }
    }
}
