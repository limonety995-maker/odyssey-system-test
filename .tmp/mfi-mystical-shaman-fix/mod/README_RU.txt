MFI Mystical Shaman Def Fix v3 — RimWorld 1.6

Что исправляет
--------------
Vanilla Events Expanded позволяет отключать события других модов и удаляет
соответствующие IncidentDef из базы. More Faction Interaction жёстко ожидает,
что MFI_MysticalShaman всегда существует, поэтому его удаление вызывает
ошибки MapComponent_GoodWillTrader.

Версия 2 заменяла IncidentDef через XML-патч. Это сохраняло событие, но у
заменённого Def мог отсутствовать modContentPack, из-за чего падал статический
конструктор VEE.Settings.DefsAlterer.

Версия 3:
- не заменяет XML Def;
- сохраняет оригинальный MFI_MysticalShaman и его modContentPack;
- не даёт Vanilla Events Expanded удалить только это событие;
- оставляет остальные настройки событий VEE без изменений.

Установка
---------
1. Полностью удалить старую папку "MFI Mystical Shaman Def Fix v2".
2. Распаковать папку "MFI Mystical Shaman Def Fix" в RimWorld/Mods.
3. Поставить после More Faction Interaction и Vanilla Events Expanded.
4. Полностью перезапустить RimWorld.

Ожидаемая строка в Player.log:
[MFI Mystical Shaman Fix] Prevented Vanilla Events Expanded from removing MFI_MysticalShaman.
