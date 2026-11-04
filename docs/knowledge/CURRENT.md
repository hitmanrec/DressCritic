# Текущее состояние и очередь

Версия кода: **0.15.3** (установлена в BepInEx/plugins/DressmakerCritic).

## Как должно работать (целевой UX)

Только **просмотр платья из истории** (`oldDressViewHolder`):

1. Блок задания (title/text) — компактнее, выше, меньший body-шрифт.
2. Стат-полоски — сжаты и подняты.
3. Photo Mode — меньше и сдвинута.
4. В освободившемся месте **левой** страницы — упрощённый Jury **без** своего фона
   (фон = страница блокнота):
   - `Rate dress` + переключатель `Houses` / `Order`
   - после оценки: среднее, вердикт, **гербы домов** и **ремарки**
   - без закладки Jury, без оверлея, без времени снапшота, без limitations-текста
5. WIP / текущее платье — Jury **не** показывается.
6. Смена платья в истории сбрасывает результат.
7. UI-строки по умолчанию English; RU — опциональный
   `BepInEx/config/DressmakerCritic/locale.json` (образец `locale.example.ru.json`).

Пайплайн оценки (fixed views → local VLM jury) при этом **работает**: оценки
считаются; проблема сейчас в **встраивании UI**, не в inference.

## Что сделано в коде (0.13 → 0.15)

| Версия | Суть |
|--------|------|
| 0.13.x | Fixed views + view-key lighting; снят floating `GUI.Button` |
| 0.14.x | Unity UI + TMP/sprites как у ModLoader; English + locale hook; гербы |
| 0.15.0 | Убраны закладка и окно; history-only inline; без preview/limitations |
| 0.15.1–2 | Попытки якорей на `leftPage`, сжатие brief/score |
| 0.15.3 | Jury на **левой половине** `oldDressViewHolder` + VerticalLayoutGroup; brief не scale'ит весь page |

Также: `UiStrings.cs`, `HouseEmblems.cs`, TMP reference в csproj, docs/README.

## Что не работает сейчас (подтверждено скринами)

Макет **всё ещё сломан** в истории:

- Элементы Jury (`Rate dress` / `Houses` / `Order`, иногда mean) собираются у
  **корешка** разворота, а не в нижней половине левой страницы.
- **Гербы и ремарки домов не видны** (есть место под лилией пустое) — контейнер
  Jury не получает нормальную высоту/ширину на левой странице, scroll с домами
  фактически не читаем.
- Сжатие brief/stats **недостаточное** или бьёт не в те RectTransform.

Гипотеза: `oldDressViewHolder` / система якорей Sketchbook не совпадает с
визуальной «левой страницей»; fractional anchors и scale существующих UI
элементов нестабильны. Нужна привязка к конкретному child (карточка статов /
parchment brief) через local offsets относительно **известного** RectTransform,
плюс dump иерархии в runtime для калибровки.

## Очередь

1. Runtime dump иерархии left page (имена, anchors, sizes) при открытии истории.
2. Пересадить Jury sibling’ом под score card с явной высотой в px/local space.
3. Дожать brief+score так, чтобы нижние ~40% левой страницы были свободны.
4. Проверить: Rate → видны 4 дома с гербами и remarks; WIP без Jury.

## Проверка после фикса UI

1. `bash install.sh`, перезапуск игры.
2. История: Jury только слева внизу; гербы+ремарки после Rate.
3. WIP: Jury отсутствует; выход из истории восстанавливает brief/score/Photo Mode.
