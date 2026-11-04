# Изображения: что действительно доказано

## Идентичность

OldDressView.renderImage.m_Texture даёт чистую картинку, но это общий RenderTexture
DressPreview, а сохранённые thumbnailTex — другие Texture2D. Время, имя, сходство
и viewer.siblingIndex не доказывают связь с записью истории.

Подтверждённая цепочка: уникальная exact state reference в completedQuests →
Sketchbook._displayDress → _assignedMannequin == previewMannequin →
dressPreviewCamera.targetTexture == текстура единственного видимого viewer.
Проверка до/после capture, сравнение нормализованного state. Source
[HistoryImageBinding](../../DressmakerCritic-Diagnostics/HistoryImageBinding.cs),
[GameRuntimeReader](../../DressmakerCritic-Diagnostics/GameRuntimeReader.cs),
[Plugin](../../DressmakerCritic-Diagnostics/Plugin.cs), [capture-800](../CAPTURE-800.md).
Dirty thumbnail, неоднозначность, смена state — невалидная привязка.
На главном экране нет изображения/гармента; отказ ожидаем.

## Последовательность рендер-экспериментов

| Подход | Наблюдение | Вывод |
| --- | --- | --- |
| Single viewer PNG 2048² | Изображение работает, зависит от поворота | Удобно для первого inference, непригодно для калибровки ракурс-независимой оценки |
| Temporary built-in camera | В реальной игре URP | Built-in fallback не доказывает поддержку URP |
| StandardRequest, 4 вида 768² | Восемь PNG в первой паре; платье крошечное | URP API работает; world renderer bounds неверны для framing |
| Mesh bounds → mannequin local | Пропущено 15 пустых AnimationMesh; orthographic size 1.13 вместо12.3 | Перед/спина/бока целиком видны; причина плохого масштаба подтверждена |
| Три поворота украшенного платья | local extents совпадают ~1e-7, свет резко меняется | Геометрическая нормализация работает; освещение ещё зависит от игровой сцены |
| Отдельная scene + static mesh copies, v0.12.0 | Скриншоты: перед тёмный, зад светлый, часть декора отсутствует | Метод ещё не принят; свойства шейдеров/источники света/батчинг требуют расследования |

Постоянный режим перспективы отсутствует: ортографическая камера уменьшает
перспективные искажения. Охват задаёт orthographicSize, не только расстояние.
+Z визуально подтверждён как перед, -Z как спина; +X/-X обозначаем side A/B,
не объявляя анатомическую сторону. Bound includes mannequin/pole, не только ткань.

## Текущая реализация v0.12.1

[FixedViewCapture](../../DressmakerCritic-Diagnostics/FixedViewCapture.cs): временная
additive scene без переключения active scene; собственные MeshFilter/MeshRenderer,
sharedMesh/sharedMaterials read-only, копии property blocks; transform относительно
манекена, проверка углов на неподдерживаемый shear. Skinned meshes отвергаются,
не выдаются за static pose. Камера + четыре cardinal directions, fixed light rig,
probes/shadows copies off. Cleanup отключает owned root, уничтожает объекты,
выгружает сцену и восстанавливает render target. Ни одного поворота оригинала.

**Camera.scene/layer/light culling пока не доказывают изоляцию в runtime.**
Не считать собственную сцену автоматически свободной от глобального света,
RenderSettings, custom shader effects. Нет измеренной цветовой калибровки.

AccessoryBatchRenderer реально существует и referenced из Dress. Предположение,
что пропущенные детали рисуются batched/instanced вне MeshRenderer, пока hypothesis.
Никакие его недокументированные методы/поля для восстановления не придуманы.
Наличие 20 accessoryStates не доказывает 20 корректно отрисованных объектов.

Новый accessory-render.json снимает реальные поля batcher + первые4 аксессуара;
fixed-views.json — lights/scenes/masks/направления/интенсивности, global sun/ambient,
copied meshes/material shaders. Нужен полный новый пакет, не только screenshot.

Пока approvedForInference=false и readiness gate остаются. Оригинальный image.png
сохраняется для сравнения, но не служит тихим fallback. Подготовлены четыре
model image parts с подписями и общим лимитом4 MiB; неподтверждённые виды не отправляются.
[Подробная история с capture IDs](../FIXED-VIEWS.md).
