# Локальная модель, оценка и интерфейс

Целевая архитектура обновлена: [основной план](../EVALUATION-PLAN.md) и
[ревизия](../REPOSITORY-AUDIT.md). Описание одного запроса с четырьмя готовыми
оценками ниже относится к текущей baseline. Калибровка/разделение наблюдений,
scoring и wording ещё не реализованы; замена модели не объявлена проверенной.

## Продуктовый контракт

[PRODUCT](../PRODUCT.md) и [JuryContract](../../DressmakerCritic-Diagnostics/JuryContract.cs).
Один локальный VLM, четыре роли в одном запросе, не четыре независимых эксперта.

| ID | Имя в UI | Приоритет | Девиз |
| --- | --- | --- | --- |
| harmony | Maison Orvelle | Композиция/цвет/согласованность | Каждая деталь на своём месте |
| character | Bellombre | Выразительный образ | Наряд должен оставлять впечатление |
| avant_garde | Virelai | Необычные сочетания/эксперимент | Привычное — лишь отправная точка |
| atelier | Maison Aveline | Видимые пропорции/элегантность | Убедительность в каждой линии |

Все судят платье в целом, без случайных бонусов и принудительных разногласий.
0–10, максимум один десятичный знак, 1–2 коротких замечания. Если нет данных,
unknown/null, не ноль. Все четыре known → арифметическое среднее в приложении.
Разброс≥3 отмечается divided. Verdict bands: [0,3) weak, [3,5) uneven,
[5,7) competent, [7,9) strong, [9,10] exceptional. Это правила представления,
не научно подтверждённые пороги. Есть unknown → нет среднего по неполному жюри.

В jury request нет заказа, brief, клиента, quest name/history index. Отдельный
stateless context request получает только проверенный brief/customer evidence,
без голосов жюри. Compliance/customer score не входят в среднее. Нет контекста —
нет лишнего запроса. Детерминированные игровые требования остаются not-evaluated.
Советы/список действий не входят в формат: пока не известны editor capabilities,
они могут оказаться неисполнимыми. Encoder-only и сравнение платьев не в MVP.

Rubric1.1.0 запрещает выдавать ракурс, качество рендера, удобство, вес, функциональность,
сшиваемость или намерение автора за наблюдаемый дизайн. Реальные ответы всё ещё
нарушали это. Инструкция ≠ гарантия; синтаксическая валидация не оценивает истинность.

## Модель и последовательность ошибок

Текущий [Modelfile](../../model/Modelfile): qwen3-vl:4b-instruct-q4_K_M,
num_ctx16384, temperature0.2; локальный alias dresscritic-vision.
Ollama≥0.12.7, loopback /v1/chat/completions. Первый выбор qwen3-vl:4b
указывал на Thinking weights по официальному registry. reasoning_effort=none
сам по себе не устранил обрезанный пустой final content в пользовательском runtime.
Instruct дал полный ответ. Не предлагать повторную установку старого default tag.

| Этап | Наблюдалось |
| --- | --- |
| v0.8.1 | Подтверждённое платье; отмена через90 с |
| v0.8.2/3 | 82.6/73.3 с; finish_reason=length; final content пустой, 2048 completion tokens |
| v0.8.4 Instruct | 10.9 с; полный JSON, дословная копия шаблона unknown |
| v0.8.5 исправленный prompt | 17.3 с; реальные голоса6.5/7/6/5.5, среднее6.25 |
| v0.9.0 другие захваты | 18.6 с /6.25 и12.0 с /6.625; не performance benchmark |

Убраны copyable answer examples, явно описан assessed status. Заглушки отвергаются.
reasoning не восстанавливает оценки, не показывается как ремарка. Truncated/empty
ответ отклоняется до парсинга; JSON reader получает MoveToContent для Mono.

