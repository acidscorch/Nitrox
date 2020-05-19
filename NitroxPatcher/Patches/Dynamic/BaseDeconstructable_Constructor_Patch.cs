using System;
using System.Reflection;
using Harmony;
using NitroxClient.GameLogic;
using NitroxClient.MonoBehaviours;
using NitroxModel.Core;
using NitroxModel.DataStructures;
using NitroxModel.Logger;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic
{
    public class BaseDeconstructable_Constructor_Patch : NitroxPatch, IDynamicPatch
    {
        public static readonly Type TARGET_CLASS = typeof(Base);
        public static readonly ConstructorInfo TARGET_METHOD = TARGET_CLASS.GetConstructor(new Type[] { });

        public static void Postfix(BaseDeconstructable __instance)
        {
            //NitroxServiceLocator.LocateService<Building>().BaseDeconstructable_Constructor_Post(__instance);
        }

        public override void Patch(HarmonyInstance harmony)
        {
            PatchPostfix(harmony, TARGET_METHOD);
        }
    }
}

