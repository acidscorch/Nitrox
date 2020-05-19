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
    public class BuilderTool_OnHoverConstructable_Patch : NitroxPatch, IDynamicPatch
    {
        public static readonly Type TARGET_CLASS = typeof(BuilderTool);
        public static readonly MethodInfo TARGET_METHOD = TARGET_CLASS.GetMethod("OnHover", BindingFlags.NonPublic | BindingFlags.Instance,
                                                                                 null, new[] { typeof(Constructable) }, null);

        public static void Postfix(BuilderTool __instance, Constructable constructable)
        {
            
            NitroxServiceLocator.LocateService<Building>().BuilderTool_OnHoverConstructable_Post(__instance.gameObject, constructable);
        }

        public override void Patch(HarmonyInstance harmony)
        {
            PatchPostfix(harmony, TARGET_METHOD);
        }
    }

    public class BuilderTool_OnHoverDeconstructable_Patch : NitroxPatch, IDynamicPatch
    {
        public static readonly Type TARGET_CLASS = typeof(BuilderTool);
        public static readonly MethodInfo TARGET_METHOD = TARGET_CLASS.GetMethod("OnHover", BindingFlags.NonPublic | BindingFlags.Instance,
                                                                                 null, new[] { typeof(BaseDeconstructable) }, null);

        public static void Postfix(BuilderTool __instance, BaseDeconstructable deconstructable)
        {
            NitroxServiceLocator.LocateService<Building>().BuilderTool_OnHoverDeconstructable_Post(__instance.gameObject, deconstructable);
        }

        public override void Patch(HarmonyInstance harmony)
        {
            PatchPostfix(harmony, TARGET_METHOD);
        }
    }


    public class BuilderTool_HandleInput_Patch : NitroxPatch, IDynamicPatch
    {
        public static readonly Type TARGET_CLASS = typeof(BuilderTool);
        public static readonly MethodInfo TARGET_METHOD = TARGET_CLASS.GetMethod("HandleInput", BindingFlags.NonPublic | BindingFlags.Instance);

        public static readonly OpCode INJECTION_OPCODE = OpCodes.Callvirt;
        public static readonly object INJECTION_OPERAND = typeof(Constructable).GetMethod("SetState", BindingFlags.Public | BindingFlags.Instance);

        public static bool Prefix(BuilderTool __instance)
        {
            bool _result = true;

            _result = NitroxServiceLocator.LocateService<Building>().BuilderTool_HandleInput_Pre(__instance.gameObject);
            if (_result == false)
            {
                return _result; //skip further checks and exit here
            }

            //Add more checks here later for other Objects

            return _result;
        }

        public static void Postfix(BuilderTool __instance)
        {
            //Building must be first            
            NitroxServiceLocator.LocateService<Building>().BuilderTool_HandleInput_Post(__instance);
        }

        public override void Patch(HarmonyInstance harmony)
        {
            PatchMultiple(harmony, TARGET_METHOD, true, true, false);
        }
    }
}
