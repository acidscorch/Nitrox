using System;
using System.Reflection;
using Harmony;
using NitroxClient.GameLogic;
using NitroxModel.Core;
using NitroxModel.DataStructures.Util;

namespace NitroxPatcher.Patches.Dynamic
{
    public class Constructable_Construct_Patch : NitroxPatch, IDynamicPatch
    {
        public static readonly Type TARGET_CLASS = typeof(Constructable);
        public static readonly MethodInfo TARGET_METHOD = TARGET_CLASS.GetMethod("Construct");

        public static bool Prefix(Constructable __instance, ref bool __result)
        {
            return NitroxServiceLocator.LocateService<Building>().Constructable_Construct_Pre(__instance, ref __result);
        }

        public static void Postfix(Constructable __instance, bool __result)
        {
            NitroxServiceLocator.LocateService<Building>().Constructable_Construct_Post(__instance, __result);
        }

        public override void Patch(HarmonyInstance harmony)
        {
            PatchMultiple(harmony, TARGET_METHOD, true, true, false);
        }

        /*public static readonly Type TARGET_CLASS = typeof(Constructable);
        public static readonly MethodInfo TARGET_METHOD = TARGET_CLASS.GetMethod("Construct");

        private static Base lastTargetBase;
        private static Int3 lastTargetBaseOffset;

        public static bool Prefix(Constructable __instance)
        {
            if (!__instance._constructed && __instance.constructedAmount < 1.0f)
            {
                NitroxServiceLocator.LocateService<Building>().ChangeConstructionAmount(__instance.gameObject, __instance.constructedAmount);
            }
            
            // If we are constructing a base piece then we'll want to store all of the BaseGhost information
            // as it will not be available when the construction hits 100%
            BaseGhost baseGhost = __instance.gameObject.GetComponentInChildren<BaseGhost>();

            if (baseGhost != null && baseGhost.TargetBase)
            {
                lastTargetBase = baseGhost.TargetBase.GetComponent<Base>();
                lastTargetBaseOffset = baseGhost.TargetOffset;
            }
            else
            {
                lastTargetBase = null;
                lastTargetBaseOffset = default(Int3);
            }

            return true;
        }

        public static void Postfix(Constructable __instance, bool __result)
        {
            if (__result && __instance.constructedAmount >= 1.0f)
            {
                NitroxServiceLocator.LocateService<Building>().ConstructionComplete(__instance.gameObject, Optional.OfNullable(lastTargetBase), lastTargetBaseOffset);
            }
        }

        public override void Patch(HarmonyInstance harmony)
        {
            PatchMultiple(harmony, TARGET_METHOD, true, true, false);
        }*/
    }

    public class Constructable_NotifyConstructedChanged_Patch : NitroxPatch, IDynamicPatch
    {
        public static readonly Type TARGET_CLASS = typeof(Constructable);
        public static readonly MethodInfo TARGET_METHOD = TARGET_CLASS.GetMethod("NotifyConstructedChanged", BindingFlags.NonPublic | BindingFlags.Instance);

        public static bool Prefix(Constructable __instance, bool constructed)
        {
            //return NitroxServiceLocator.LocateService<Building>().Constructable_NotifyConstructedChanged_Pre(__instance, constructed);
            return true;
        }

        public static void Postfix(Constructable __instance)
        {
            NitroxServiceLocator.LocateService<Building>().Constructable_NotifyConstructedChanged_Post(__instance);
        }

        public override void Patch(HarmonyInstance harmony)
        {
            PatchMultiple(harmony, TARGET_METHOD, true, true, false);
        }
    }
}
