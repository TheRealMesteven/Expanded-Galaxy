using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using static HarmonyLib.AccessTools;
using static PulsarModLoader.Patches.HarmonyHelpers;

namespace ExpandedGalaxy
{
    [HarmonyPatch(typeof(PLInGameUI), "Update")]
    class RobotBatteryDisplay
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> target = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldfld, Field(typeof(PLInGameUI), "ABLabel")),
                new CodeInstruction(OpCodes.Callvirt, Method(typeof(UnityEngine.Component), "get_gameObject")),
                new CodeInstruction(OpCodes.Ldc_I4_0),
                new CodeInstruction(OpCodes.Call, Method(typeof(PLGlobal), "SafeGameObjectSetActive", new System.Type[] { typeof(UnityEngine.GameObject), typeof(bool) })),
            }; // target: PLGlobal.SafeGameObjectSetActive(this.ABLabel.gameObject, false);

            int find = FindSequence(instructions, target) - 6;
            List<CodeInstruction> codeInstructions = instructions.ToList();
            codeInstructions.Insert(find, new CodeInstruction(OpCodes.Call, Method(typeof(RobotBatteryDisplay), "ResetPatch")));
            codeInstructions.Insert(find, new CodeInstruction(OpCodes.Ldarg_0));
            // target: br_s becomes: ExpandedGalaxy.RobotBatteryDisplay.ResetPatch(this) -> br_s

            List<CodeInstruction> patch = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Call, Method(typeof(RobotBatteryDisplay), "Patch"))
            }; // result: ExpandedGalaxy.RobotBatteryDisplay.Patch(this);

            return PatchBySequence(instructions, target, patch, PatchMode.REPLACE);
        }
        public static void ResetPatch(PLInGameUI __instance)
        {
            if (__instance.ABLabel.text == "CL") return;
            __instance.ABLabel.color = new Color(0.4886f, 0.2261f, 0.6029f, 1);
            __instance.ABLabel.text = "CL";
            __instance.ABFill.color = new Color(0.4886f, 0.2261f, 0.6029f, 1);
        }
        public static void Patch(PLInGameUI __instance)
        {
            if (PLNetworkManager.Instance.ViewedPawn.GetPlayer() != null && PLNetworkManager.Instance.ViewedPawn.MyController != null && PLNetworkManager.Instance.ViewedPawn.GetPlayer().RaceID == 2)
            {
                PLGlobal.SafeGameObjectSetActive(__instance.ABLabel.gameObject, true);
                /* Change battery level code here
                if (PLNetworkManager.Instance.ViewedPawn.Cloaked)
                {
                    float num31 = 1f - Mathf.Clamp01((Time.time - PLNetworkManager.Instance.ViewedPawn.MyController.LastCloakedActivatedTime) / 15f);
                    __instance.ABFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 260f * num31);
                }
                else
                {
                    float num32 = Mathf.Clamp01((Time.time - PLNetworkManager.Instance.ViewedPawn.MyController.LastCloakedActivatedTime) / 120f);
                    __instance.ABFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 260f * num32);
                }*/
                if (__instance.ABLabel.text == "BAT") return;
                __instance.ABLabel.color = new Color(0.902f, 0.360f, 0.140f, 1);
                __instance.ABLabel.text = "BAT";
                __instance.ABFill.color = new Color(0.902f, 0.360f, 0.140f, 1);
            }
            else
            {                
                PLGlobal.SafeGameObjectSetActive(__instance.ABLabel.gameObject, false);
            }
        }
    }
}
