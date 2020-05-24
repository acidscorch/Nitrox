using System;
using System.Reflection;
using Harmony;
using NitroxClient.GameLogic;
using NitroxClient.GameLogic.Bases;
using NitroxModel.Core;

namespace NitroxPatcher.Patches.Dynamic
{
    public class ConstructableBase_SetState_Patch : NitroxPatch, IDynamicPatch
    {
        public static readonly Type TARGET_CLASS = typeof(ConstructableBase);
        public static readonly MethodInfo TARGET_METHOD = TARGET_CLASS.GetMethod("SetState", BindingFlags.Public | BindingFlags.Instance);

        public static void Prefix(ConstructableBase __instance, bool __result, bool value, bool setAmount)
        {
            NitroxServiceLocator.LocateService<GeometryLayoutChangeHandler>().ConstructableBase_SetState_Pre(__instance, value, setAmount);
        }

        public override void Patch(HarmonyInstance harmony)
        {
            PatchPrefix(harmony, TARGET_METHOD);
        }
    }

    /*public class ConstructableBase_DestroyModelCopy_Patch : NitroxPatch
    {
        public static readonly Type TARGET_CLASS = typeof(ConstructableBase);
        public static readonly MethodInfo TARGET_METHOD = TARGET_CLASS.GetMethod("DestroyModelCopy", BindingFlags.NonPublic | BindingFlags.Instance);

        public static bool Prefix(ConstructableBase __instance)
        {
            return NitroxServiceLocator.LocateService<Building>().ConstructableBase_Pre_DestroyModelCopy(__instance);
        }

        public static void Postfix(ConstructableBase __instance)
        {
            NitroxServiceLocator.LocateService<Building>().ConstructableBase_Post_DestroyModelCopy(__instance);
        }

        public override void Patch(HarmonyInstance harmony)
        {
            PatchMultiple(harmony, TARGET_METHOD, true, true, false);
        }
    }*/
}
