using HarmonyLib;
using Verse;

namespace ASQHPBar;

/// <summary>
/// 修补 GenMapUI.GetPawnLabelNameWidth 的最小宽度，
/// 使原版 DrawPawnLabel 也使用 mod 的 healthBarWidth 作为最小宽度，
/// 确保原版标签宽度与 mod 健康条背景宽度同步。
/// </summary>
[HarmonyPatch(typeof(GenMapUI))]
[HarmonyPatch("GetPawnLabelNameWidth")]
[HarmonyPriority(Priority.High)]
public static class Patch_GenMapUI_GetPawnLabelNameWidth
{
    public static void Postfix(ref float __result, Pawn pawn)
    {
        if (!SimpleHealthBarSettings.enableImprovedHealthBar || SimpleHealthBarSettings.noNamePositionAbove)
            return;

        if (pawn.health.summaryHealth.SummaryHealthPercent < 0.999f)
        {
            float minWidth = SimpleHealthBarSettings.healthBarMinWidth;
            if (__result < minWidth)
                __result = minWidth;
        }
    }
}