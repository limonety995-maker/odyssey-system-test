CE-VEE WeaponPod Compatibility Fix
=================================

Исправляет стартовую ошибку:
Combat Extended :: Failed to find injection point when applying Patch: Harmony_Compat_VanillaEventExpanded

Причина:
Combat Extended 16.7.3 dev ищет старую структуру метода WeaponPod из Vanilla Events Expanded.
В актуальном VEE 1.6 событие было переписано, поэтому старый transpiler CE не находит точку вставки.

Что делает фикс:
1. До инициализации Combat Extended отключает только устаревший WeaponPod-transpiler CE.
2. Сохраняет актуальную логику события VEE 1.6.
3. Добавляет к выпавшему оружию 1-3 магазина совместимых боеприпасов CE.
4. Не отключает событие и не изменяет остальные события VEE.

Порядок загрузки ОБЯЗАТЕЛЕН:
Vanilla Events Expanded
CE-VEE WeaponPod Compatibility Fix
Combat Extended

После установки полностью перезапустите RimWorld.
В логе должна появиться строка:
[CE-VEE WeaponPod Fix] Native CE WeaponPod transpiler disabled; compatible replacement installed.

Фикс собран и проверен для RimWorld 1.6.4871, Vanilla Events Expanded 1.1.0 и Combat Extended 16.7.3 dev.
