using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Alik.QuartermasterRussianCE
{
    internal static class QuartermasterCECompatibility
    {
        private static Type armorEvaluatorType;
        private static Type recommendationType;
        private static Type ceVerbPropertiesType;
        private static Type ceProjectilePropertiesType;
        private static Type ceAmmoPropsType;
        private static Type ceStatDefOfType;

        private static MethodInfo applyScoreBandsMethod;
        private static readonly Dictionary<string, FieldInfo> RecFields =
            new Dictionary<string, FieldInfo>(StringComparer.Ordinal);

        private static bool installed;
        private static bool errorLogged;

        public static bool Install(Harmony harmony)
        {
            ceVerbPropertiesType = AccessTools.TypeByName("CombatExtended.VerbPropertiesCE");
            ceProjectilePropertiesType = AccessTools.TypeByName("CombatExtended.ProjectilePropertiesCE");
            ceAmmoPropsType = AccessTools.TypeByName("CombatExtended.CompProperties_AmmoUser");
            ceStatDefOfType = AccessTools.TypeByName("CombatExtended.CE_StatDefOf");

            if (ceVerbPropertiesType == null || ceProjectilePropertiesType == null)
            {
                return false;
            }

            armorEvaluatorType = AccessTools.TypeByName("Quartermaster.ArmorEvaluator");
            recommendationType = AccessTools.TypeByName("Quartermaster.WeaponRecommendation");
            if (armorEvaluatorType == null || recommendationType == null)
            {
                Log.Warning("[Quartermaster RU/CE] Quartermaster weapon evaluator types were not found.");
                return false;
            }

            applyScoreBandsMethod = AccessTools.Method(
                armorEvaluatorType,
                "ApplyScoreBands",
                new[] { recommendationType });

            CacheRecommendationFields();

            MethodInfo rawDps = AccessTools.Method(
                armorEvaluatorType,
                "ComputeRangedRawDps",
                new[] { typeof(ThingDef), typeof(ThingDef) });
            MethodInfo dpsa = AccessTools.Method(
                armorEvaluatorType,
                "ComputeRangedDPSA",
                new[] { typeof(ThingDef), typeof(ThingDef) });
            MethodInfo fill = AccessTools.Method(
                armorEvaluatorType,
                "FillWeaponStats",
                new[] { recommendationType, typeof(ThingDef), typeof(ThingDef) });
            MethodInfo existing = AccessTools.Method(
                armorEvaluatorType,
                "BuildWeaponThingRec",
                new[] { typeof(Thing), typeof(bool) });

            Type tabType = AccessTools.TypeByName("Quartermaster.MainTabWindow_BestArmor");
            MethodInfo tooltip = tabType == null
                ? null
                : AccessTools.Method(tabType, "BuildWeaponTableTooltip", new[] { recommendationType });

            if (rawDps == null || dpsa == null || fill == null || existing == null || applyScoreBandsMethod == null)
            {
                Log.Warning("[Quartermaster RU/CE] Current Quartermaster methods do not match the supported 1.6 build.");
                return false;
            }

            harmony.Patch(
                rawDps,
                prefix: new HarmonyMethod(typeof(QuartermasterCECompatibility), nameof(RawDpsPrefix)));
            harmony.Patch(
                dpsa,
                prefix: new HarmonyMethod(typeof(QuartermasterCECompatibility), nameof(DpsaPrefix)));
            harmony.Patch(
                fill,
                postfix: new HarmonyMethod(typeof(QuartermasterCECompatibility), nameof(FillStatsPostfix)));
            harmony.Patch(
                existing,
                postfix: new HarmonyMethod(typeof(QuartermasterCECompatibility), nameof(ExistingWeaponPostfix)));

            if (tooltip != null)
            {
                harmony.Patch(
                    tooltip,
                    postfix: new HarmonyMethod(typeof(QuartermasterCECompatibility), nameof(TooltipPostfix)));
            }

            installed = true;
            return true;
        }

        public static bool RawDpsPrefix(ThingDef __0, ThingDef __1, ref float __result)
        {
            if (!TryCompute(__0, __1, null, out CEWeaponStats stats))
            {
                return true;
            }

            __result = stats.SustainedDps;
            return false;
        }

        public static bool DpsaPrefix(ThingDef __0, ThingDef __1, ref float __result)
        {
            if (!TryCompute(__0, __1, null, out CEWeaponStats stats))
            {
                return true;
            }

            __result = stats.SustainedDps;
            return false;
        }

        public static void FillStatsPostfix(object __0, ThingDef __1, ThingDef __2)
        {
            if (__0 == null || !TryCompute(__1, __2, null, out CEWeaponStats stats))
            {
                return;
            }

            ApplyToRecommendation(__0, stats);
        }

        public static void ExistingWeaponPostfix(Thing __0, object __result)
        {
            if (__0 == null || __result == null ||
                !TryCompute(__0.def, __0.Stuff, __0, out CEWeaponStats stats))
            {
                return;
            }

            ApplyToRecommendation(__result, stats);
        }

        public static void TooltipPostfix(object __0, ref string __result)
        {
            if (!installed || __0 == null)
            {
                return;
            }

            ThingDef weaponDef = GetRec<ThingDef>(__0, "WeaponDef");
            ThingDef stuffDef = GetRec<ThingDef>(__0, "StuffDef");
            Thing existing = GetRec<Thing>(__0, "ExistingThing");

            if (!TryCompute(weaponDef, stuffDef, existing, out CEWeaponStats stats))
            {
                return;
            }

            string ammo = stats.AmmoLabel.NullOrEmpty() ? "без боеприпаса" : stats.AmmoLabel;
            __result +=
                "\n\nCombat Extended:" +
                "\nРасчёт выполнен по стандартному боеприпасу: " + ammo + "." +
                "\nУВС учитывает очередь и усреднённую перезарядку магазина." +
                "\nТочность стрелка и условия боя не моделируются; недоступные дистанции получают оценку 0." +
                "\nПробитие: " + stats.SharpPen.ToString("0.##") + " мм RHA / " +
                stats.BluntPen.ToString("0.##") + " МПа.";
        }

        private static void CacheRecommendationFields()
        {
            foreach (FieldInfo field in recommendationType.GetFields(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                RecFields[field.Name] = field;
            }
        }

        private static void ApplyToRecommendation(object rec, CEWeaponStats stats)
        {
            SetRec(rec, "BaseDps", stats.SustainedDps);
            SetRec(rec, "Damage", stats.DamagePerShot);
            SetRec(rec, "ArmorPenetration", stats.EffectivePen);
            SetRec(rec, "Warmup", stats.Warmup);
            SetRec(rec, "Cooldown", stats.CooldownWithReload);
            SetRec(rec, "BurstCount", stats.BurstCount);
            SetRec(rec, "SecondaryValue", stats.Range);
            SetRec(rec, "MinRange", stats.MinRange);

            SetRec(rec, "AccTouch", stats.Reachable(3f) ? 1f : 0f);
            SetRec(rec, "AccShort", stats.Reachable(12f) ? 1f : 0f);
            SetRec(rec, "AccMedium", stats.Reachable(25f) ? 1f : 0f);
            SetRec(rec, "AccLong", stats.Reachable(40f) ? 1f : 0f);

            try
            {
                applyScoreBandsMethod.Invoke(null, new[] { rec });
            }
            catch (Exception exception)
            {
                LogOnce("Could not recalculate Quartermaster's CE weapon score", exception);
            }
        }

        private static bool TryCompute(
            ThingDef weaponDef,
            ThingDef stuffDef,
            Thing existingThing,
            out CEWeaponStats stats)
        {
            stats = default;
            if (weaponDef == null || !weaponDef.IsRangedWeapon)
            {
                return false;
            }

            VerbProperties verb = weaponDef.Verbs?.FirstOrDefault(v => v.isPrimary)
                ?? weaponDef.Verbs?.FirstOrDefault();
            object ammoProps = FindCEAmmoProps(weaponDef);
            ThingDef projectileDef = GetStandardProjectile(verb, ammoProps, out string ammoLabel);

            if (verb == null || projectileDef?.projectile == null)
            {
                return false;
            }

            object projectileProps = projectileDef.projectile;
            bool isCE = ceVerbPropertiesType.IsInstanceOfType(verb)
                || ceProjectilePropertiesType.IsInstanceOfType(projectileProps)
                || ammoProps != null;
            if (!isCE)
            {
                return false;
            }

            try
            {
                float damageMultiplier = StatValue(
                    weaponDef,
                    stuffDef,
                    existingThing,
                    StatDefOf.RangedWeapon_DamageMultiplier,
                    1f);
                float apMultiplier = StatValue(
                    weaponDef,
                    stuffDef,
                    existingThing,
                    StatDefOf.RangedWeapon_ArmorPenetrationMultiplier,
                    1f);

                int pelletCount = Math.Max(1, GetField(projectileProps, "pelletCount", 1));
                float damage = projectileDef.projectile.GetDamageAmount(damageMultiplier, null) * pelletCount;

                float sharpPen = Math.Max(0f, GetField(projectileProps, "armorPenetrationSharp", 0f) * apMultiplier);
                float bluntPen = Math.Max(0f, GetField(projectileProps, "armorPenetrationBlunt", 0f) * apMultiplier);
                bool sharpDamage = projectileDef.projectile.damageDef?.armorCategory == DamageArmorCategoryDefOf.Sharp;
                float effectivePen = sharpDamage ? sharpPen : bluntPen;
                if (effectivePen <= 0f)
                {
                    effectivePen = Math.Max(sharpPen, bluntPen);
                }

                float warmupMultiplier = GetField(projectileProps, "warmupMultiplier", 1f);
                float warmupOffset = GetField(projectileProps, "warmupOffset", 0f);
                float reloadMultiplier = GetField(projectileProps, "reloadTimeMultiplier", 1f);
                float rangeMultiplier = GetField(projectileProps, "effectiveRangeMultiplier", 1f);
                float rangeOffset = GetField(projectileProps, "effectiveRangeOffset", 0f);

                float warmup = Math.Max(0f, verb.warmupTime * warmupMultiplier + warmupOffset);
                float range = Math.Max(0f, verb.range * rangeMultiplier + rangeOffset);
                float minRange = Math.Max(0f, verb.minRange);

                int burstCount = Math.Max(
                    1,
                    Mathf.RoundToInt(StatValue(
                        weaponDef,
                        stuffDef,
                        existingThing,
                        CEStat("BurstShotCount"),
                        Math.Max(1, verb.burstShotCount))));

                float ticksBetween = Math.Max(
                    0f,
                    StatValue(
                        weaponDef,
                        stuffDef,
                        existingThing,
                        CEStat("TicksBetweenBurstShots"),
                        Math.Max(0, verb.ticksBetweenBurstShots)));

                float cooldown = Math.Max(
                    0.01f,
                    StatValue(
                        weaponDef,
                        stuffDef,
                        existingThing,
                        StatDefOf.RangedWeapon_Cooldown,
                        0.01f));

                int magazineSize = Math.Max(
                    0,
                    Mathf.RoundToInt(StatValue(
                        weaponDef,
                        stuffDef,
                        existingThing,
                        CEStat("MagazineCapacity"),
                        GetField(ammoProps, "magazineSize", 0))));

                float reloadTime = Math.Max(
                    0f,
                    StatValue(
                        weaponDef,
                        stuffDef,
                        existingThing,
                        CEStat("ReloadTime"),
                        GetField(ammoProps, "reloadTime", 0f)) * reloadMultiplier);

                int ammoPerShot = Math.Max(
                    1,
                    Math.Max(
                        GetField(verb, "ammoConsumedPerShotCount", 1),
                        GetAmmoSetConsumedPerShot(ammoProps)));

                float burstSpacing = Math.Max(0, burstCount - 1) * ticksBetween / 60f;
                float baseCycle = Math.Max(0.01f, warmup + cooldown + burstSpacing);

                float reloadPerBurst = 0f;
                if (magazineSize > 0 && reloadTime > 0f)
                {
                    float shotsPerMagazine = Math.Max(1f, magazineSize / (float)ammoPerShot);
                    float burstsPerMagazine = Math.Max(1f, shotsPerMagazine / burstCount);
                    reloadPerBurst = reloadTime / burstsPerMagazine;
                }

                float totalCycle = Math.Max(0.01f, baseCycle + reloadPerBurst);
                float sustainedDps = Math.Max(0f, damage * burstCount / totalCycle);

                stats = new CEWeaponStats
                {
                    SustainedDps = sustainedDps,
                    DamagePerShot = damage,
                    EffectivePen = effectivePen,
                    SharpPen = sharpPen,
                    BluntPen = bluntPen,
                    Warmup = warmup,
                    CooldownWithReload = cooldown + reloadPerBurst,
                    BurstCount = burstCount,
                    Range = range,
                    MinRange = minRange,
                    AmmoLabel = ammoLabel
                };
                return true;
            }
            catch (Exception exception)
            {
                LogOnce("Could not calculate CE weapon stats for " + weaponDef.defName, exception);
                return false;
            }
        }

        private static object FindCEAmmoProps(ThingDef weaponDef)
        {
            if (weaponDef?.comps == null || ceAmmoPropsType == null)
            {
                return null;
            }

            return weaponDef.comps.FirstOrDefault(comp => comp != null && ceAmmoPropsType.IsInstanceOfType(comp));
        }

        private static ThingDef GetStandardProjectile(
            VerbProperties verb,
            object ammoProps,
            out string ammoLabel)
        {
            ammoLabel = null;
            object ammoSet = GetFieldObject(ammoProps, "ammoSet");
            object ammoTypes = GetFieldObject(ammoSet, "ammoTypes");

            if (ammoTypes is IEnumerable links)
            {
                foreach (object link in links)
                {
                    ThingDef projectile = GetFieldObject(link, "projectile") as ThingDef;
                    if (projectile == null)
                    {
                        continue;
                    }

                    Def ammo = GetFieldObject(link, "ammo") as Def;
                    ammoLabel = ammo?.LabelCap.ToString();
                    return projectile;
                }
            }

            return verb?.defaultProjectile;
        }

        private static int GetAmmoSetConsumedPerShot(object ammoProps)
        {
            object ammoSet = GetFieldObject(ammoProps, "ammoSet");
            return GetField(ammoSet, "ammoConsumedPerShot", 1);
        }

        private static StatDef CEStat(string name)
        {
            return ceStatDefOfType?.GetField(
                name,
                BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as StatDef;
        }

        private static float StatValue(
            ThingDef def,
            ThingDef stuff,
            Thing thing,
            StatDef stat,
            float fallback)
        {
            if (stat == null)
            {
                return fallback;
            }

            try
            {
                float value = thing != null
                    ? thing.GetStatValue(stat)
                    : def.GetStatValueAbstract(stat, stuff);

                return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
            }
            catch
            {
                return fallback;
            }
        }

        private static T GetField<T>(object instance, string name, T fallback)
        {
            object value = GetFieldObject(instance, name);
            if (value is T typed)
            {
                return typed;
            }

            try
            {
                return value == null ? fallback : (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return fallback;
            }
        }

        private static object GetFieldObject(object instance, string name)
        {
            if (instance == null)
            {
                return null;
            }

            FieldInfo field = AccessTools.Field(instance.GetType(), name);
            return field?.GetValue(instance);
        }

        private static void SetRec(object rec, string name, object value)
        {
            if (rec != null && RecFields.TryGetValue(name, out FieldInfo field))
            {
                field.SetValue(rec, value);
            }
        }

        private static T GetRec<T>(object rec, string name) where T : class
        {
            if (rec != null && RecFields.TryGetValue(name, out FieldInfo field))
            {
                return field.GetValue(rec) as T;
            }
            return null;
        }

        private static void LogOnce(string context, Exception exception)
        {
            if (errorLogged)
            {
                return;
            }

            errorLogged = true;
            Exception actual = exception is TargetInvocationException invocation && invocation.InnerException != null
                ? invocation.InnerException
                : exception;
            Log.Warning("[Quartermaster RU/CE] " + context + ": " + actual);
        }

        private struct CEWeaponStats
        {
            public float SustainedDps;
            public float DamagePerShot;
            public float EffectivePen;
            public float SharpPen;
            public float BluntPen;
            public float Warmup;
            public float CooldownWithReload;
            public int BurstCount;
            public float Range;
            public float MinRange;
            public string AmmoLabel;

            public bool Reachable(float distance)
            {
                return distance >= MinRange && distance <= Range;
            }
        }
    }
}
