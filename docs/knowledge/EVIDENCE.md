# Реестр доказательств и история расследований

IDs ниже — UTC capture IDs/суффиксы, не индексы UI. У поздних присланных архивов
нет постоянных копий в Git: таблица ссылается на сохранённый анализ, а не
притворяется, что оригинал лежит по временному /tmp. Для повторного побайтового
анализа такого архива нужен оригинал из пользовательского архива/чата.

## Последний пакет и доступность оригиналов

`2d0e51e` удалил старые captures из рабочего дерева, сохранив
[20261104-230842-469-86722d39](../../DressmakerCritic-Diagnostics/exports/captures/20261104-230842-469-86722d39).
Его manifest hashes и размеры проверены. v0.15.3, history record 0, 15 panels,
0 accessories; 4 fixed views приняты. Jury HTTP ответ получен за 10.8 с, но stage
failed: `Invalid remark count`. Все четыре `remarks` — строки. Сами оценки
6.5 / 7 / 5.5 / 7.5, расчётное среднее 6.625. Это **не** успешная отображённая
оценка 0.15.3. v0.15.4 принимает эту форму на границе модели, сохраняя строгий
канонический формат и исходный ответ. Регрессия:
[исходный content](../../tests/fixtures/jury-string-remarks.json).

Старые файлы из таблицы ниже искать в истории Git, например:
`git show d322691:DressmakerCritic-Diagnostics/exports/captures/<id>/<file>`.
13 JSON, необходимых для существующих тестов, сохранены отдельно в gzip без
изменения байтов: [provenance/hashes](../../tests/fixtures/runtime/sources.json).
Тесты больше не требуют старых папок captures. Полные архивы не восстановлены.

## Ранние данные, сохранённые в истории репозитория

| Доказательство | Результат | Где искать |
| --- | --- | --- |
| v0.1 diagnostics/log/catalog | Загрузка, events, каталог; пустой singleton snapshot неоднозначен | DressmakerCritic-Diagnostics/exports; [runtime-findings](../runtime-findings.md) |
| 9224471, пять v0.2 captures | Unity serializer потерял nested fields, file existence недостаточно | runtime-findings |
| 0b9ead5, три v0.2.1 captures | Собственный JSON writer подтверждён | runtime-findings |
| 9bfe595, три v0.2.2 captures | CuttingRoom/Mannequin в общей PatternCutting; empty editor state | runtime-findings |
| 04a94ed, девять стадий | Гармент/фабрики/флаги/аксессуары в workflow | [nine-stage-findings](../nine-stage-findings.md), JSON summaries рядом |
| 5d53199, 124653/124817 | До/после принятия заказа; безопасный прогресс | [order-pair-findings](../order-pair-findings.md) |
| 125434-693-79d9b746 | Полный unlocked membership и resolved tag requirements | order-pair-findings |
| 9c43ffa, image141507 + brief141325/141420 | Чистый single PNG; speaker-only не brief | [first-image-findings](../first-image-findings.md) |

Последний raw export искать в [exports](../../DressmakerCritic-Diagnostics/exports),
регрессионные данные — в [tests/fixtures](../../tests/fixtures). Не все тестовые
fixtures — полные исходные пакеты; часть специально сокращена. Объём fixtures
не является мерой полноты исследованных game данных.

## Поздние загрузки из чата (оригиналы не добавлены в Git)

| Capture / archive | Проверенный результат | Сохранённый анализ |
| --- | --- | --- |
| capture-700, 182433-869-c1721003 | Shared RT, URP, hardware, no configured model | [CAPTURE-700](../CAPTURE-700.md); reduced fixture capture-700 |
| 194051-125-28428ddd /194102-761-4a069d5d | Display state reference→history7; main screen refusal | [CAPTURE-800](../CAPTURE-800.md) |
| 194716-102-b6fddc29 | Exact full display chain; timeout90 с | [CAPTURE-801](../CAPTURE-801.md) |
| 195439-869-4b2977d5 | length/empty final,2048 tokens | [CAPTURE-802](../CAPTURE-802.md) |
| 200023-297-2e2492e8 | none option не устранил Thinking truncation | [CAPTURE-803](../CAPTURE-803.md) |
| 200828-774-d455f3c3 | Instruct copied template | [CAPTURE-804](../CAPTURE-804.md); exact final-content fixture capture-804 |
| 201159-031-73a8b9c2 | Первый genuine jury:6.25/17.3 с | [CAPTURE-805](../CAPTURE-805.md); final-content fixture capture-805 |
| 202234-438-085a7c52 /202434-931-ec79c904 | UI, два платья6.25/6.625 | [RESULT-UI](../RESULT-UI.md) |
| 205543-739-ca5c6923 /205645-784-b610e7da | URP renders, bad world framing | [FIXED-VIEWS](../FIXED-VIEWS.md) |
| 210119-876-6a56b304 | Framing исправлен;15 empty meshes | FIXED-VIEWS |
| 210534-044-69572427 /210541-604-4ddb08c3 /210557-306-229632f0 | Stable geometry, varying illumination;20 accessoryStates | FIXED-VIEWS |
| Скриншоты после v0.12.0 | Back-biased light, missing accessory meshes | FIXED-VIEWS; без нового полного пакета источник света/батчинга неизвестен |

Все перечисленные поздние пакеты при разборе прошли проверку manifest hashes/sizes.
Это не означает успешный inference или качество изображений. Для последнего ряда
есть только визуальный feedback, а не проверка исходного manifest.

## Не повторять ошибочные подходы

1. Не выводить отсутствие платья/заказа из null singleton/proposedQuest.
2. Не считать events последним авторитетным state; не суммировать preview duplicates.
3. Не использовать timestamps, имена и siblingIndex как image/history binding.
4. Не считать список каталога unlocked/owned/editable.
5. Не подменять brief именем speaker, tag scores — визуальным качеством.
6. Не извлекать оценки из reasoning и не принимать template echoes за оценку.
7. Не лечить Thinking loop только timeout/token budget; вариант модели имеет значение.
8. Не объявлять fixed camera достаточной: geometry framing, coverage и light проверяются отдельно.
9. Не считать isolated scene автоматической гарантией light isolation в URP.
10. Не считать syntax/unit tests заменой native compile/runtime.

## Документы-архивы и схемы

[AUDIT](../AUDIT.md) — исторический аудит v0.7; ранние implementation docs
runtime-findings, garment-snapshot, progress-context, phase-three-inputs,
garment-card и export-schema содержат планы/команды своих версий. Текущая очередь
находится в CURRENT, а не в их разделах Next test.

Схемы в docs остаются на местах: evaluation-input, jury-response,
context-response, assessment; garment-snapshot, progress-snapshot/progress-context,
brief-reference/brief-evidence; image-evidence/capture-image; runtime-export.
Они относятся к разным поколениям экспортов. Версия plugin не равна версии schema.
Некоторые JSON schemas допускают только часть семантических правил: runtime
дополнительно проверяет IDs, decimal precision, evidence availability и mean.
