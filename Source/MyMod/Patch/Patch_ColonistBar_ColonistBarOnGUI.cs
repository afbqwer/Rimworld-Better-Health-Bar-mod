using HarmonyLib;
using RimWorld;
using Verse;

namespace ASQHPBar;

/// <summary>
/// 在 ColonistBar.ColonistBarOnGUI 末尾刷新批量渲染缓冲区。
/// 由于批量缓冲区仅在地图绘制链 (ThingOverlaysOnGUI) 中刷新，
/// ColonistBar 中绘制的血条数据若不在此处刷新将永远不会被绘制。
/// </summary>
[HarmonyPatch(typeof(ColonistBar), nameof(ColonistBar.ColonistBarOnGUI))]
public static class Patch_ColonistBar_ColonistBarOnGUI
{
    public static void Postfix()
    {
        HealthBarEaseHelper.FlushBatch();
    }
}
