using HarmonyLib;
using PulsarModLoader.Content.Components.MissionShipComponent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using static HarmonyLib.AccessTools;
using static PulsarModLoader.Patches.HarmonyHelpers;

namespace ExpandedGalaxy
{
    /// <summary>
    /// Changes to add Robot Battery Life
    /// </summary>
    /* - Add component (Flat circle shield generator) that robot respawns on / charges on.
     * - Talent to increase battery life
     * - Increase power draw with sprinting or stamina regen
     * - Toggle on and off battery usage
     * - ? Can turn self into Aux Boost? (Get stunned in place)
     * - ? Recharging has power draw?
     * - Consume old battery (biscuit eating sound)
     * - ? Overclock talent - Can run x faster at increased battery consumption
     * - ? No place like home talent - Recharge anywhere on ship
     * - Cant heal with atrium or healing beam rifle (? Repair gun will need to take Ammo, 200 like healing beam)
    */
    class RobotBattery
    {
        static int MAX_STEPS_GROUND = 150;
        static float StepsTaken = 0;

        static readonly Color BATTERY_COLOR = new Color(0.902f, 0.360f, 0.140f, 1);
        internal static readonly Color DEFAULT_COLOR = new Color(0.4886f, 0.2261f, 0.6029f, 1);

        static float FootstepsSinceLastReset => PLGameProgressManager.Instance.FootstepsTaken - StepsTaken;

        internal static Material ChargerMaterial = null;

        #region Robot-Battery

        [HarmonyPatch(typeof(PLInGameUI), "Update")]
        class RobotBatteryDisplay
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> target = new List<CodeInstruction>()
                {
                    new CodeInstruction(OpCodes.Ldfld, Field(typeof(PLInGameUI), "ABLabel")),
                    new CodeInstruction(OpCodes.Callvirt, Method(typeof(UnityEngine.Component), "get_gameObject")),
                    new CodeInstruction(OpCodes.Ldc_I4_1),
                    new CodeInstruction(OpCodes.Call, Method(typeof(PLGlobal), "SafeGameObjectSetActive", new System.Type[] { typeof(UnityEngine.GameObject), typeof(bool) })),
                }; // target: PLGlobal.SafeGameObjectSetActive(this.ABLabel.gameObject, true);

