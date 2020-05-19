using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using NitroxClient.GameLogic;
using NitroxClient.GameLogic.Helper;
using NitroxModel.Core;
using NitroxModel.Helper;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic
{
    public class BaseDeconstructable_Deconstruct_Patch : NitroxPatch, IDynamicPatch
    {
        public static readonly Type TARGET_CLASS = typeof(BaseDeconstructable);

        public static readonly MethodInfo TARGET_METHOD = TARGET_CLASS.GetMethod("Deconstruct", BindingFlags.Public | BindingFlags.Instance);

        public static void Prefix(BaseDeconstructable __instance)
        {
            NitroxServiceLocator.LocateService<Building>().BaseDeconstructable_Deconstruct_Pre(__instance);
        }

        public override void Patch(HarmonyInstance harmony)
        {
            PatchPrefix(harmony, TARGET_METHOD);
        }


        /*public static readonly MethodInfo TARGET_METHOD = TARGET_CLASS.GetMethod("Deconstruct");

        public static readonly OpCode INJECTION_OPCODE = OpCodes.Callvirt;
        public static readonly object INJECTION_OPERAND = typeof(Constructable).GetMethod("SetState");

        public static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> instructions)
        {
            Validate.NotNull(INJECTION_OPERAND);

            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction;

                if (instruction.opcode.Equals(INJECTION_OPCODE) && instruction.operand.Equals(INJECTION_OPERAND))
                {
                    
                     BaseDeconstructable_Deconstruct_Patch.Callback(gameObject);
                    
                    yield return original.Ldloc<ConstructableBase>(0);
                    yield return new CodeInstruction(OpCodes.Call, typeof(BaseDeconstructable_Deconstruct_Patch).GetMethod("Callback", BindingFlags.Static | BindingFlags.Public));
                }
            }
        }

        public static void Callback(GameObject gameObject)
        {
            TransientLocalObjectManager.Add(TransientLocalObjectManager.TransientObjectType.LATEST_DECONSTRUCTED_BASE_PIECE, gameObject);
        }

        public override void Patch(HarmonyInstance harmony)
        {
            PatchTranspiler(harmony, TARGET_METHOD);
        }*/
    }

    public class BaseDeconstructable_MakeFaceDeconstructable_Patch : NitroxPatch, IDynamicPatch
    {
        public static readonly Type TARGET_CLASS = typeof(BaseDeconstructable);
        public static readonly MethodInfo TARGET_METHOD = TARGET_CLASS.GetMethod("MakeFaceDeconstructable");

        public static void Postfix(Transform geometry)
        {
            NitroxServiceLocator.LocateService<Building>().BaseDeconstructable_MakeFaceDeconstructable_Post(geometry.gameObject);
        }

        public override void Patch(HarmonyInstance harmony)
        {
            PatchPostfix(harmony, TARGET_METHOD);
        }
    }

    public class BaseDeconstructable_MakeCellDeconstructable_Patch : NitroxPatch, IDynamicPatch
    {
        public static readonly Type TARGET_CLASS = typeof(BaseDeconstructable);
        public static readonly MethodInfo TARGET_METHOD = TARGET_CLASS.GetMethod("MakeCellDeconstructable");

        public static void Postfix(Transform geometry)
        {
            NitroxServiceLocator.LocateService<Building>().BaseDeconstructable_MakeCellDeconstructable_Post(geometry.gameObject);
        }

        public override void Patch(HarmonyInstance harmony)
        {
            PatchPostfix(harmony, TARGET_METHOD);
        }
    }
}

