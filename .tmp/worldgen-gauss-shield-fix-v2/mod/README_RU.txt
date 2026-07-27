Alik — Worldgen, Gauss and Shield Fix v2
========================================

Версия 2.0 для RimWorld 1.6.4871.

Почему понадобилась v2
----------------------
В версии 1.0 новые r_name были добавлены через DefInjected. RimWorld использует
DefInjected для перевода уже существующей структуры списков, поэтому новые
структурные элементы списка rulesStrings не были добавлены в итоговые RulePackDef.
Та же причина не позволила надёжно перекрыть повреждённую строку торговой гильдии.

Что делает v2
-------------
1. После полной загрузки русского языка исправляет итоговые RulePackDef в памяти.
2. Добавляет r_name->[terrain_word] всем VEE_NamerLandmark_* и
   VGE_NamerWorldObject_*, у которых есть terrain_word, но отсутствует r_name.
3. Исправляет повреждённую строку торговой гильдии Odyssey:
   [tradeAdj_fem] [tradeNoun_fem]
   -> r_name->[tradeAdj_fem] [tradeNoun_fem]
4. Удаляет старый VEF MultiVerb-компонент у трёх CE-версий Gauss-оружия.
5. Добавляет VAE riot shield renderNodeProperties для RimWorld 1.6.

Установка
---------
Полностью удалить старую папку Alik Worldgen Gauss Shield Fix.
Распаковать новую папку с тем же названием в RimWorld\Mods.
Оставить мод последним в списке и полностью перезапустить RimWorld.

Проверка
--------
В Player.log должна появиться строка:
[Alik Worldgen Fix v2] Runtime Russian grammar repair complete.

Для текущего набора ожидается примерно 47 исправленных генераторов и
1 исправленная строка торговой гильдии. Точное число зависит от набора модов.