Сервер пользователя показывал5.8 GB, context16384, 8%/92% или10%/90% CPU/GPU:
это размещение, не загрузка/доля времени и не доказательство причины задержки.
Cold loading/VRAM pressure/CPU fallback как причины первого timeout не установлены.

## Брифы и заказчицы

В DLL пять оригинальных английских summaries: Undercover Servant, Librarian
Workwear, Flamboyant Funeral, Fierce Funeral, Pigeon Adoption. Источники —
цитируемые wiki pages, coverage excerpt-only. Это не полные диалоги и не все квесты.
GUID Undercover Servant проверен; остальные key mappings не объявлять GUID-verified
лишь по совпадению заголовка. Lookup version-scoped, GUID→exact key; title-only
candidate не допускается как проверенный brief. Conflict/version mismatch → unknown.

Speaker-only Rose из FrontDesk не содержит brief. Нет доказанной истории диалогов.
Нет проверенных полных профилей заказчиц: нельзя выдумывать вкус по имени/заказу.
[Источники и правила расширения](../english-brief-reference.md).

## Транспорт и артефакты

[ModelRequest](../../DressmakerCritic-Diagnostics/ModelRequest.cs),
[LocalEvaluator](../../DressmakerCritic-Diagnostics/LocalEvaluator.cs),
[CaptureSession](../../DressmakerCritic-Diagnostics/CaptureSession.cs).
Loopback HTTP(S), без redirect/proxy/credentials/query. Image hashes/PNG dimensions
проверяются перед base64; максимум4 изображения, общий4 MiB. HTTP reply≤256 KiB,
assessment≤64 KiB; строгие keys/house IDs/ranges, никаких советов/готового mean.
2048 output tokens, deadline default300 с, clamp30–600, shutdown cancellation.
Metadata GET≤128 KiB,3 с; наличие model ID само по себе не доказывает vision.

Capture manifest содержит независимые stages, hashes/sizes. complete означает
конец коллекции, не успех всех stages. Частичная ошибка сохраняет остальные
данные. Importer не забирает/удаляет незавершённый пакет; --all сохраняет источник
и позволяет обновить частичную копию после завершения. Raw model reply сохраняется
даже при отклонении. Полные raw graph/private data не отправляются модели.

## UI / поставка

Целевой UX (0.15+): history-only inline Jury на левой странице блокнота без
закладки/оверлея; Rate + Houses/Order; гербы и ремарки после оценки; English UI
+ `locale.json`; WIP без Jury. Подробности и **текущий broken layout** —
[CURRENT.md](CURRENT.md).

Код: Unity UI + TMP/sprites игры, `UiStrings`, `HouseEmblems`. В 0.15.3
подтверждён сломанный макет. Последний пакет также выявил ошибку контракта:
модель вернула remarks строками; `manifest.complete=true` не означает успех.
0.15.4 оборачивает **только remarks-строку** в массив на границе HTTP, затем
применяет прежнюю строгую проверку (1–2 элемента, ≤240 символов, четыре дома,
score/status/keys). Не обрезает, не делит предложения, не исправляет оценки и
не делает повторный вызов модели. Raw HTTP остаётся неизменным; assessment.json
канонический. limitations и остальные поля не получают такой совместимости.

Результат принадлежит непрерывному просмотру одного history state: смена
страницы, выход из истории, замена state при том же индексе инвалидируют UI ticket.
Ответ сохраняется в своём пакете, но не попадает на другое платье. В 0.15.4 это
покрыто core-тестами; интеграция требует проверки в игре. Discovery Sketchbook
ограничен двумя вызовами в секунду, между ними читаются только известные поля.
Убрана безусловная пересборка layout каждый кадр. Scroll получил прозрачную
raycast-поверхность; высота ремарок вычисляется TMP по доступной ширине.
Локализация generated remarks не сделана; английский текст модели сохраняется.

BepInEx plugin остаётся net472. Никакие game DLL не распространяются. Ollama
пока запускается отдельно вручную.
