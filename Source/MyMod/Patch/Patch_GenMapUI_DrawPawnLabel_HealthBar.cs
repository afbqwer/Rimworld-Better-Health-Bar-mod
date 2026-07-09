using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace ASQHPBar;

/// <summary>
/// 在 GenMapUI.DrawPawnLabel 中通过 Prefix 绘制改进的健康条。
/// 原版 DrawPawnLabel 中的健康条和背景已被 Patch_GenMapUI_DrawPawnLabel (Transpiler) 移除。
/// </summary>
[HarmonyPatch(typeof(GenMapUI), nameof(GenMapUI.DrawPawnLabel),
    [typeof(Pawn), typeof(Rect), typeof(float), typeof(float),
     typeof(Dictionary<string, string>), typeof(GameFont), typeof(bool), typeof(bool)])]
[HarmonyPriority(Priority.High)]
public static class Patch_GenMapUI_DrawPawnLabel_HealthBar
{
    public static void Prefix(Pawn pawn, Rect bgRect)
    {
        if (!SimpleHealthBarSettings.enableImprovedHealthBar || SimpleHealthBarSettings.enableCompatibilityMode)
            return;
        //if (pawn == null || !pawn.Spawned)
        //    return;

        if (HealthBarEaseHelper.IsOutsideViewport(bgRect.center))
            return;

        // 计算标签文本宽度
        GameFont prevFont = Text.Font;
        Text.Font = GameFont.Tiny;
        string label = pawn.LabelShortCap;
        float textWidth = Text.CalcSize(label).x;
        float minWidth = SimpleHealthBarSettings.healthBarMinWidth;
        Text.Font = prevFont;
        textWidth = Mathf.Max(minWidth,textWidth);

        // 根据生命系数 HealthScale 增加血条宽度
        float healthScaleExtra = SimpleHealthBarSettings.healthScaleWidthMultiplier <= 0f ? 0f : HealthBarEaseHelper.GetHealthScaleSafe(pawn) * SimpleHealthBarSettings.healthScaleWidthMultiplier;

        // 最大宽度上限无视名称宽度，钳制最终宽度
        float newWidth = bgRect.width + healthScaleExtra;
        if (newWidth < minWidth)
        {
            newWidth = minWidth;
        }
        int maxBarWidth = SimpleHealthBarSettings.EffectiveMaxBarWidth;
        if (maxBarWidth > 0 && newWidth > maxBarWidth)
        {
            newWidth = maxBarWidth;
        }
        bgRect = new Rect(bgRect.x - (newWidth - bgRect.width) / 2f, bgRect.y, newWidth, bgRect.height);

        HealthBarEaseHelper.DrawPawnHealthBarOnGUI(pawn, bgRect, true, textWidth);
    }
}
