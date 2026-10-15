using HarmonyLib;
using RimWorld;
using Verse;

namespace ASQHPBar;

/// <summary>
/// 殖民者栏绘制钩子：
/// 1. 标记绘制区间并记录布局缩放 Scale，供宽度上限随栏缩放使用；
/// 2. 在 ColonistBar.ColonistBarOnGUI 末尾刷新批量渲染缓冲区。
/// 由于批量缓冲区仅在地图绘制链 (ThingOverlaysOnGUI) 中刷新，
/// ColonistBar 中绘制的血条数据若不在此处刷新将永远不会被绘制。
/// </summary>
[HarmonyPatch(typeof(ColonistBar), nameof(ColonistBar.ColonistBarOnGUI))]
public static class Patch_ColonistBar_ColonistBarOnGUI
{
    public static void Prefix(ColonistBar __instance)
    {
        // 就地取 Scale（cachedScale，恒 > 0），避免绘制期经 Find.ColonistBar 取值
        HealthBarEaseHelper.BeginColonistBarDraw(__instance != null ? __instance.Scale : 1f);
    }

    public static void Postfix()
    {
        HealthBarEaseHelper.EndColonistBarDraw();
        HealthBarEaseHelper.FlushBatch();
    }
}
