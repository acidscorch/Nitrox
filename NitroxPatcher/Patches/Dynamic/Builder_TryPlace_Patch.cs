﻿using System;
using System.Reflection;
using Harmony;
using NitroxModel.Core;
using NitroxPatcher.PatchLogic.Bases;

namespace NitroxPatcher.Patches.Dynamic
{
    public class Builder_TryPlace_Patch : NitroxPatch, IDynamicPatch
    {
        public static readonly Type TARGET_CLASS = typeof(Builder);
        public static readonly MethodInfo TARGET_METHOD = TARGET_CLASS.GetMethod("TryPlace", BindingFlags.Public | BindingFlags.Static);

        public static bool Prefix(ref bool __result)
        {
            return NitroxServiceLocator.LocateService<Building>().Builder_TryPlace_Pre(ref __result);
        }

        public override void Patch(HarmonyInstance harmony)
        {
            PatchPrefix(harmony, TARGET_METHOD);
        }
    }
}
