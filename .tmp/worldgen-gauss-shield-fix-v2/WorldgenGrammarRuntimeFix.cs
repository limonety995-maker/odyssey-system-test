using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace Alik.WorldgenGaussShieldFix
{
    public sealed class WorldgenGaussShieldFixMod : Mod
    {
        public WorldgenGaussShieldFixMod(ModContentPack content) : base(content)
        {
            LongEventHandler.ExecuteWhenFinished(ApplyRuntimeGrammarFixes);
        }

        private static void ApplyRuntimeGrammarFixes()
        {
            try
            {
                string languageFolder = LanguageDatabase.activeLanguage?.folderName;
                if (!string.Equals(languageFolder, "Russian", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Message("[Alik Worldgen Fix v2] Russian grammar repair skipped because the active language is " +
                        (languageFolder ?? "unknown") + ".");
                    return;
                }

                int namersFixed = 0;
                foreach (RulePackDef def in DefDatabase<RulePackDef>.AllDefsListForReading)
                {
                    if (def?.rulePack == null || def.defName.NullOrEmpty())
                    {
                        continue;
                    }

                    bool targeted =
                        def.defName.StartsWith("VEE_NamerLandmark_", StringComparison.Ordinal) ||
                        def.defName.StartsWith("VGE_NamerWorldObject_", StringComparison.Ordinal);

                    if (!targeted || !HasRule(def, "terrain_word") || HasRule(def, "r_name"))
                    {
                        continue;
                    }

                    if (def.rulePack.rulesStrings == null)
                    {
                        def.rulePack.rulesStrings = new List<string>();
                    }

                    def.rulePack.rulesStrings.Insert(0, "r_name->[terrain_word]");
                    ClearRuleCaches(def);
                    namersFixed++;
                }

                int traderRulesFixed = FixTraderGuildRule();

                Log.Message(
                    "[Alik Worldgen Fix v2] Runtime Russian grammar repair complete. " +
                    "Namers fixed: " + namersFixed + ", trader rules fixed: " + traderRulesFixed + ".");
            }
            catch (Exception exception)
            {
                Log.Error("[Alik Worldgen Fix v2] Runtime grammar repair failed: " + exception);
            }
        }

        private static int FixTraderGuildRule()
        {
            RulePackDef def = DefDatabase<RulePackDef>.GetNamedSilentFail("NamerFactionTradersGuild");
            List<string> rules = def?.rulePack?.rulesStrings;
            if (rules == null)
            {
                return 0;
            }

            const string malformed = "[tradeAdj_fem] [tradeNoun_fem]";
            const string corrected = "r_name->[tradeAdj_fem] [tradeNoun_fem]";
            int fixedCount = 0;

            for (int index = 0; index < rules.Count; index++)
            {
                if (string.Equals(rules[index]?.Trim(), malformed, StringComparison.Ordinal))
                {
                    rules[index] = corrected;
                    fixedCount++;
                }
            }

            if (fixedCount > 0)
            {
                ClearRuleCaches(def);
            }

            return fixedCount;
        }

        private static bool HasRule(RulePackDef def, string keyword)
        {
            List<string> rules = def?.rulePack?.rulesStrings;
            if (rules == null)
            {
                return false;
            }

            return rules.Any(rule => RuleKeyword(rule) == keyword);
        }

        private static string RuleKeyword(string rule)
        {
            if (rule.NullOrEmpty())
            {
                return null;
            }

            int arrow = rule.IndexOf("->", StringComparison.Ordinal);
            if (arrow <= 0)
            {
                return null;
            }

            string left = rule.Substring(0, arrow).Trim();
            int parameters = left.IndexOf('(');
            if (parameters > 0)
            {
                left = left.Substring(0, parameters).Trim();
            }

            return left;
        }

        private static void ClearRuleCaches(RulePackDef def)
        {
            ClearField(def.rulePack, "rules");
            ClearField(def, "rulesPlusIncludes");
            ClearField(def, "cachedRulesPlusIncludes");
            ClearField(def, "firstRuleKeyword");
            ClearField(def, "cachedFirstRuleKeyword");
        }

        private static void ClearField(object instance, string fieldName)
        {
            if (instance == null)
            {
                return;
            }

            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (field == null || field.IsInitOnly)
            {
                return;
            }

            object value = field.FieldType.IsValueType
                ? Activator.CreateInstance(field.FieldType)
                : null;
            field.SetValue(instance, value);
        }
    }
}
