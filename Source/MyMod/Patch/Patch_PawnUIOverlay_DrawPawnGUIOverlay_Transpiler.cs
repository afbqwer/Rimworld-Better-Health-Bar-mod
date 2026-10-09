using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ASQHPBar;

/// <summary>
/// Transpiler: 在 DrawPawnGUIOverlay 的早期 ret 处注入健康条绘制。
/// 
/// IL 中共有 6 处 ret：
///   1. IL_0036 — 未出生/迷雾中/过场动画 → 跳过（不应绘制任何内容）
///   2. IL_006d — 动物且不应显示名称
///   3. IL_00ba — 机械体且不应显示名称
///   4. IL_00bb — 非人形、非动物、非机械体
///   5. IL_00e0 — 变异体且 hideLabel
///   6. IL_015e — 正常返回（DrawPawnLabel 已被调用，Prefix 中已绘制血条）
/// 
/// 将第 2~5 处的 ret 替换为：加载 pawn → 调用 DrawHealthBarBeforeReturn → ret，
/// 同时将分支标签移至第一条新指令，确保控制流正确。
/// </summary>
[HarmonyPatch(typeof(PawnUIOverlay), nameof(PawnUIOverlay.DrawPawnGUIOverlay))]
[HarmonyPriority(Priority.High)]
public static class Patch_PawnUIOverlay_DrawPawnGUIOverlay_Transpiler
{
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var pawnField = AccessTools.Field(typeof(PawnUIOverlay), "pawn");
        var drawBarMethod = AccessTools.Method(
            typeof(Patch_PawnUIOverlay_DrawPawnGUIOverlay_Transpiler),
            nameof(DrawHealthBarBeforeReturn));

        if (pawnField == null)
        {
            Log.Error("[Simple Health Bar] Patch_PawnUIOverlay_DrawPawnGUIOverlay_Transpiler: 未找到 PawnUIOverlay.pawn 字段");
            return instructions;
        }
        if (drawBarMethod == null)
        {
            Log.Error("[Simple Health Bar] Patch_PawnUIOverlay_DrawPawnGUIOverlay_Transpiler: 未找到 DrawHealthBarBeforeReturn 方法");
            return instructions;
        }

        int retCount = 0;
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].opcode != OpCodes.Ret)
                continue;

            retCount++;

            // 跳过第 1 个（迷雾/过场）和第 6 个（正常返回）
            if (retCount == 1 || retCount == 6)
                continue;

            // 将原 ret 上的标签转移到第一条新指令，
            // 使原本跳转到此 ret 的分支先执行血条绘制
            var labels = codes[i].labels;
            codes[i].labels = new List<Label>();

            var newInstructions = new List<CodeInstruction>
            {
                new CodeInstruction(OpCodes.Ldarg_0) { labels = labels },
                new CodeInstruction(OpCodes.Ldfld, pawnField),
                new CodeInstruction(OpCodes.Call, drawBarMethod),
            };

            codes.InsertRange(i, newInstructions);
            i += newInstructions.Count; // 跳过新插入的指令，下次迭代处理原本的 ret
        }

        if (retCount != 6)
        {
            Log.Error($"[Simple Health Bar] Patch_PawnUIOverlay_DrawPawnGUIOverlay_Transpiler: 预期 6 个 ret，实际 {retCount} 个。可能游戏版本已变更，血条补丁可能未完全生效。");
        }

        return codes;
    }

    /// <summary>
    /// 在非人形/名称隐藏等提前返回前调用，为 pawn 绘制无标签血条。
    /// </summary>
    private static void DrawHealthBarBeforeReturn(Pawn pawn)
    {
        if (!SimpleHealthBarSettings.enableImprovedHealthBar || SimpleHealthBarSettings.enableCompatibilityMode)
            return;
        // if (pawn == null || !pawn.Spawned || Find.CameraDriver == null)
        //     return;

        Vector2 pos = HealthBarEaseHelper.GetScreenPosBelow(pawn);//GenMapUI.LabelDrawPosFor(pawn, -0.6f);
        if (HealthBarEaseHelper.IsOutsideViewport(pos))
            return;

        // 根据无标签健康条的最大显示距离过滤
        var zoom = Find.CameraDriver.CurrentZoom;

        if ((int)zoom > (int)SimpleHealthBarSettings.noNameMaxZoom)
            return;
        float height = SimpleHealthBarSettings.noNameHealthBarHeight;
        float width = SimpleHealthBarSettings.healthBarMinWidth;

        // 根据生命系数 HealthScale 增加血条宽度
        float healthScaleExtra = SimpleHealthBarSettings.healthScaleWidthMultiplier <= 0f ? 0f : HealthBarEaseHelper.GetHealthScaleSafe(pawn) * SimpleHealthBarSettings.healthScaleWidthMultiplier;
        if (healthScaleExtra > 0f)
            width += healthScaleExtra;

        int maxBarWidth = SimpleHealthBarSettings.EffectiveMaxBarWidth;
        if (maxBarWidth > 0 && width > maxBarWidth)
            width = maxBarWidth;

        float zoomScale = 1f;
        if (SimpleHealthBarSettings.enableZoomScale)
        {
            zoomScale = SimpleHealthBarSettings.GetZoomScale();
            width *= zoomScale;
            height = Mathf.Max(4f, height * zoomScale);
            if (width < 8f)
                return;
        }

        Rect bgRect = new Rect(pos.x - width / 2f, pos.y, width, height);

        HealthBarEaseHelper.DrawPawnHealthBarOnGUI(pawn, bgRect, false, 0, zoomScale);
    }
}
