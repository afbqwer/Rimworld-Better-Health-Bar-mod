using HarmonyLib;
using UnityEngine;
using Verse;

namespace ASQHPBar;

/// <summary>
/// 修正名称标签位置，排除掉 Pawn 的抖动（jitter）影响，
/// 使名称标签不受受伤、格挡等抖动效果干扰。
/// </summary>
[HarmonyPatch(typeof(GenMapUI), nameof(GenMapUI.LabelDrawPosFor), typeof(Thing), typeof(float))]
public static class Patch_GenMapUI_LabelDrawPosFor
{
    public static void Postfix(ref Vector2 __result, Thing thing, float worldOffsetZ)
    {
        if (!SimpleHealthBarSettings.enableStableLabel)
            return;

        if (thing is not Pawn pawn)
            return;

        Vector3 jitter = HealthBarEaseHelper.GetJitterOffsetDelegate?.Invoke(pawn) ?? Vector3.zero;
        if (jitter == Vector3.zero)
            return;

        // 正交相机下 WorldToScreenPoint 是线性仿射变换：
        //   WTS(pos) - WTS(pos - jitter) = WTS(jitter) - WTS(zero)
        // 因此直接用 jitter 向量计算屏幕偏移，无需再次调用 pawn.DrawPos
        Vector2 delta = (Find.Camera.WorldToScreenPoint(jitter)
                       - Find.Camera.WorldToScreenPoint(Vector3.zero)) / Prefs.UIScale;

        // LabelDrawPosFor 中做了 result.y = screenHeight - result.y 的翻转，
        // 所以 Y 方向的 delta 要取反才能对齐 UI 坐标
        __result.x -= delta.x;
        __result.y += delta.y;
    }
}