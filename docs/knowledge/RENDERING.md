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

## Текущая реализация v0.15.4

[FixedViewCapture](../../DressmakerCritic-Diagnostics/FixedViewCapture.cs): временная
additive scene без переключения active scene; собственные MeshFilter/MeshRenderer,
sharedMesh/sharedMaterials read-only, копии property blocks; transform относительно
манекена, проверка углов на неподдерживаемый shear. Skinned meshes отвергаются,
не выдаются за static pose. Камера + четыре cardinal directions. Cleanup отключает
owned root, уничтожает объекты, выгружает сцену, восстанавливает render target и
RenderSettings ambient/sun. Ни одного поворота оригинала.

**Camera.scene/layer/light culling пока не доказывают изоляцию в runtime.**
Нет измеренной цветовой калибровки.

Из IL подтверждено: `AccessoryBatchRenderer` регистрирует MeshRenderer аксессуаров,
отключает их и рисует `Graphics.RenderMeshInstanced` с матрицами
`mannequin.localToWorld * entry.local`. `AccessoryItemPath` в Register сразу
выходит и остаётся на обычных MeshRenderer. v0.12.0 копировал только `enabled`
renderer → батченный декор пропадал. Текущий код копирует disabled MeshRenderer из registration list и AccessoryItems;
неактивные worldPattern и кубические pick proxies исключены из framing.
Свет — `isolated-scene-view-key-v3`: key 0.75 со стороны текущего канонического
вида и fill 0.28 с обратной стороны, temporary flat ambient. Исходные global
RenderSettings сохраняются/восстанавливаются синхронно в finally. Это временное
изменение параметров рендера, не изменение сохранения/гармента.

В 0.15.4 accessory copies получают global/per-material property blocks (раньше
только панели), проверку finite/shear даже без расширения framing. Общий предел
копий — 256; accessory item limit 256, dictionary capacity limit 1024; превышение
даёт явную ошибку, а не оценку неполной копии. Игровые рендереры не изменяются.

accessory-render.json: batcher fields, renderersLive/captureLive, counts
enabled/disabled MeshRenderer, до 8 AccessoryItems. fixed-views.json добавляет
copiedDisabledRenderers/copiedBatchedAccessories/batchGroups/batchEntries.
Пакет 0.15.3 `230842` содержит **0 accessoryStates**, `copiedBatchedAccessories=0`.
Это согласованные счётчики, но они не проверяют перенос декора. Повторная проверка
украшенного платья нужна для изменений property blocks в 0.15.4.

v0.13.0: `approvedForInference=true` после пакетов `220136`/`220146` (ortho≈1.135,
декор на PNG). Оригинальный image.png сохраняется для сравнения, не как silent
fallback. Четыре model image parts, лимит 4 MiB.
[Подробная история с capture IDs](../FIXED-VIEWS.md).
