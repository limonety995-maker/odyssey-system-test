using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Alik.CEVEEWeaponPodFix
{
    public sealed class CEVEEWeaponPodFixMod : Mod
    {
        private const string HarmonyId = "alik.ce.vee.weaponpod.fix";
        private const string CECompatibilityTypeName =
            "CombatExtended.HarmonyCE.Compatibility.Harmony_Compat_VanillaEventExpanded+Harmony_WeaponPod_Patch";
        private const string VEEWeaponPodTypeName = "VEE.RegularEvents.WeaponPod";
        private const string CEAmmoUtilityTypeName = "CombatExtended.CE_ThingSetMakerUtility";

        private static MethodInfo decideWeaponMethod;
        private static MethodInfo generateAmmoMethod;
        private static bool ammoFailureLogged;

        public CEVEEWeaponPodFixMod(ModContentPack content) : base(content)
        {
            Harmony harmony = new Harmony(HarmonyId);

            bool cePatchSuppressed = SuppressBrokenCEPatch(harmony);
            bool veePatchApplied = PatchWeaponPod(harmony);

            if (cePatchSuppressed && veePatchApplied)
            {
                Log.Message("[CE-VEE WeaponPod Fix] Native CE WeaponPod transpiler disabled; compatible replacement installed.");
            }
            else
            {
                Log.Warning(
                    "[CE-VEE WeaponPod Fix] Initialization was incomplete. " +
                    "CE patch suppressed=" + cePatchSuppressed + ", VEE patch applied=" + veePatchApplied + ".");
            }
        }

        private static bool SuppressBrokenCEPatch(Harmony harmony)
        {
            Type patchType = AccessTools.TypeByName(CECompatibilityTypeName);
            if (patchType == null)
            {
                Log.Warning("[CE-VEE WeaponPod Fix] CE Vanilla Events compatibility patch type was not found.");
                return false;
            }

            MethodInfo prepare = AccessTools.Method(patchType, "Prepare");
            MethodInfo transpiler = AccessTools.Method(patchType, "Transpiler");
            bool patchedAny = false;

            if (prepare != null)
            {
                harmony.Patch(
                    prepare,
                    prefix: new HarmonyMethod(
                        typeof(CEVEEWeaponPodFixMod),
                        nameof(SkipNativePrepare),
                        new[] { typeof(bool).MakeByRefType() })
                    {
                        priority = Priority.First
                    });
                patchedAny = true;
            }

            // Defensive fallback: even if a future CE path invokes the transpiler without Prepare,
            // return the original instructions without running the outdated injection search.
            if (transpiler != null)
            {
                harmony.Patch(
                    transpiler,
                    prefix: new HarmonyMethod(
                        typeof(CEVEEWeaponPodFixMod),
                        nameof(SkipNativeTranspiler))
                    {
                        priority = Priority.First
                    });
                patchedAny = true;
            }

            return patchedAny;
        }

        private static bool PatchWeaponPod(Harmony harmony)
        {
            Type weaponPodType = AccessTools.TypeByName(VEEWeaponPodTypeName);
            if (weaponPodType == null)
            {
                Log.Warning("[CE-VEE WeaponPod Fix] VEE WeaponPod type was not found.");
                return false;
            }

            MethodInfo tryExecute = AccessTools.Method(
                weaponPodType,
                "TryExecuteWorker",
                new[] { typeof(IncidentParms) });
            decideWeaponMethod = AccessTools.Method(
                weaponPodType,
                "DecideWeapon",
                new[] { typeof(Map) });

            Type ammoUtilityType = AccessTools.TypeByName(CEAmmoUtilityTypeName);
            if (ammoUtilityType != null)
            {
                generateAmmoMethod = AccessTools.Method(
                    ammoUtilityType,
                    "GenerateAmmoForWeapon",
                    new[]
                    {
                        typeof(List<Thing>),
                        typeof(bool),
                        typeof(bool),
                        typeof(IntRange)
                    });
            }

            if (tryExecute == null || decideWeaponMethod == null)
            {
                Log.Warning("[CE-VEE WeaponPod Fix] Current VEE WeaponPod methods do not match the supported 1.6 implementation.");
                return false;
            }

            harmony.Patch(
                tryExecute,
                prefix: new HarmonyMethod(
                    typeof(CEVEEWeaponPodFixMod),
                    nameof(WeaponPodPrefix))
                {
                    priority = Priority.First
                });

            return true;
        }

        private static bool SkipNativePrepare(ref bool __result)
        {
            __result = false;
            return false;
        }

        private static bool SkipNativeTranspiler(
            IEnumerable<CodeInstruction> instructions,
            ref IEnumerable<CodeInstruction> __result)
        {
            __result = instructions;
            return false;
        }

        private static bool WeaponPodPrefix(object __instance, IncidentParms __0, ref bool __result)
        {
            Map map = __0.target as Map;
            if (map == null)
            {
                __result = false;
                return false;
            }

            Thing weapon;
            try
            {
                weapon = decideWeaponMethod.Invoke(__instance, new object[] { map }) as Thing;
            }
            catch (Exception exception)
            {
                Log.Error("[CE-VEE WeaponPod Fix] VEE failed to choose a weapon: " + Unwrap(exception));
                __result = false;
                return false;
            }

            if (weapon == null)
            {
                Log.Error("[CE-VEE WeaponPod Fix] VEE returned no weapon for the WeaponPod event.");
                __result = false;
                return false;
            }

            IntVec3 dropCell = DropCellFinder.RandomDropSpot(map);
            List<Thing> things = new List<Thing> { weapon };

            if (generateAmmoMethod != null)
            {
                try
                {
                    generateAmmoMethod.Invoke(
                        null,
                        new object[] { things, true, true, new IntRange(1, 3) });
                }
                catch (Exception exception)
                {
                    if (!ammoFailureLogged)
                    {
                        ammoFailureLogged = true;
                        Log.Warning(
                            "[CE-VEE WeaponPod Fix] Ammo generation failed; the weapon pod will still arrive: " +
                            Unwrap(exception));
                    }
                }
            }
            else if (!ammoFailureLogged)
            {
                ammoFailureLogged = true;
                Log.Warning("[CE-VEE WeaponPod Fix] CE ammo utility was not found; the weapon pod will arrive without generated ammo.");
            }

            DropPodUtility.DropThingsNear(dropCell, map, things, 110, false, true, true);
            Find.LetterStack.ReceiveLetter(
                "VEE_WeaponCargoPodLabel".Translate(),
                "VEE_WeaponCargoPodDesc".Translate(),
                LetterDefOf.PositiveEvent,
                new TargetInfo(dropCell, map, false),
                null,
                null);

            __result = true;
            return false;
        }

        private static Exception Unwrap(Exception exception)
        {
            return exception is TargetInvocationException target && target.InnerException != null
                ? target.InnerException
                : exception;
        }
    }
}
