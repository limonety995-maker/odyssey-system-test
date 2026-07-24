using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml.Linq;
using HarmonyLib;
using Verse;

namespace Alik.QuartermasterRussianCE
{
    internal static class QuartermasterTranslation
    {
        private static readonly Dictionary<string, string> Global =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> PerMethod =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<short, OpCode> OneByteOpCodes =
            new Dictionary<short, OpCode>();
        private static readonly Dictionary<short, OpCode> TwoByteOpCodes =
            new Dictionary<short, OpCode>();

        static QuartermasterTranslation()
        {
            foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (!(field.GetValue(null) is OpCode opCode))
                {
                    continue;
                }

                ushort value = unchecked((ushort)opCode.Value);
                if (value < 0x100)
                {
                    OneByteOpCodes[(short)value] = opCode;
                }
                else if ((value & 0xff00) == 0xfe00)
                {
                    TwoByteOpCodes[(short)(value & 0xff)] = opCode;
                }
            }
        }

        public static int Install(Harmony harmony, ModContentPack content)
        {
            if (!IsRussianLanguage())
            {
                return 0;
            }

            LoadTranslations(content);
            if (Global.Count == 0 && PerMethod.Count == 0)
            {
                Log.Warning("[Quartermaster RU/CE] Translation data was not found.");
                return 0;
            }

            Type anchor = AccessTools.TypeByName("Quartermaster.MainTabWindow_BestArmor");
            Assembly assembly = anchor?.Assembly;
            if (assembly == null)
            {
                Log.Warning("[Quartermaster RU/CE] Quartermaster assembly was not found.");
                return 0;
            }

            HarmonyMethod transpiler = new HarmonyMethod(
                typeof(QuartermasterTranslation),
                nameof(TranslateStrings));

            int patched = 0;
            foreach (Type type in SafeGetTypes(assembly))
            {
                if (type == null || type.Namespace == null ||
                    !type.Namespace.StartsWith("Quartermaster", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (MethodBase method in type.GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Cast<MethodBase>()
                    .Concat(type.GetConstructors(
                        BindingFlags.Public | BindingFlags.NonPublic |
                        BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)))
                {
                    if (!CanPatch(method) || !ContainsTranslatableString(method))
                    {
                        continue;
                    }

                    try
                    {
                        harmony.Patch(method, transpiler: transpiler);
                        patched++;
                    }
                    catch (Exception exception)
                    {
                        Log.Warning("[Quartermaster RU/CE] Could not patch strings in " +
                            MethodKey(method) + ": " + exception.Message);
                    }
                }
            }

            return patched;
        }

        public static IEnumerable<CodeInstruction> TranslateStrings(
            IEnumerable<CodeInstruction> instructions,
            MethodBase original)
        {
            string methodKey = MethodKey(original);
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldstr && instruction.operand is string source &&
                    TryGetTranslation(methodKey, source, out string target))
                {
                    instruction.operand = target;
                }

                yield return instruction;
            }
        }

        private static bool IsRussianLanguage()
        {
            try
            {
                string folder = LanguageDatabase.activeLanguage?.folderName;
                return string.Equals(folder, "Russian", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(folder, "Russian (Русский)", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return true;
            }
        }

        private static void LoadTranslations(ModContentPack content)
        {
            Global.Clear();
            PerMethod.Clear();

            string directory = Path.Combine(content.RootDir, "Translations");
            if (!Directory.Exists(directory))
            {
                return;
            }

            foreach (string path in Directory.GetFiles(directory, "Translations*.xml")
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    XDocument document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
                    foreach (XElement entry in document.Root?.Elements("Entry") ?? Enumerable.Empty<XElement>())
                    {
                        string kind = entry.Element("Kind")?.Value;
                        string method = entry.Element("Method")?.Value;
                        string source = entry.Element("Source")?.Value;
                        string target = entry.Element("Target")?.Value;

                        if (source == null || target == null)
                        {
                            continue;
                        }

                        if (string.Equals(kind, "method", StringComparison.OrdinalIgnoreCase) &&
                            !method.NullOrEmpty())
                        {
                            PerMethod[method + "\n" + source] = target;
                        }
                        else
                        {
                            Global[source] = target;
                        }
                    }
                }
                catch (Exception exception)
                {
                    Log.Warning("[Quartermaster RU/CE] Could not read " + path + ": " + exception.Message);
                }
            }
        }

        private static bool TryGetTranslation(string methodKey, string source, out string target)
        {
            if (PerMethod.TryGetValue(methodKey + "\n" + source, out target))
            {
                return true;
            }

            return Global.TryGetValue(source, out target);
        }

        private static bool CanPatch(MethodBase method)
        {
            if (method == null || method.IsAbstract || method.ContainsGenericParameters)
            {
                return false;
            }

            try
            {
                return method.GetMethodBody() != null;
            }
            catch
            {
                return false;
            }
        }

        private static bool ContainsTranslatableString(MethodBase method)
        {
            MethodBody body;
            byte[] il;
            try
            {
                body = method.GetMethodBody();
                il = body?.GetILAsByteArray();
            }
            catch
            {
                return false;
            }

            if (il == null || il.Length == 0)
            {
                return false;
            }

            string methodKey = MethodKey(method);
            int position = 0;
            while (position < il.Length)
            {
                OpCode opcode;
                byte first = il[position++];
                if (first == 0xfe)
                {
                    if (position >= il.Length || !TwoByteOpCodes.TryGetValue(il[position++], out opcode))
                    {
                        return false;
                    }
                }
                else if (!OneByteOpCodes.TryGetValue(first, out opcode))
                {
                    return false;
                }

                if (opcode.OperandType == OperandType.InlineString)
                {
                    if (position + 4 > il.Length)
                    {
                        return false;
                    }

                    int token = BitConverter.ToInt32(il, position);
                    try
                    {
                        string value = method.Module.ResolveString(token);
                        if (TryGetTranslation(methodKey, value, out _))
                        {
                            return true;
                        }
                    }
                    catch
                    {
                    }
                }

                int operandSize = OperandSize(opcode.OperandType, il, position);
                if (operandSize < 0 || position + operandSize > il.Length)
                {
                    return false;
                }

                position += operandSize;
            }

            return false;
        }

        private static int OperandSize(OperandType type, byte[] il, int position)
        {
            switch (type)
            {
                case OperandType.InlineNone:
                    return 0;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    return 1;
                case OperandType.InlineVar:
                    return 2;
                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineI:
                case OperandType.InlineMethod:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineSwitch:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                case OperandType.ShortInlineR:
                    if (type == OperandType.InlineSwitch)
                    {
                        if (position + 4 > il.Length)
                        {
                            return -1;
                        }

                        int count = BitConverter.ToInt32(il, position);
                        return 4 + Math.Max(0, count) * 4;
                    }
                    return 4;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    return 8;
                default:
                    return -1;
            }
        }

        private static string MethodKey(MethodBase method)
        {
            return (method?.DeclaringType?.FullName ?? string.Empty) + "::" + (method?.Name ?? string.Empty);
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(type => type != null);
            }
        }
    }
}
