using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;

namespace Alik.VGE.MechanoidSignalFix
{
    public sealed class QuestNode_Root_MechanoidSignal_Fixed : QuestNode
    {
        private const string LandingStructureDefName = "VGE_LandingStructure";
        private const string DamagedLayoutDefName = "VGE_StartingGravjumperDamaged";
        private const string GravjumperEngineDefName = "VGE_GravjumperEngine";
        private const string InteractiveSpawnPartTypeName = "VanillaGravshipExpanded.QuestPart_SpawnThingInteractive";
        private const string GravshipUtilityTypeName = "VanillaGravshipExpanded.GravshipUtility";

        protected override void RunInt()
        {
            Quest quest = QuestGen.quest;
            Map map = QuestGen_Get.GetMap();
            string inSignal = QuestGen.slate.Get<string>("inSignal");
            string landingStructureSpawnedSignal = QuestGen.GenerateNewSignal("LandingStructureSpawned");

            QuestUtility.AddQuestTag(
                map.Parent,
                QuestGenUtility.HardcodedTargetQuestTagWithQuestID("gravEngine"));

            Thing landingStructure = MakeLandingStructure();
            QuestPart interactiveSpawnPart = MakeInteractiveSpawnPart(
                landingStructure,
                map.Parent,
                inSignal,
                landingStructureSpawnedSignal);
            quest.AddPart(interactiveSpawnPart);

            quest.Letter(
                LetterDefOf.PositiveEvent,
                inSignal: landingStructureSpawnedSignal,
                label: "[gravEngineSpawnedLetterLabel]",
                text: "[gravEngineSpawnedLetterText]");

            float threatPoints = StorytellerUtility.DefaultThreatPointsNow(map);
            int chunkAmount = Mathf.Max((int)(threatPoints / 1000f), 1);
            quest.AddPart(new QuestPart_DeferredMechanoidDrop
            {
                inSignal = landingStructureSpawnedSignal,
                mapParent = map.Parent,
                tryLandNearThing = landingStructure,
                chunkAmount = chunkAmount
            });

            QuestPart_Choice choicePart = quest.RewardChoice();
            QuestPart_Choice.Choice choice = new QuestPart_Choice.Choice();
            choice.rewards.Add(new Reward_DefinedThingDef
            {
                thingDef = DefDatabase<ThingDef>.GetNamed(GravjumperEngineDefName)
            });
            choicePart.choices.Add(choice);

            quest.End(QuestEndOutcome.Success, 0, null, landingStructureSpawnedSignal);
        }

        protected override bool TestRunInt(Slate slate)
        {
            if (QuestGen_Get.GetMap() == null)
            {
                return false;
            }

            Type utilityType = ReflectionUtility.FindType(GravshipUtilityTypeName);
            MethodInfo method = utilityType.GetMethod(
                "PlayerHasGravEngine",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null)
            {
                Log.Error("[VGE Mechanoid Signal Fix] Could not find GravshipUtility.PlayerHasGravEngine.");
                return false;
            }

            return !(bool)method.Invoke(null, null);
        }

        private static Thing MakeLandingStructure()
        {
            ThingDef landingDef = DefDatabase<ThingDef>.GetNamed(LandingStructureDefName);
            Thing landingStructure = ThingMaker.MakeThing(landingDef);

            ReflectionUtility.SetField(landingStructure, "forceNullFaction", true);

            FieldInfo layoutField = ReflectionUtility.FindField(landingStructure.GetType(), "layoutDef");
            object layoutDef = ReflectionUtility.GetNamedDef(layoutField.FieldType, DamagedLayoutDefName);
            layoutField.SetValue(landingStructure, layoutDef);

            return landingStructure;
        }

        private static QuestPart MakeInteractiveSpawnPart(
            Thing landingStructure,
            MapParent mapParent,
            string inSignal,
            string outSignal)
        {
            Type partType = ReflectionUtility.FindType(InteractiveSpawnPartTypeName);
            QuestPart part = (QuestPart)Activator.CreateInstance(partType);

            ReflectionUtility.SetField(part, "thing", landingStructure);
            ReflectionUtility.SetField(part, "mapParent", mapParent);
            ReflectionUtility.SetField(part, "inSignal", inSignal);
            ReflectionUtility.SetField(part, "questLookTarget", true);
            ReflectionUtility.SetField(part, "outSignalResult", outSignal);

            return part;
        }
    }

    public sealed class QuestPart_DeferredMechanoidDrop : QuestPart
    {
        private const string AstropedeDefName = "VGE_Astropede";
        private const string HunterDefName = "VGE_Hunter";

        public string inSignal;
        public MapParent mapParent;
        public Thing tryLandNearThing;
        public int chunkAmount = 1;

