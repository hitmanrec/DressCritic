# Ревизия репозитория — 2026-11-07

Baseline: коммит `19e6d98`, plugin 0.15.10. Основная очередь теперь в
[EVALUATION-PLAN](EVALUATION-PLAN.md). Проверены текущие исходники, тесты и документы;
нового запуска Unity, аппаратного benchmark или сравнения моделей не было.

Обновление после baseline-аудита: v0.15.11 / `713a107` закрывает fallback guard-кодом
и fault tests. Добавлены offline inventory, production C# harness, repeated runner,
экспериментальный observation protocol и owned Linux CPU runtime probe. Изображений
для качественного сравнения по-прежнему только одно, human labels нет. Реальный
2B CPU probe сохранён в [research JSON](research/2026-11-07-cpu-probe.json);
это не head-to-head benchmark. Текущие инструменты: [OFFLINE-EVALUATION](OFFLINE-EVALUATION.md).
Таблица ниже сохраняет находки исходного baseline; закрытые/частично выполненные
части нужно читать с этим обновлением, не как новую очередь UI-работ.

Текущий приоритет пользователя: завершить извлечение исторического задания и целей
до генератора/обучения. В 0.15.12 добавлены history-bound runtime card и focused goal
fields; причины отказа и цели доступны независимо от нейросети. Ожидается игровой
тест, см. [HISTORY-CONTEXT](HISTORY-CONTEXT.md). Это не подтверждение полного
диалога, вкусов клиентки или вычисления выполнения целей.

Проверка 2026-11-09: три игровых пакета 0.15.12 подтвердили history brief/goals,
включая отсутствие заказа у off-rack. Оба semantic context ответа нарушили контракт.
0.15.13 добавляет server-side JSON schema и нормализует наблюдавшийся color target;
[результаты](HISTORY-CONTEXT-RESULTS.md). Два CPU probe не подтверждают смысловую
надёжность оценок; модель игры не заменена. Pending — новый context-вызов на
игровом Ollama, затем работа над grounded scoring по основному плану.

## Находки и приоритеты

| Приоритет | Находка | Доказательство в коде | Следующее действие |
| --- | --- | --- | --- |
| P0 | Возможен silent single-view fallback при отказе fixed views | Plugin.cs: images заполняются до fixed-view-rendering, Clear вызывается только после успешного Capture. CaptureSession.Stage глотает исключение; EvaluationReadiness требует непустые images и binding, не успех stage/четыре оси | До benchmark сделать отказ/отдельный явный режим; проверить fault injection. Это статически установленный путь, не воспроизведённый игровой инцидент |
| P0 | Качество эстетической шкалы не доказано | ModelRequest.Build просит четыре готовых голоса; JuryContract.ReadJury валидирует форму, Aggregate усредняет | Контрольные примеры, разметка и baseline по основному плану |
| P0 | Нет воспроизводимого сравнения моделей/калибровки | tests покрывают JSON, binding и arithmetic; нет человеческой разметки/holdout/ranking runner | Не подменять тесты протокола тестом эстетического качества |
| P1 | Не разделены visual observations, scoring и wording | ModelRequest/JuryContract: один ответ содержит score и remarks; отдельной calibrated scoring-head нет | Ввести проверяемые контракты после baseline; не просто добавить ещё промптов |
| P1 | Неполная идентификация inference run | jury-request-audit фиксирует prompt/model/limits/timeouts, local-models — список сервера; нет полного reproducibility record | Сохранить hashes weights/projector, runtime/device/preprocessing/decoding и versions перед сравнением |
| P1 | Неавтономная поставка | Plugin config Endpoint/Model, LocalEvaluator HTTP, model/Modelfile, install.sh | После выбора проверяемого кандидата пакетировать runtime и управлять собственным процессом |
| P1 | Нет подтверждённого режима слабого ПК | Нет измерений CPU/малой VRAM с запущенной игрой | Измерить cold/warm/p95 и peak memory; не обещать требования по размеру файла |
| P1 | Отказ отдельного контекста виден в UI | Пользовательские скриншоты показывают Context scoring failed, но исходного нового response/manifest нет | Получить существующий пакет; не объявлять причиной schema/model до проверки |
| P2 | Видимость ремарок ограничена тремя строками | EvaluationPanel: фиксированные cards, ellipsis; исходный текст assessment сохраняется | При новой схеме генерировать краткий evidence-grounded текст, сохранять полный audit |
| P2 | Проблемы tag scroll временно приняты пользователем | Скриншоты/обратная связь после UI 0.15.8–9 | Не расширять косметическую работу без функционального блокера |

P0 здесь означает условие корректного исследования/оценивания, не риск изменения
сохранения. Эти дефекты зафиксированы, но алгоритм и runtime этой ревизией не менялись.

## Что сохраняем и используем

- Exact history display/state binding и before/after checks: HistoryImageBinding,
  GameRuntimeReader, Plugin. Оценка не переносится на другое платье через поздний
  ответ: EvaluationSelection и соответствующие tests.
- Бounded field-only capture, references/depth/collection limits: FieldSnapshot,
  FocusedGarmentFields, DiagnosticJson. Не добавлять произвольные game getters.
- Гармент, материалы, аксессуары: GarmentSnapshot и AssetNames. Это не полная карта
  регионов изображения, ownership или перечень допустимых редактирований.
- Исторический questDefinition вместо текущего заказа: HistoryOrder, Plugin.
  Origin GUID whitelist привязан к 0.7.0; unseen остаётся unknown. Продажа/цена
  не доказываются одним off-rack origin.
- Четыре canonical PNG и renderer metadata: FixedViewCapture. Нужны дальнейшие
  доказательства coverage/света, но новый renderer писать с нуля не требуется.
- CaptureSession и pull-exports.sh дают hashes/stages/raw replies. complete
  означает завершение, не успех оценки. Их достаточно как основы offline набора.
- Изоляция jury/context в ModelRequest полезна и остаётся. Пользовательские
  предпочтения не выдумываются; пять sourced English briefs не равны полным диалогам.
- Имена/ID домов, среднее приложения, отсутствие random bonuses, стиль notebook
  и принятый UI сохраняются. Четыре дома — четыре вкуса, не независимый ensemble.

## Документация и границы проверки

PRODUCT отделён от исторических UI/gate-инструкций; CURRENT ведёт к основному
плану. Старый AUDIT.md остаётся архивом v0.7, не текущим планом. Существующие
schemas описывают нынешний runtime contract; proposed observation/scoring schema
пока не существует и не объявляется реализованной. Model/Modelfile оставлен baseline.

В ревизии запущены существующий core suite и тест importer. Они проверяют
инфраструктуру, а не реалистичность оценок. Proprietary game DLL не доступны;
native build, Unity rendering, скорость новых моделей и offline distribution
остаются непроверенными. Подробности исходных расследований: knowledge/*.
