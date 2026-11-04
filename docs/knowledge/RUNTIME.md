# Runtime: подтверждённые маршруты и смысл данных

## Окружение

Наблюдались Dressmaker 0.7.0, Unity 6000.3.4f1, BepInEx 5.4.23.5,
Yuuma ModLoader 0.7.8, URP, Linux/Proton. В пользовательском пакете: RTX 5070,
12227 MB VRAM, i7-14700KF, 31902 MB RAM. Это конкретная машина, не минимум.
Источники: [ранний runtime](../runtime-findings.md), [capture-700](../CAPTURE-700.md).

## Маршруты

| Данные | Поле/маршрут | Подтверждение / предел |
| --- | --- | --- |
| Подэкран | GameManager.<CurrentScene>k__BackingField | Unity scene PatternCutting общая; FrontDesk, Sketchbook, CuttingRoom, Store, Mannequin, Photo различаются backing enum |
| Активный гармент | GameManager.activeDress.state | Заполнен с раскройки в девятистадийном тесте; null на главном экране/в раннем дизайне не означает отсутствие всех объектов |
| Сохранённые | PlayerProgress.completedQuests[i].dress | Индекс локален текущему списку; не равен siblingIndex UI |
| Отображаемое платье | Sketchbook._displayDress.state | Exact reference в тестах совпала с history record 7, хотя viewer.siblingIndex был 5 |
| Прогресс | PlayerProgress._current | Статическое поле читается только при отсутствии type initializer; маршрут подтверждён в игре, guard не снимать |
| Заказы | activeCommissions, activeCommissionIndex, QuestState.questDefinition | До принятия 0/-1, после 1/0; proposedQuest был null в обоих случаях |
| Открытые ресурсы | unlockedPatterns/unlockedFabrics/unlockedAccessories | В одном save полные компактные списки 72/212/182; это не универсальные количества |
| Компоненты | bodice/sleeve/collar/skirt Definition и варианты | GUID — asset identity; prettyName может быть пустым/отличаться от asset name |
| Панели | panelStates, fabric, hasBeenCut, placedOnMannequin, hasBeenStitched, grainQuality | Назначенная ткань панели отличается от выбора ткани до раскройки |
| Аксессуары | accessoryStates, definition, position/rotation/normal | Нормализованные записи и координаты доступны; полнота визуальных meshes — отдельный текущий блокер |

Доступ: [GameRuntimeReader](../../DressmakerCritic-Diagnostics/GameRuntimeReader.cs),
[ProgressAccess](../../DressmakerCritic-Diagnostics/ProgressAccess.cs),
[GarmentSnapshot](../../DressmakerCritic-Diagnostics/GarmentSnapshot.cs),
[ProgressSnapshot](../../DressmakerCritic-Diagnostics/ProgressSnapshot.cs).

## Девять стадий

[Полная таблица](../nine-stage-findings.md): до раскройки state отсутствует;
после 14/14 вырезанных панелей они ещё не placed/stiched; частичное шитьё 7/14;
на манекене sewingComplete=true при finished=false; на фото finished=true.
Фото содержит дубли loaded Dress/PatternPanel: нельзя суммировать все загруженные
объекты как один дизайн. 22 join-state записи не означают 22 готовых шва.
Поздние сохранённые платья не всегда имеют hasBeenStitched=true у всех панелей;
нельзя объявлять завершённость по «100% stitched». GrainQuality наблюдалось
71.72221–99.98385 после раскройки; эстетический смысл/пороги не установлены.

## Заказы и инвентарь

Undercover Servant GUID f205a54432cf7a4bda9bf1ab59049a6d: budget 250,
Quality AtLeast75, Elegant AtLeast50, Daywear AtLeast50, Simple AtLeast25,
gated=false. Это игровые ограничения, не художественная оценка.
[Доказательства и tag GUID](../order-pair-findings.md).

Статический каталог: 178 компонентов и метаданные совместимости/вариантов/тегов.
Наличие в каталоге ≠ unlocked; unlocked ≠ owned stock ≠ affordable ≠ compatible
≠ доступная операция редактирования. Полный runtime GUID ↔ catalog mapping,
детерминированный score calculation и реальные возможности editor не доказаны.

## Безопасность извлечения

Read-only instance fields, свойства игры — только metadata, не arbitrary getters.
BCL List читается по проверенным _size/_items, не через custom enumerators.
Повторные ссылки — graph IDs; value types требуют focused paths без reference ID.
Обычный граф 512 nodes, focused garment 4096, новый accessory probe 2048;
depth4, fields64, items32, strings512, hierarchy8. Compact inventory/history
имеет отдельные лимиты до512. Лимиты вывода не ограничивают внутренние массивы,
которые Unity/reflection выделяет до нашей обработки. Idle UI не сканирует игру.

Ранний Unity JSON serializer терял nested DTO fields; собственный bounded DTO
writer исправил это в реальных v0.2.1 captures. Missing, null, empty и truncated
не взаимозаменяемы. Не вызывать методы требований/прогресса для «проверки».
