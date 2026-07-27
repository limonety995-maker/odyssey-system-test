Alik — Worldgen, Gauss and Shield Fix v2
=======================================

Версия 2 исправляет недостаток первой версии.

Что было не так в v1:
- языковые файлы DefInjected пытались повторно определить уже загруженные списки правил;
- RimWorld не применил эти дубли как замену;
- поэтому ошибки Grammar unresolvable и повреждённое правило торговой гильдии остались.

Что делает v2:
1. После внедрения русского языка напрямую исправляет загруженные RulePackDef.
2. Добавляет r_name->[terrain_word] всем VEE_NamerLandmark_* и VGE_NamerWorldObject_*, у которых есть terrain_word, но нет r_name.
3. Заменяет повреждённую строку торговой гильдии:
   [tradeAdj_fem] [tradeNoun_fem]
   на
   r_name->[tradeAdj_fem] [tradeNoun_fem]
4. Очищает кэши грамматики, чтобы исправленные правила использовались сразу.
5. Сохраняет рабочие XML-исправления Gauss-оружия и riot shield из v1.

Установка:
- удалить старую папку Alik Worldgen Gauss Shield Fix;
- распаковать новую папку с тем же названием в RimWorld\Mods;
- оставить мод последним в порядке загрузки;
- полностью перезапустить RimWorld.

Ожидаемая строка в Player.log:
[Alik Worldgen Fix v2] Runtime grammar fixes applied. Missing r_name roots added: N; malformed traders-guild rules fixed: 1.

При повторном вызове исправление идемпотентно: уже исправленные RulePackDef не изменяются второй раз.
