using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using static HarmonyLib.AccessTools;
using static PLBurrowArena;
using static PulsarModLoader.Patches.HarmonyHelpers;

namespace ExpandedGalaxy
{
    /// <summary>
    /// Changes to add Robot Battery Life
    /// </summary>
    /* - Add component (Flat circle shield generator) that robot respawns on / charges on.
     * - Increase Charge Time
     * - Talent to increase battery life
     * - Increase power draw with sprinting or stamina regen
    */
    class RobotBattery
    {
        static int MAX_STEPS_GROUND = 150;
        static int StepsTaken = 0;

        static readonly Color BATTERY_COLOR = new Color(0.902f, 0.360f, 0.140f, 1);
        internal static readonly Color DEFAULT_COLOR = new Color(0.4886f, 0.2261f, 0.6029f, 1);

        static int FootstepsSinceLastReset => PLGameProgressManager.Instance.FootstepsTaken - StepsTaken;

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
                if (__instance.ABLabel.text != "CL") return;
                __instance.ABLabel.color = DEFAULT_COLOR;
                __instance.ABLabel.text = "CL";
                __instance.ABFill.color = DEFAULT_COLOR;
            }
            public static void Patch(PLInGameUI __instance)
            {
                if (PLNetworkManager.Instance.ViewedPawn.GetPlayer() != null && PLNetworkManager.Instance.ViewedPawn.MyController != null && PLNetworkManager.Instance.ViewedPawn.GetPlayer().RaceID == 2)
                {
                    PLGlobal.SafeGameObjectSetActive(__instance.ABLabel.gameObject, true);

                    float fillAmount;
                    fillAmount = 1f - Mathf.Clamp01((float)FootstepsSinceLastReset / MAX_STEPS_GROUND);
                    __instance.ABFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 260f * fillAmount);

                    // Maths doesnt like me so I did the below
                    if (FootstepsSinceLastReset > MAX_STEPS_GROUND + 1)
                        StepsTaken = PLGameProgressManager.Instance.FootstepsTaken - (MAX_STEPS_GROUND + 1);

                    if (StepsTaken > PLGameProgressManager.Instance.FootstepsTaken)
                        StepsTaken = PLGameProgressManager.Instance.FootstepsTaken;

                    if (__instance.ABLabel.text == "BAT") return;
                    __instance.ABLabel.color = BATTERY_COLOR;
                    __instance.ABLabel.text = "BAT";
                    __instance.ABFill.color = BATTERY_COLOR;
                    StepsTaken = PLGameProgressManager.Instance.FootstepsTaken;
                }
                else
                {
                    PLGlobal.SafeGameObjectSetActive(__instance.ABLabel.gameObject, false);
                    if (__instance.ABLabel.text != "CL") return;
                    __instance.ABLabel.color = DEFAULT_COLOR;
                    __instance.ABLabel.text = "CL";
                    __instance.ABFill.color = DEFAULT_COLOR;
                }
            }
        }

        [HarmonyPatch(typeof(PLLifeSupportSystem), "Update")]
        class RefillBatteryLifeSupport
        {
            public static void Postfix(PLLifeSupportSystem __instance)
            {
                if (__instance.MyShipInfo?.MyTLI == PLNetworkManager.Instance?.LocalPlayer?.MyCurrentTLI &&
                PLNetworkManager.Instance.LocalPlayer.GetPawn() != null && __instance.MyInstance != null)
                {
                    float sqrDist = (PLNetworkManager.Instance.LocalPlayer.GetPawn().transform.position - __instance.MyInstance.transform.position).sqrMagnitude;
                    if (sqrDist < __instance.Range && !__instance.OnFire && FootstepsSinceLastReset <= MAX_STEPS_GROUND + 1)
                    {
                        StepsTaken += Mathf.RoundToInt(10 * __instance.GetHealthRatio());
                    }
                }
            }
        }

        [HarmonyPatch(typeof(PLController), "IsEncumbered")]
        class BatteryDrained
        {
            public static bool Prefix(PLController __instance, ref bool __result)
            {
                if (__instance.MyPawn?.GetPlayer() == PLNetworkManager.Instance?.LocalPlayer &&
                FootstepsSinceLastReset > MAX_STEPS_GROUND)
                {
                    __result = true;
                    return false;
                }
                return true;
            }
        }
    }
}
