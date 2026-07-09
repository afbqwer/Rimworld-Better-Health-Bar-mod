using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace ASQHPBar;

/// <summary>
/// 与 Ammo Readout (Andromeda.AmmoReadout) 的兼容层。
/// 通过反射定位其 NamePlate_DrawIndentBlock_Patch.Postfix，并注入 Prefix 在绘制弹药前把
/// offsetMarks 下移，避免弹药显示被本 Mod 血条（标签下方堆叠）遮挡。
/// 全部反射访问均包裹 try/catch，失败则静默禁用兼容。
/// </summary>
public static class AmmoReadoutCompat
{
    /// <summary>兼容是否已成功启用（设置界面据此显示提示）。</summary>
    public static bool Enabled { get; private set; }

    /// <summary>是否已尝试过初始化（幂等标记，避免重复扫描/重复补丁）。</summary>
    private static bool _resolved;

    private static Harmony? _harmony;

    public static void TryInit()
    {
        if (_resolved)
            return;
        _resolved = true;
        try
        {
            if (!ModsConfig.IsActive("Andromeda.AmmoReadout"))
                return;

            Type? patchType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == "AmmoReadout")
                {
                    patchType = asm.GetType("AmmoReadout.NamePlate_DrawIndentBlock_Patch");
                    break;
                }
            }
            if (patchType == null)
                return;

            MethodInfo? postfix = patchType.GetMethod("Postfix",
                BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(Pawn), typeof(Rect), typeof(float).MakeByRefType(), typeof(bool) }, null);
            if (postfix == null)
                return;

            MethodInfo prefix = typeof(AmmoReadoutCompat).GetMethod(
                nameof(PostfixPrefix), BindingFlags.NonPublic | BindingFlags.Static)!;

            _harmony = new Harmony("assssssqwww.ASQHPBar.AmmoReadoutCompat");
            _harmony.Patch(postfix, prefix: new HarmonyMethod(prefix));
            Enabled = true;
        }
        catch (Exception e)
        {
            Log.Warning("[ASQHPBar] Ammo Readout 兼容初始化失败，已禁用该兼容：" + e.Message);
        }
    }

    /// <summary>
    /// 注入到 AmmoReadout Postfix 前的 Prefix：按 pawn 血条在标签下方的实际延伸量下移 offsetMarks。
    /// Harmony 按参数名匹配 pawn 与 ref offsetMarks。
    /// </summary>
    private static void PostfixPrefix(Pawn pawn, ref float offsetMarks)
    {
        try
        {
            if (!Enabled)
                return;
            offsetMarks += HealthBarEaseHelper.GetAmmoReadoutShift(pawn);
        }
        catch (Exception e)
        {
            Log.Warning("[ASQHPBar] Ammo Readout 兼容 Prefix 执行异常：" + e.Message);
        }
    }
}
