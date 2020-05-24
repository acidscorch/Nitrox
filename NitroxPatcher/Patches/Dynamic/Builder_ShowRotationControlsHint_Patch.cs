using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using NitroxClient.GameLogic;
using NitroxModel.Core;
using NitroxModel.Helper;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic
{
    public class Builder_ShowRotationControlsHint_Patch : NitroxPatch, IDynamicPatch
    {
        public static readonly Type TARGET_CLASS = typeof(Builder);
        public static readonly MethodInfo TARGET_METHOD = TARGET_CLASS.GetMethod("ShowRotationControlsHint", BindingFlags.Public | BindingFlags.Static);

        public static bool Prefix()
        {
            //test:
            NitroxModel.Logger.Log.Debug("ShowRotationControlsHint");
            return false;

            //return NitroxServiceLocator.LocateService<Building>().Builder_ShowRotationControlsHint_Pre();
        }

        public override void Patch(HarmonyInstance harmony)
        {
            PatchPrefix(harmony, TARGET_METHOD);
        }

    }
}
