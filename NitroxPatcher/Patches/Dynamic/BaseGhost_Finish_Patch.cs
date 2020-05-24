﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using NitroxClient.GameLogic;
using NitroxClient.GameLogic.Bases;
using NitroxClient.GameLogic.Helper;
using NitroxModel.Core;
using NitroxModel.Helper;
using UnityEngine;
using static NitroxClient.GameLogic.Helper.TransientLocalObjectManager;

namespace NitroxPatcher.Patches.Dynamic
{
    public class BaseGhost_Finish_Patch : NitroxPatch, IDynamicPatch
    {
        public static readonly Type TARGET_CLASS = typeof(BaseGhost);
        public static readonly MethodInfo TARGET_METHOD = TARGET_CLASS.GetMethod("Finish", BindingFlags.Public | BindingFlags.Instance);

        public static bool Prefix(BaseGhost __instance)
        {
            NitroxServiceLocator.LocateService<GeometryLayoutChangeHandler>().BaseGhost_Finish_Pre(__instance);
            return true;
        }

        public static void Postfix(BaseGhost __instance)
        {
            NitroxServiceLocator.LocateService<GeometryLayoutChangeHandler>().BaseGhost_Finish_Post(__instance);
        }

        public override void Patch(HarmonyInstance harmony)
        {
            PatchMultiple(harmony, TARGET_METHOD, true, true, false);
        }

        /*
        public static readonly Type TARGET_CLASS = typeof(BaseGhost);
        public static readonly MethodInfo TARGET_METHOD = TARGET_CLASS.GetMethod("Finish", BindingFlags.Public | BindingFlags.Instance);

        public static readonly OpCode INJECTION_OPCODE = OpCodes.Stfld;
        public static readonly object INJECTION_OPERAND = TARGET_CLASS.GetField("targetBase", BindingFlags.NonPublic | BindingFlags.Instance);

        public static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> instructions)
        {
            Validate.NotNull(INJECTION_OPERAND);

            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction;

                if (instruction.opcode.Equals(INJECTION_OPCODE))
                {
                    
                      TransientLocalObjectManager.Add(TransientLocalObjectManager.TransientObjectType.BASE_GHOST_NEWLY_CONSTRUCTED_BASE_GAMEOBJECT, gameObject);
                     
                    yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                    yield return original.Ldloc<GameObject>(1);
                    yield return new CodeInstruction(OpCodes.Call, typeof(TransientLocalObjectManager).GetMethod("Add", BindingFlags.Static | BindingFlags.Public, null, new Type[] { TransientObjectType.BASE_GHOST_NEWLY_CONSTRUCTED_BASE_GAMEOBJECT.GetType(), typeof(object) }, null));
                }
            }
        }

        public override void Patch(HarmonyInstance harmony)
        {
            PatchTranspiler(harmony, TARGET_METHOD);
        }
         */
    }

}

