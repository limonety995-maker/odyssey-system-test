using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Alik.MFI.MysticalShamanFix
{
    public sealed class MFIMysticalShamanFixMod : Mod
    {
        private const string HarmonyId = "alik.mfi.mysticalshaman.def.fix";
        private const string VeeDefsAltererTypeName = "VEE.Settings.DefsAlterer";

        public MFIMysticalShamanFixMod(ModContentPack content) : base(content)
        {
            try
            {
                Type defsAltererType = AccessTools.TypeByName(VeeDefsAltererTypeName);
                if (defsAltererType == null)
                {
                    Log.Warning("[MFI Mystical Shaman Fix] Vanilla Events Expanded was not found; no compatibility patch was needed.");
                    return;
                }

                MethodInfo removeDefMethod = AccessTools.Method(defsAltererType, "RemoveDef", new[] { typeof(IncidentDef) });
                if (removeDefMethod == null)
                {
                    Log.Error("[MFI Mystical Shaman Fix] Could not find VEE.Settings.DefsAlterer.RemoveDef(IncidentDef).");
                    return;
                }

                MethodInfo prefix = AccessTools.Method(typeof(MFIMysticalShamanFixMod), nameof(RemoveDefPrefix));
                new Harmony(HarmonyId).Patch(removeDefMethod, prefix: new HarmonyMethod(prefix));
                Log.Message("[MFI Mystical Shaman Fix] Prevented Vanilla Events Expanded from removing MFI_MysticalShaman.");
            }
            catch (Exception ex)
            {
                Log.Error("[MFI Mystical Shaman Fix] Failed to apply compatibility patch: " + ex);
            }
        }

        private static bool RemoveDefPrefix(IncidentDef def)
        {
            return def == null || def.defName != "MFI_MysticalShaman";
        }
    }
}
