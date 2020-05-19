using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using NitroxClient.GameLogic;
using NitroxModel.Core;
using Harmony;

namespace NitroxPatcher.Patches.Dynamic
{
    public class BaseRoot_Patch : NitroxPatch, IDynamicPatch
    {
        public static readonly Type TARGET_CLASS = typeof(BaseRoot);
        public static readonly ConstructorInfo TARGET_METHOD = TARGET_CLASS.GetConstructor(new Type[] { });

        public static void Postfix(BaseRoot __instance)
        {
            NitroxServiceLocator.LocateService<Building>().BaseRoot_Constructor_Post(__instance);
        }

        public override void Patch(HarmonyInstance harmony)
        {
            PatchPostfix(harmony, TARGET_METHOD);
        }
    }
}
