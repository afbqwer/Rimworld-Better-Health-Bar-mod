using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace ASQHPBar;

/// <summary>
/// 在 ThingOverlaysOnGUI 结束后刷新血条的批量渲染缓冲区。
/// 执行顺序：单位血条收集
///        → 炮台血条
///        → 此处 FlushBatch
/// </summary>
[HarmonyPatch(typeof(ThingOverlays), nameof(ThingOverlays.ThingOverlaysOnGUI))]
[HarmonyPriority(Priority.High)]
public static class Patch_MapInterface_OnGUI
{
    public static void Prefix()
    {
        HealthBarEaseHelper.UpdateCachedTimeValues();
        if (Event.current.type != EventType.Repaint || Find.CameraDriver == null)
            return;
        // 绘制炮台血条
        DrawTurretHealthBar();
        // 兼容模式：绘制所有 Pawn 血条
        DrawCompatibilityModeHealthBars();
        // 死亡残留血条动画（复用同一 Repaint 帧与批量缓冲）
        HealthBarEaseHelper.DrawDyingBars();
    }

    public static void Postfix()
    {
        HealthBarEaseHelper.FlushBatch();
    }

    // — 炮台建筑缓存：每 120 帧重建一次，避免每帧对全部人工建筑做类型判断与 HashSet 查询
    private static readonly List<Building> cachedTurretBuildings = new List<Building>();
    // 仅缓存 Map 的 uniqueID，避免持有 Map 强引用
    private static int cachedTurretMapID = -1;
    private static int lastTurretCacheFrame = -1;
    private const int TurretCacheRefreshFrames = 120;

    private static void RefreshTurretBuildingCache(Map map)
    {
        cachedTurretBuildings.Clear();
        var turretDefs = HealthBarEaseHelper.TurretDefs;
        List<Thing> list = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial);
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] is Building bd && turretDefs.Contains(bd.def.defNameHash) && bd.Spawned)
                cachedTurretBuildings.Add(bd);
        }
    }

    private static void DrawTurretHealthBar()
    {
        if (SimpleHealthBarSettings.enableTurretHealthBar)
        {
            var zoom = Find.CameraDriver.CurrentZoom;
            if ((int)zoom <= (int)SimpleHealthBarSettings.noNameMaxZoom)
            {
                Map map = Find.CurrentMap;
                if (map != null)
                {
                    int frame = Time.frameCount;
                    if (map.uniqueID != cachedTurretMapID || frame - lastTurretCacheFrame >= TurretCacheRefreshFrames)
                    {
                        RefreshTurretBuildingCache(map);
                        cachedTurretMapID = map.uniqueID;
                        lastTurretCacheFrame = frame;
                    }

                    for (int i = 0; i < cachedTurretBuildings.Count; i++)
                    {
                        Building bd = cachedTurretBuildings[i];
                        if (!bd.Spawned)
                            continue;

                        Vector2 screenPos = HealthBarEaseHelper.GetScreenPosBelow(bd);//GenMapUI.LabelDrawPosFor(bd, -0.6f);
                        if (HealthBarEaseHelper.IsOutsideViewport(screenPos))
                            continue;
                        float barWidth = bd.DrawSize.x * SimpleHealthBarSettings.turretHealthBarWidthMultiplier;
                        float barHeight = SimpleHealthBarSettings.turretHealthBarHeight;

                        float zoomScale = 1f;
                        if (SimpleHealthBarSettings.enableZoomScale)
                        {
                            zoomScale = SimpleHealthBarSettings.GetZoomScale();
                            barWidth *= zoomScale;
                            barHeight = Mathf.Max(4f, barHeight * zoomScale);
                            if (barWidth < 8f)
                                continue;
                        }

                        var barRect = new Rect(
                            screenPos.x - barWidth / 2f,
                            screenPos.y + barHeight / 2f,
                            barWidth,
                            barHeight
                        );
                        HealthBarEaseHelper.DrawThingHealthBarOnGUI(bd, barRect, zoomScale);
                    }
                }
            }
        }
    }

    private static void DrawCompatibilityModeHealthBars()
    {
        if (!SimpleHealthBarSettings.enableCompatibilityMode || !SimpleHealthBarSettings.enableImprovedHealthBar)
            return;
        var zoom = Find.CameraDriver.CurrentZoom;
        if ((int)zoom > (int)SimpleHealthBarSettings.noNameMaxZoom)
            return;

        Map map = Find.CurrentMap;
        if (map == null)
            return;

        var pawns = map.mapPawns.AllPawnsSpawned;
        for (int i = 0; i < pawns.Count; i++)
        {
            Pawn pawn = pawns[i];
            if (pawn == null || pawn.Dead)
                continue;

            // 与 GenMapUI.DrawPawnLabel 使用相同的绘制位置
            Vector2 screenPos = GenMapUI.LabelDrawPosFor(pawn, -0.6f);
            if (HealthBarEaseHelper.IsOutsideViewport(screenPos))
                continue;

            float width = SimpleHealthBarSettings.healthBarMinWidth;
            float height = SimpleHealthBarSettings.noNameHealthBarHeight;

            float healthScaleExtra = SimpleHealthBarSettings.healthScaleWidthMultiplier <= 0f ? 0f : HealthBarEaseHelper.GetHealthScaleSafe(pawn) * SimpleHealthBarSettings.healthScaleWidthMultiplier;
            if (healthScaleExtra > 0f)
                width += healthScaleExtra;

            int maxBarWidth = SimpleHealthBarSettings.EffectiveMaxBarWidth;
            if (maxBarWidth > 0 && width > maxBarWidth)
                width = maxBarWidth;

            float zoomScale = 1f;
            if (SimpleHealthBarSettings.enableZoomScale && pawn.NonHumanlikeOrWildMan())
            {
                zoomScale = SimpleHealthBarSettings.GetZoomScale();
                width *= zoomScale;
                height = Mathf.Max(4f, height * zoomScale);
                if (width < 8f)
                    continue;
            }

            Rect barRect = new Rect(screenPos.x - width / 2f, screenPos.y + height / 2, width, height);
            HealthBarEaseHelper.DrawPawnHealthBarOnGUI(pawn, barRect, true, 1);
        }
    }

}