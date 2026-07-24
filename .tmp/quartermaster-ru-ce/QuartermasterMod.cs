using System;
using HarmonyLib;
using Verse;

namespace Alik.QuartermasterRussianCE
{
    public sealed class QuartermasterRussianCEPatchMod : Mod
    {
        private const string HarmonyId = "alik.quartermaster.russian.ce";
        private readonly ModContentPack content;

        public QuartermasterRussianCEPatchMod(ModContentPack content) : base(content)
        {
            this.content = content;
            LongEventHandler.ExecuteWhenFinished(Initialize);
        }

        private void Initialize()
        {
            try
            {
                Harmony harmony = new Harmony(HarmonyId);
                int translatedMethods = QuartermasterTranslation.Install(harmony, content);
                bool ceInstalled = QuartermasterCECompatibility.Install(harmony);

                Log.Message(
                    "[Quartermaster RU/CE] Russian UI patch installed for " + translatedMethods +
                    " method(s). CE compatibility active: " + ceInstalled + ".");
            }
            catch (Exception exception)
            {
                Log.Error("[Quartermaster RU/CE] Initialization failed: " + exception);
            }
        }
    }
}
