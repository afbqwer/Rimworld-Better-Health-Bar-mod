using HarmonyLib;
using Verse;

namespace ASQHPBar;

/// <summary>
/// 修补 GenMapUI.GetPawnLabelNameWidth 的最小宽度，
/// 使原版 DrawPawnLabel 也使用 mod 的 healthBarWidth 作为最小宽度，
/// 确保原版标签宽度与 mod 健康条背景宽度同步。
/// 殖民者栏内另按专属上限收窄，避免标签压到相邻头像。
/// </summary>
[HarmonyPatch(typeof(GenMapUI))]
[HarmonyPatch("GetPawnLabelNameWidth")]
[HarmonyPriority(Priority.High)]
public static class Patch_GenMapUI_GetPawnLabelNameWidth
{
    public static void Postfix(ref float __result, Pawn pawn)
    {
        if (!SimpleHealthBarSettings.enableImprovedHealthBar)
            return;

        if (!SimpleHealthBarSettings.noNamePositionAbove
            && pawn.health.summaryHealth.SummaryHealthPercent < 0.999f)
        {
            float minWidth = SimpleHealthBarSettings.healthBarMinWidth;
            if (__result < minWidth)
                __result = minWidth;
        }

        // 殖民者栏专属宽度上限（0 = 不限制，随殖民者栏缩放）：此处返回的是名称宽度，
        // 标签背景还要左右各加 padding，故上限需扣掉这部分
        float cap = SimpleHealthBarSettings.ColonistBarWidthCap;
        if (cap > 0)
        {
            float padding = Prefs.DisableTinyText ? 6f : 4f;
            float maxNameWidth = cap - padding * 2f;
            if (maxNameWidth < 0f)
                maxNameWidth = 0f;
            if (__result > maxNameWidth)
                __result = maxNameWidth;
        }
    }
}
