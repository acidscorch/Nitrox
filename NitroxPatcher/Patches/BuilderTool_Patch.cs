﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using NitroxClient.GameLogic;
using NitroxModel.Core;
using NitroxModel.Helper;
using UnityEngine;

namespace NitroxPatcher.Patches
{
    public class BuilderTool_OnHover_Patch : NitroxPatch
    {
        public static readonly Type TARGET_CLASS = typeof(BuilderTool);
        public static readonly MethodInfo TARGET_METHOD = TARGET_CLASS.GetMethod("OnHover", BindingFlags.NonPublic | BindingFlags.Instance,
                                                                                 null, new[] { typeof(Constructable) }, null);

        public static bool Prefix(BuilderTool __instance, bool __result, Constructable constructable)
        {
            bool _result = true;

            /*_result = NitroxServiceLocator.LocateService<Building>().BuilderTool_Pre_OnHover(__instance.gameObject, constructable);
            if (_result == false)
            {
                return _result; //skip further checks and exit here
            }*/
            
            //Add more checks here later for other Objects


            return _result;
        }

        public static void Postfix(BuilderTool __instance, Constructable constructable)
        {

            NitroxServiceLocator.LocateService<Building>().BuilderTool_Post_OnHover(__instance.gameObject, constructable);

        }

        public override void Patch(HarmonyInstance harmony)
        {
            PatchPostfix(harmony, TARGET_METHOD);
        }
    }


    public class BuilderTool_HandleInput_Patch : NitroxPatch
    {
        public static readonly Type TARGET_CLASS = typeof(BuilderTool);
        public static readonly MethodInfo TARGET_METHOD = TARGET_CLASS.GetMethod("HandleInput", BindingFlags.NonPublic | BindingFlags.Instance);

        public static readonly OpCode INJECTION_OPCODE = OpCodes.Callvirt;
        public static readonly object INJECTION_OPERAND = typeof(Constructable).GetMethod("SetState", BindingFlags.Public | BindingFlags.Instance);

        public static bool Prefix(BuilderTool __instance)
        {
            bool _result = true;

            _result = NitroxServiceLocator.LocateService<Building>().BuilderTool_Pre_HandleInput(__instance.gameObject);
            if (_result == false)
            {
                return _result; //skip further checks and exit here
            }

            //Add more checks here later for other Objects

            return _result;
        }

        public static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> instructions)
        {
            Validate.NotNull(INJECTION_OPERAND);

            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction;
                if (instruction.opcode.Equals(INJECTION_OPCODE) && instruction.operand.Equals(INJECTION_OPERAND))
                {
                    /*
                     * Multiplayer.Logic.Building.DeconstructionBegin(constructable.gameObject);
                     */
                    yield return TranspilerHelper.LocateService<Building>();
                    yield return original.Ldloc<Constructable>();
                    yield return new CodeInstruction(OpCodes.Callvirt, typeof(Component).GetMethod("get_gameObject", BindingFlags.Instance | BindingFlags.Public));
                    yield return new CodeInstruction(OpCodes.Callvirt, typeof(Building).GetMethod("DeconstructionBegin", BindingFlags.Public | BindingFlags.Instance));
                }
            }
        }

        public override void Patch(HarmonyInstance harmony)
        {
            PatchMultiple(harmony, TARGET_METHOD,true,false,true);
        }
    }
}