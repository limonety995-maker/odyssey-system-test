using System;
using System.Collections.Generic;
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
                if (languageFolder == null ||
                    !languageFolder.StartsWith("Russian", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Message("[Alik Worldgen Fix v2] Russian grammar repair skipped because the active language is " +
                        (languageFolder ?? "unknown") + ".");
                    return;
                }

                int namersFixed = 0;
                foreach (RulePackDef def in DefDatabase<RulePackDef>.AllDefsListForReading)
                {
                    if (def == null || def.defName.NullOrEmpty())
                    {
                        continue;
                    }

                    bool targeted =
                        def.defName.StartsWith("VEE_NamerLandmark_", StringComparison.Ordinal) ||
                        def.defName.StartsWith("VGE_NamerWorldObject_", StringComparison.Ordinal);

                    if (!targeted)
                    {
                        continue;
                    }

                    IList<string> rules = GetRulesStrings(def, true);
                    if (rules == null || !HasRule(rules, "terrain_word") || HasRule(rules, "r_name"))
                    {
                        continue;
                    }

                    rules.Insert(0, "r_name->[terrain_word]");
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
            IList<string> rules = GetRulesStrings(def, false);
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

        private static bool HasRule(IList<string> rules, string keyword)
        {
            if (rules == null)
            {
                return false;
            }

            for (int index = 0; index < rules.Count; index++)
            {
                if (string.Equals(RuleKeyword(rules[index]), keyword, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
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

        private static IList<string> GetRulesStrings(RulePackDef def, bool create)
        {
            if (def == null)
            {
                return null;
            }

            object rulePack = GetRulePackObject(def) ?? def;
            FieldInfo field = FindField(rulePack.GetType(), "rulesStrings");
            if (field != null)
            {
                IList<string> current = field.GetValue(rulePack) as IList<string>;
                if (current == null && create && !field.IsInitOnly)
                {
                    current = new List<string>();
                    field.SetValue(rulePack, current);
                }
                return current;
            }

            PropertyInfo property = FindProperty(rulePack.GetType(), "rulesStrings");
            if (property != null)
            {
                IList<string> current = property.GetValue(rulePack, null) as IList<string>;
                if (current == null && create && property.CanWrite)
                {
                    current = new List<string>();
                    property.SetValue(rulePack, current, null);
                }
                return current;
            }

            return null;
        }

        private static object GetRulePackObject(RulePackDef def)
        {
            FieldInfo namedField = FindField(def.GetType(), "rulePack");
            object value = namedField?.GetValue(def);
            if (value != null)
            {
                return value;
            }

            PropertyInfo namedProperty = FindProperty(def.GetType(), "rulePack") ??
                                         FindProperty(def.GetType(), "RulePack");
            value = namedProperty?.GetValue(def, null);
            if (value != null)
            {
                return value;
            }

            for (Type type = def.GetType(); type != null; type = type.BaseType)
            {
                foreach (FieldInfo field in type.GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.FieldType.FullName == "Verse.Grammar.RulePack")
                    {
                        value = field.GetValue(def);
                        if (value != null)
                        {
                            return value;
                        }
                    }
                }
            }

            return null;
        }

        private static void ClearRuleCaches(RulePackDef def)
        {
            object rulePack = GetRulePackObject(def);
            ClearField(rulePack, "rules");
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

            FieldInfo field = FindField(instance.GetType(), fieldName);
            if (field == null || field.IsInitOnly)
            {
                return;
            }

            object value = field.FieldType.IsValueType
                ? Activator.CreateInstance(field.FieldType)
                : null;
            field.SetValue(instance, value);
        }

        private static FieldInfo FindField(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(
                    name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field;
                }
            }

            return null;
        }

        private static PropertyInfo FindProperty(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                PropertyInfo property = current.GetProperty(
                    name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (property != null)
                {
                    return property;
                }
            }

            return null;
        }
    }
}
