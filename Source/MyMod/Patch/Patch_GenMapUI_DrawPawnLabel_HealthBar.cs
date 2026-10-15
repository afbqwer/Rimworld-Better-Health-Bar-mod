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

        // 名称标签宽度：未下移时血条背景即名称标签背景，宽度是否超限以此为准
        float labelWidth = bgRect.width;

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

        // 宽度限制在名称标签宽度内（殖民者栏始终限制，其它位置由开关决定）
        if (ShouldClampToLabelWidth())
        {
            newWidth = Mathf.Min(newWidth, labelWidth);
        }

        // 殖民者栏专属宽度上限：最终裁决，保证该处标签/血条不压到相邻头像
        float colonistCap = SimpleHealthBarSettings.ColonistBarWidthCap;
        if (colonistCap > 0 && newWidth > colonistCap)
        {
            newWidth = colonistCap;
        }

        bgRect = new Rect(bgRect.x - (newWidth - bgRect.width) / 2f, bgRect.y, newWidth, bgRect.height);

        HealthBarEaseHelper.DrawPawnHealthBarOnGUI(pawn, bgRect, true, textWidth);
    }

    /// <summary>
    /// 是否把血条宽度限制在名称标签宽度内。仅在血条绘制于名称标签位置（未下移）时才有意义：
    /// 殖民者栏（左上角头像区）内始终限制，其它位置由设置 allowBarWiderThanLabel 决定；
    /// 下移绘制时血条是标签下方的独立条，宽度不受名称标签约束。
    /// </summary>
    private static bool ShouldClampToLabelWidth()
    {
        if (SimpleHealthBarSettings.enableHealthBarOffset)
            return false;
        return HealthBarEaseHelper.InColonistBarDraw || !SimpleHealthBarSettings.allowBarWiderThanLabel;
    }
}
