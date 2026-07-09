using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace ASQHPBar;

/// <summary>
/// 合并 Transpiler：
/// 1. 在 FillableBar 绘制前插入运行时检查，改进开启时跳过原版健康条
/// 2. 在 DrawPawnLabel 背景绘制前插入运行时检查，改进开启时跳过背景
/// </summary>
[HarmonyPatch(typeof(GenMapUI), nameof(GenMapUI.DrawPawnLabel),
    [
        typeof(Pawn), typeof(Rect), typeof(float), typeof(float),
        typeof(Dictionary<string, string>), typeof(GameFont), typeof(bool), typeof(bool)
    ])]
public static class Patch_GenMapUI_DrawPawnLabel_Transpiler
{
    private static bool ShouldSkipBackground()
    {
        return SimpleHealthBarSettings.enableImprovedHealthBar && !SimpleHealthBarSettings.enableHealthBarOffset;
    }

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var fillableBarMethod = AccessTools.Method(typeof(Widgets), "FillableBar",
            new[] { typeof(Rect), typeof(float), typeof(Texture2D), typeof(Texture2D), typeof(bool) });
        var enableImprovedField = AccessTools.Field(typeof(SimpleHealthBarSettings),
            "enableImprovedHealthBar");
        var shouldSkipBgMethod = AccessTools.Method(typeof(Patch_GenMapUI_DrawPawnLabel_Transpiler),
            nameof(ShouldSkipBackground));
        var grayTextField = AccessTools.Field(typeof(TexUI), "GrayTextBG");

        int fillableBarIndex = -1;
        int bgLdsfldIndex = -1;
        int bgCallIndex = -1;

        // ── 第一遍：定位目标指令 ──
        for (int i = 0; i < codes.Count; i++)
        {
            // FillableBar 调用（在 IL_0080 附近）
            if (codes[i].Calls(fillableBarMethod))
            {
                fillableBarIndex = i;
            }

            // ldsfld TexUI.GrayTextBG（在 IL_0057）
            if (codes[i].LoadsField(grayTextField))
            {
                bgLdsfldIndex = i;
                for (int j = i; j < Math.Min(i + 4, codes.Count); j++)
                {
                    if (codes[j].opcode == OpCodes.Call &&
                        codes[j].operand is MethodInfo mi &&
                        mi.DeclaringType == typeof(GUI) &&
                        mi.Name == "DrawTexture")
                    {
                        bgCallIndex = j;
                        break;
                    }
                }
            }
        }

        // ── 第二遍：从高索引到低索引依次修改 ──

        // 1. FillableBar 跳过（高索引，不影响背景块）
        if (fillableBarIndex < 0)
        {
            Log.Error($"[Simple Health Bar] Transpiler failed: GenMapUI.DrawPawnLabel 中未找到 FillableBar 调用（共 {codes.Count} 条指令）");
        }
        else
        {
            // 回溯找到 FillableBar 块的起始（bge.un.s 之后第一条指令）
            int fillBarStart = fillableBarIndex;
            for (int j = fillableBarIndex; j >= Math.Max(0, fillableBarIndex - 10); j--)
            {
                if (codes[j].opcode == OpCodes.Bge_Un_S)
                {
                    fillBarStart = j + 1;
                    break;
                }
            }

            if (fillBarStart == fillableBarIndex)
            {
                Log.Error($"[Simple Health Bar] Transpiler failed: 在 FillableBar 调用（索引 {fillableBarIndex}）前 10 条内未找到 bge.un.s，附近指令：{string.Join(" | ", Enumerable.Range(Math.Max(0, fillableBarIndex - 8), 9).Select(k => $"[{k}]{codes[k].opcode}"))}");
            }
            else
            {
                // pop 之后的下一条指令的标签即跳转目标（IL_0086）
                var skipLabels = codes[fillableBarIndex + 2].labels;
                if (skipLabels.Count > 0)
                {
                    codes.Insert(fillBarStart, new CodeInstruction(OpCodes.Brtrue, skipLabels[0]));
                    codes.Insert(fillBarStart, new CodeInstruction(OpCodes.Ldsfld, enableImprovedField));
                }
                else
                {
                    Log.Error($"[Simple Health Bar] Transpiler failed: FillableBar 的 pop 后一条指令（索引 {fillableBarIndex + 2}，opcode={codes[fillableBarIndex + 2].opcode}）无跳转标签，无法插入跳过");
                }
            }
        }

        // 2. 背景跳过运行时检查（低索引）
        if (bgLdsfldIndex < 0)
        {
            Log.Error($"[Simple Health Bar] Transpiler failed: GenMapUI.DrawPawnLabel 中未找到 ldsfld TexUI.GrayTextBG（共 {codes.Count} 条指令）");
        }
        else if (bgCallIndex < 0)
        {
            Log.Error($"[Simple Health Bar] Transpiler failed: 在 ldsfld GrayTextBG（索引 {bgLdsfldIndex}）附近未找到 GUI.DrawTexture 调用，附近指令：{string.Join(" | ", Enumerable.Range(bgLdsfldIndex, Math.Min(4, codes.Count - bgLdsfldIndex)).Select(k => $"[{k}]{codes[k].opcode}"))}");
        }
        else
        {
            // 回溯到背景块起始（ldarg.s alwaysDrawBg）
            // 只用 Ldarg_S 而不用 Ldarg_0~3，避免错误匹配到 ldarg.1 (bgRect)
            int bgStart = bgLdsfldIndex;
            for (int j = bgLdsfldIndex; j >= Math.Max(0, bgLdsfldIndex - 10); j--)
            {
                if (codes[j].opcode == OpCodes.Ldarg_S)
                {
                    bgStart = j;
                    break;
                }
            }

            if (bgStart == bgLdsfldIndex)
            {
                Log.Error($"[Simple Health Bar] Transpiler failed: 在 ldsfld GrayTextBG（索引 {bgLdsfldIndex}）前 10 条内未找到 ldarg.s alwaysDrawBg，附近指令：{string.Join(" | ", Enumerable.Range(Math.Max(0, bgLdsfldIndex - 5), 6).Select(k => $"[{k}]{codes[k].opcode}"))}");
            }
            else
            {
                // 获取背景绘制后的标签（bge.un.s IL_0061 的目标）
                var skipLabels = codes[bgCallIndex + 1].labels;
                if (skipLabels.Count > 0)
                {
                    // 插入：if (ShouldSkipBackground()) skip background
                    codes.Insert(bgStart, new CodeInstruction(OpCodes.Brtrue, skipLabels[0]));
                    codes.Insert(bgStart, new CodeInstruction(OpCodes.Call, shouldSkipBgMethod));
                }
                else
                {
                    Log.Error($"[Simple Health Bar] Transpiler failed: GUI.DrawTexture 后一条指令（索引 {bgCallIndex + 1}，opcode={codes[bgCallIndex + 1].opcode}）无跳转标签，无法插入背景跳过");
                }
            }
        }

        return codes;
    }
}