        private bool spawned;

        public override void Notify_QuestSignalReceived(Signal signal)
        {
            base.Notify_QuestSignalReceived(signal);

            if (spawned || signal.tag != inSignal)
            {
                return;
            }

            Map map = mapParent?.Map;
            if (map == null)
            {
                Log.Error("[VGE Mechanoid Signal Fix] Could not spawn mechanoid chunks because the target map is unavailable.");
                return;
            }

            PawnKindDef astropede = DefDatabase<PawnKindDef>.GetNamed(AstropedeDefName);
            PawnKindDef hunter = DefDatabase<PawnKindDef>.GetNamed(HunterDefName);
            List<PawnKindDef> mechTypes = new List<PawnKindDef> { astropede, hunter };

            int count = Mathf.Max(chunkAmount, 1);
            for (int i = 0; i < count; i++)
            {
                SpawnChunk(map, mechTypes);
            }

            spawned = true;
        }

        private void SpawnChunk(Map map, List<PawnKindDef> mechTypes)
        {
            List<Thing> contents = MakeChunkContents(mechTypes);
            Skyfaller skyfaller = SkyfallerMaker.MakeSkyfaller(
                ThingDefOf.ShipChunkIncoming_SmallExplosion,
                contents);
            skyfaller.contentsCanOverlap = false;
            skyfaller.moveAside = true;

            IntVec3 spawnCell = FindSpawnCell(map, skyfaller.def.size);
            if (!GenPlace.TryPlaceThing(skyfaller, spawnCell, map, ThingPlaceMode.Near))
            {
                Log.Error("[VGE Mechanoid Signal Fix] Failed to place a mechanoid skyfaller on the map.");
            }
        }

        private static List<Thing> MakeChunkContents(List<PawnKindDef> mechTypes)
        {
            List<Thing> contents = new List<Thing>
            {
                ThingMaker.MakeThing(ThingDefOf.ShipChunk_Mech)
            };

            int remaining = 1000;
            while (remaining > 0)
            {
                PawnKindDef kind = mechTypes.RandomElement();
                Pawn pawn = PawnGenerator.GeneratePawn(kind, Faction.OfMechanoids);
                contents.Add(pawn);
                remaining -= Mathf.Max((int)kind.combatPower, 1);
            }

            return contents;
        }

        private IntVec3 FindSpawnCell(Map map, IntVec2 size)
        {
            IntVec3 result = IntVec3.Invalid;

            if (tryLandNearThing != null &&
                tryLandNearThing.Spawned &&
                tryLandNearThing.Map == map)
            {
                DropCellFinder.FindSafeLandingSpotNearAvoidingHostiles(
                    tryLandNearThing,
                    map,
                    out result,
                    35,
                    15,
                    25,
                    size);
            }

            if (!result.IsValid &&
                !DropCellFinder.FindSafeLandingSpot(
                    out result,
                    Faction.OfMechanoids,
                    map,
                    35,
                    15,
                    25,
                    size))
            {
                IntVec3 randomSpot = DropCellFinder.RandomDropSpot(map);
                if (!DropCellFinder.TryFindDropSpotNear(
                        randomSpot,
                        map,
                        out result,
                        allowFogged: false,
                        canRoofPunch: false,
                        allowIndoors: false,
                        size))
                {
                    result = randomSpot;
                }
            }

            return result;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref inSignal, "inSignal");
            Scribe_References.Look(ref mapParent, "mapParent");
            Scribe_References.Look(ref tryLandNearThing, "tryLandNearThing");
            Scribe_Values.Look(ref chunkAmount, "chunkAmount", 1);
            Scribe_Values.Look(ref spawned, "spawned", false);
        }
    }

    internal static class ReflectionUtility
    {
        private const BindingFlags AllInstanceFields =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            throw new TypeLoadException("Could not find type " + fullName);
        }

        public static FieldInfo FindField(Type type, string fieldName)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(fieldName, AllInstanceFields);
                if (field != null)
                {
                    return field;
                }
            }

            throw new MissingFieldException(type.FullName, fieldName);
        }

        public static void SetField(object instance, string fieldName, object value)
        {
            FindField(instance.GetType(), fieldName).SetValue(instance, value);
        }

        public static object GetNamedDef(Type defType, string defName)
        {
            Type databaseType = typeof(DefDatabase<>).MakeGenericType(defType);
            MethodInfo getNamed = databaseType.GetMethod(
                "GetNamed",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string), typeof(bool) },
                null);

            if (getNamed == null)
            {
                throw new MissingMethodException(databaseType.FullName, "GetNamed(string, bool)");
            }

            return getNamed.Invoke(null, new object[] { defName, true });
        }
    }
}