                List<CodeInstruction> patch = new List<CodeInstruction>()
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Call, Method(typeof(RobotBatteryDisplay), "ResetPatch")),
                }; // result: ExpandedGalaxy.RobotBatteryDisplay.ResetPatch(this);

                instructions = PatchBySequence(instructions, target, patch, PatchMode.AFTER);

                target = new List<CodeInstruction>()
                {
                    new CodeInstruction(OpCodes.Ldfld, Field(typeof(PLInGameUI), "ABLabel")),
                    new CodeInstruction(OpCodes.Callvirt, Method(typeof(UnityEngine.Component), "get_gameObject")),
                    new CodeInstruction(OpCodes.Ldc_I4_0),
                    new CodeInstruction(OpCodes.Call, Method(typeof(PLGlobal), "SafeGameObjectSetActive", new System.Type[] { typeof(UnityEngine.GameObject), typeof(bool) })),
                }; // target: PLGlobal.SafeGameObjectSetActive(this.ABLabel.gameObject, false);

                patch = new List<CodeInstruction>()
                {
                    new CodeInstruction(OpCodes.Call, Method(typeof(RobotBatteryDisplay), "Patch"))
                }; // result: ExpandedGalaxy.RobotBatteryDisplay.Patch(this);

                return PatchBySequence(instructions, target, patch, PatchMode.REPLACE);
            }
            public static void ResetPatch(PLInGameUI __instance)
            {
                if (__instance.ABLabel.text == "CL") return;
                __instance.ABLabel.text = "CL";
                __instance.ABLabel.color = DEFAULT_COLOR;
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
                    __instance.ABLabel.text = "BAT";
                    __instance.ABLabel.color = BATTERY_COLOR;
                    __instance.ABFill.color = BATTERY_COLOR;
                    StepsTaken = PLGameProgressManager.Instance.FootstepsTaken;
                }
                else
                {
                    PLGlobal.SafeGameObjectSetActive(__instance.ABLabel.gameObject, false);
                    if (__instance.ABLabel.text == "CL") return;
                    __instance.ABLabel.text = "CL";
                    __instance.ABLabel.color = DEFAULT_COLOR;
                    __instance.ABFill.color = DEFAULT_COLOR;
                }
            }
        }

        [HarmonyPatch(typeof(PLController), "IsEncumbered")]
        class BatteryDrained
        {
            public static bool Prefix(PLController __instance, ref bool __result)
            {
                if (__instance.MyPawn?.GetPlayer() == PLNetworkManager.Instance?.LocalPlayer &&
                    PLNetworkManager.Instance?.ViewedPawn?.GetPlayer()?.RaceID == 2 && (FootstepsSinceLastReset > MAX_STEPS_GROUND || StandingOnCharger))
                {
                    __result = true;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(PLGameStatic), "Update")]
        class RobotEncumberedDisplay
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> target = new List<CodeInstruction>()
                {
                    new CodeInstruction(OpCodes.Ldsfld),
                    new CodeInstruction(OpCodes.Ldstr, ""),
                    new CodeInstruction(OpCodes.Ldstr, "<color=red>"),
                    new CodeInstruction(OpCodes.Ldstr, "You are encumbered"),
                    new CodeInstruction(OpCodes.Ldc_I4_0),
                    new CodeInstruction(OpCodes.Call),
                    new CodeInstruction(OpCodes.Ldstr, "</color>"),
                    new CodeInstruction(OpCodes.Call),
                    new CodeInstruction(OpCodes.Ldstr, ""),
                    new CodeInstruction(OpCodes.Ldstr, ""),
                    new CodeInstruction(OpCodes.Callvirt),
                }; // target: PLGlobal.Instance.SetBottomInfo("", "<color=red>" + PLLocalize.Localize("You are encumbered", false) + "</color>", "", "");

                List<CodeInstruction> patch = new List<CodeInstruction>()
                {
                    new CodeInstruction(OpCodes.Call, Method(typeof(RobotEncumberedDisplay), "Patch")),
                }; // result: ExpandedGalaxy.RobotEncumberedDisplay.Patch();

                return PatchBySequence(instructions, target, patch, PatchMode.REPLACE, CheckMode.NONNULL);
            }
            public static void Patch()
            {
                if (PLNetworkManager.Instance?.ViewedPawn?.GetPlayer()?.RaceID == 2)
                {
                    if (FootstepsSinceLastReset > MAX_STEPS_GROUND)
                    {
                        PLGlobal.Instance.SetBottomInfo("", "<color=red>" + PLLocalize.Localize("Battery Depleted", false) + "</color>", "", "");
                        return;
                    }
                }
                PLGlobal.Instance.SetBottomInfo("", "<color=red>" + PLLocalize.Localize("You are encumbered", false) + "</color>", "", "");
            }
        }
        #endregion
        #region Robot-Charger

        static bool StandingOnCharger = false;

        [HarmonyPatch(typeof(PLLifeSupportSystem), "Update")]
        class RefillBatteryLifeSupport
        {
            static float ChargeAccumulator = 0f; // For a value like `1601438` `0.01` is too small
            static float LastCheckedTime = 0f;
            public static void Postfix(PLLifeSupportSystem __instance)
            {
                if (__instance.MyShipInfo?.MyTLI == PLNetworkManager.Instance?.LocalPlayer?.MyCurrentTLI &&
                PLNetworkManager.Instance.LocalPlayer.GetPawn() != null && __instance.MyInstance != null)
                {
                    // Raycast check if standing on charger method
                    if (PLNetworkManager.Instance.LocalPlayer.RaceID != 2 || __instance.OnFire || !(FootstepsSinceLastReset <= MAX_STEPS_GROUND + 1) || (Time.time - LastCheckedTime < 10f && !StandingOnCharger)) return;
                    LastCheckedTime = Time.time;

                    PLPawn Pawn = PLNetworkManager.Instance.LocalPlayer.GetPawn();
                    Ray ray = new Ray(Pawn.gameObject.transform.position, Vector3.down);
                    RaycastHit raycastHit;
                    if (Physics.Raycast(ray, out raycastHit, 10f, Pawn.GetCurrentRaycastCollisionMask()))
                    {
                        if (raycastHit.collider != null && raycastHit.collider.gameObject != null && raycastHit.collider.gameObject.name.Contains("Cargo") && !raycastHit.collider.gameObject.name.Contains("Base"))
                        {
                            if (int.TryParse(raycastHit.collider.gameObject.transform.parent.name.Split('_').Last(), out int padId))
                            {
                                CargoObjectDisplay detectedCargo = PLEncounterManager.Instance.PlayerShip.GetCargoComponentForID(padId - 1);
                                if (detectedCargo != null)
                                {
                                    ChargeAccumulator += 0.3f * __instance.GetHealthRatio();

                                    int gainedSteps = Mathf.FloorToInt(ChargeAccumulator);
                                    if (gainedSteps > 0)
                                    {
                                        StepsTaken += gainedSteps;
                                        ChargeAccumulator -= gainedSteps;
                                    }
                                    StandingOnCharger = true;
                                    return;
                                }
                            }
                        }
                    }
                    StandingOnCharger = false;

                    /* Old Method (Stand near life support)
                    float sqrDist = (PLNetworkManager.Instance.LocalPlayer.GetPawn().transform.position - __instance.MyInstance.transform.position).sqrMagnitude;
                    if (sqrDist < __instance.Range && !__instance.OnFire && FootstepsSinceLastReset <= MAX_STEPS_GROUND + 1)
                    {
                        ChargeAccumulator += 0.3f * __instance.GetHealthRatio();
                        if (ChargeAccumulator >= 1f)
                        {
                            StepsTaken += Mathf.FloorToInt(ChargeAccumulator);
                            ChargeAccumulator -= Mathf.Floor(ChargeAccumulator);
                        }
                    }
                    */
                }
            }
        }

        class RobotCharger : MissionShipComponentMod
        {
            public override string Name => "Robot Charger";
            public override string Description => "A device used to assemble and charge robots";
            public override int CargoVisualID => 40;
        }
        #endregion
    }
}
