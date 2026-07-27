using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace Alik.WorldgenGaussShieldFix
{
    public sealed class WorldgenGaussShieldFixMod : Mod
    {
        private const string HarmonyId = "alik.worldgen.gauss.shield.fix.v2";

        public WorldgenGaussShieldFixMod(ModContentPack content) : base(content)
        {
            Harmony harmony = new Harmony(HarmonyId);

            Type loadedLanguageType = AccessTools.TypeByName("Verse.LoadedLanguage");
            MethodInfo injectMethod = loadedLanguageType == null
                ? null
                : AccessTools.Method(loadedLanguageType, "InjectIntoData_AfterImpliedDefs");

            if (injectMethod != null)
            {
                harmony.Patch(
                    injectMethod,
                    postfix: new HarmonyMethod(
                        typeof(WorldgenGaussShieldFixMod),
                        nameof(ApplyRuntimeGrammarFixes)));
            }
            else
            {
                Log.Warning("[Alik Worldgen Fix v2] Could not patch LoadedLanguage.InjectIntoData_AfterImpliedDefs; using startup fallback only.");
            }

            LongEventHandler.ExecuteWhenFinished(ApplyRuntimeGrammarFixes);
        }

        public static void ApplyRuntimeGrammarFixes()
        {
            if (!IsRussianActive())
            {
                return;
            }

            try
            {
                int rootsAdded = 0;
                int malformedRulesFixed = 0;

                foreach (RulePackDef def in DefDatabase<RulePackDef>.AllDefsListForReading)
                {
                    if (def == null || def.defName.NullOrEmpty())
                    {
                        continue;
                    }

                    if (def.defName.StartsWith("VEE_NamerLandmark_", StringComparison.Ordinal) ||
                        def.defName.StartsWith("VGE_NamerWorldObject_", StringComparison.Ordinal))
                    {
                        if (EnsureRootRule(def))
                        {
                            rootsAdded++;
                        }
                    }
                }

                RulePackDef tradersGuild = DefDatabase<RulePackDef>.GetNamedSilentFail("NamerFactionTradersGuild");
                if (tradersGuild != null && FixTradersGuildRule(tradersGuild))
                {
                    malformedRulesFixed++;
                }

                if (rootsAdded > 0 || malformedRulesFixed > 0)
                {
                    Log.Message(
                        "[Alik Worldgen Fix v2] Runtime grammar fixes applied. " +
                        "Missing r_name roots added: " + rootsAdded +
                        "; malformed traders-guild rules fixed: " + malformedRulesFixed + ".");
                }
            }
            catch (Exception exception)
            {
                Log.Error("[Alik Worldgen Fix v2] Runtime grammar fix failed: " + exception);
            }
        }

        private static bool EnsureRootRule(RulePackDef def)
        {
            List<string> rulesStrings = GetRulesStrings(def, out object rulePack);
            if (rulesStrings == null)
            {
                return false;
            }

            bool hasTerrainWord = false;
            foreach (string rule in rulesStrings)
            {
                string trimmed = rule?.Trim();
                if (trimmed.NullOrEmpty())
                {
                    continue;
                }

                if (trimmed.StartsWith("r_name", StringComparison.Ordinal))
                {
                    return false;
                }

                if (trimmed.StartsWith("terrain_word", StringComparison.Ordinal))
                {
                    hasTerrainWord = true;
                }
            }

            if (!hasTerrainWord)
            {
                return false;
            }

            rulesStrings.Insert(0, "r_name->[terrain_word]");
            InvalidateCaches(def, rulePack);
            return true;
        }

        private static bool FixTradersGuildRule(RulePackDef def)
        {
            List<string> rulesStrings = GetRulesStrings(def, out object rulePack);
            if (rulesStrings == null)
            {
                return false;
            }

            const string malformed = "[tradeAdj_fem] [tradeNoun_fem]";
            const string corrected = "r_name->[tradeAdj_fem] [tradeNoun_fem]";
            bool changed = false;

            for (int i = 0; i < rulesStrings.Count; i++)
            {
                string trimmed = rulesStrings[i]?.Trim();
                if (string.Equals(trimmed, malformed, StringComparison.Ordinal))
                {
                    rulesStrings[i] = corrected;
                    changed = true;
                }
            }

            if (changed)
            {
                InvalidateCaches(def, rulePack);
            }

            return changed;
        }

        private static List<string> GetRulesStrings(RulePackDef def, out object rulePack)
        {
            rulePack = null;
            if (def == null)
            {
                return null;
            }

            FieldInfo rulePackField = AccessTools.Field(def.GetType(), "rulePack");
            rulePack = rulePackField?.GetValue(def);
            if (rulePack == null)
            {
                return null;
            }

            FieldInfo rulesStringsField = AccessTools.Field(rulePack.GetType(), "rulesStrings");
            return rulesStringsField?.GetValue(rulePack) as List<string>;
        }

        private static void InvalidateCaches(RulePackDef def, object rulePack)
        {
            ClearLikelyCaches(rulePack);
            ClearLikelyCaches(def);
        }

        private static void ClearLikelyCaches(object instance)
        {
            if (instance == null)
            {
                return;
            }

            foreach (FieldInfo field in instance.GetType().GetFields(
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                if (field.IsInitOnly || field.IsLiteral)
                {
                    continue;
                }

                string name = field.Name ?? string.Empty;
                bool likelyCache =
                    name.IndexOf("cache", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    string.Equals(name, "rules", StringComparison.OrdinalIgnoreCase) ||
                    name.IndexOf("firstRule", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("rulesPlusIncludes", StringComparison.OrdinalIgnoreCase) >= 0;

                if (!likelyCache || field.FieldType.IsValueType)
                {
                    continue;
                }

                try
                {
                    field.SetValue(instance, null);
                }
                catch
                {
                }
            }
        }

        private static bool IsRussianActive()
        {
            try
            {
                string folderName = LanguageDatabase.activeLanguage?.folderName;
                return string.Equals(folderName, "Russian", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(folderName, "Russian (Русский)", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return true;
            }
        }
    }
}
