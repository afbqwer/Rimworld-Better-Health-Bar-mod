using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace ASQHPBar;

[StaticConstructorOnStartup]
public static partial class HealthBarEaseHelper
{
    // 本 Mod 自有纹理，不依赖 GenMapUI.OverlayHealthTex
    // — 血条纹理
    public static Texture2D EasedDamageTex = null!;
    public static Texture2D EasedEnemyDamageTex = null!;
    public static Texture2D EasedHealTex = null!;
    public static Texture2D ActualBarTex = null!;
    public static Texture2D FriendActualBarTex = null!;
    public static Texture2D PlayerActualBarTex = null!;
    public static Texture2D NeutralActualBarTex = null!;
    public static Texture2D BgTex = null!;

    // — 护盾条纹理
    public static Texture2D ShieldBarTex = null!;
    public static Texture2D ShieldDamageTex = null!;
    public static Texture2D ShieldHealTex = null!;

    // — 血液条纹理
    public static Texture2D BloodCurrentTex = null!;
    public static Texture2D BloodBleedTex = null!;

    // — 耐痛条纹理
    public static Texture2D PainBarTex = null!;
    public static Texture2D PainPlusTex = null!;
    public static Texture2D PainMinusTex = null!;

    // — 自爆阈值标记纹理
    public static Texture2D ExplosiveThresholdTex = null!;

    // — 渐变纹理（左右两端各 6 像素）
    private static Texture2D? LeftGradActual;
    private static Texture2D? RightGradActual;
    private static Texture2D? LeftGradFriend;
    private static Texture2D? RightGradFriend;
    private static Texture2D? LeftGradPlayer;
    private static Texture2D? RightGradPlayer;
    private static Texture2D? LeftGradNeutral;
    private static Texture2D? RightGradNeutral;

    // — 护盾条专用渐变纹理
    private static Texture2D? LeftGradShield;
    private static Texture2D? RightGradShield;

    // — 血液条专用渐变纹理
    private static Texture2D? LeftGradBlood;
    private static Texture2D? RightGradBlood;

    // — 耐痛条专用渐变纹理
    private static Texture2D? LeftGradPain;
    private static Texture2D? RightGradPain;

    // — 受伤缓动色专用左渐变（死亡/受伤缓动段从最左侧开始时的左端淡入，避免硬边）
    private static Texture2D? LeftGradDamage;
    private static Texture2D? LeftGradEnemyDamage;

    // — 常量与配置
    private static int GradWidth = 6;
    private const float GradThreshold = 0.96f;
    private const float FullHealthThreshold = 0.999f;
    private const int ShieldRefreshFrames = 60;
    public static HashSet<int> TurretDefs = new();
    /// <summary>缓存的 Time.frameCount</summary>
    private static int _currentFrame;
    /// <summary>缓存的 Find.TickManager.TicksGame</summary>
    private static int _currentTick;

    // — 批量渲染数据
    private struct BatchTexRect
    {
        public float x, y, w, h;
        public Texture2D tex;
        public float alpha; // 顶点色 alpha（淡出用），1 = 不透明
    }

    private static readonly List<BatchTexRect> batchTexRects = new List<BatchTexRect>(8192);
    internal static bool batchModeActive = false;
    /// <summary>当前绘制透明度（淡出用），由各绘制出口在调用绘制方法前设置、之后复位为 1。</summary>
    internal static float drawAlpha = 1f;

    // — 批量渲染跨帧清理（调用侧已确保仅在 Repaint 事件执行，无需追踪事件类型）
    private static int lastCollectFrame = -1;

    private static void BeginBatchCollection()
    {
        if (lastCollectFrame != _currentFrame)
        {
            batchTexRects.Clear();
            lastCollectFrame = _currentFrame;
        }
    }

    // — 反射缓存
    private static readonly FieldInfo ActivatedTickField = typeof(CompProjectileInterceptor).GetField("activatedTick",
        BindingFlags.Instance | BindingFlags.NonPublic);

    private class EaseData
    {
        public float easedValue = 1f;
        public int lastUpdateFrame = -1;
        public float startValue;
        public float currentTarget;
        public float elapsedTime;

        public float shieldEasedValue = 1f;
        public float shieldStartValue;
        public float shieldCurrentTarget;
        public float shieldElapsedTime;

        public int cachedMaxHitPoints;
        // — 护盾组件缓存
        public int lastShieldCacheFrame = -1;
        public List<CompShield> cachedShieldComps = new List<CompShield>();
        public List<object> cachedVfeShields = new List<object>();
        public object? cachedAxolotlShield;
        public object? cachedExosuitCore;
        public CompProjectileInterceptor? cachedInterceptor;
        public int cachedApparelCount = -1;

        // — 血液缓存（每60帧更新）
        public int lastBloodCacheFrame = -1;
        public float cachedBloodLoss;
        public float cachedBleedRate;

        // — 疼痛缓存（每120帧更新）
        public int lastPainCacheFrame = -1;
        public float cachedPainShockThreshold = -1f;

        public float painEasedValue = 1f;
        public float painStartValue;
        public float painCurrentTarget;
        public float painElapsedTime;

        // — 不变后隐藏计时状态（数值不变且缓动收敛达到延迟时间后隐藏）
        public HideTimer shieldHide = new(1f);
        public HideTimer bloodHide = new(0f);
        public HideTimer painHide = new(1f);
        public HideTimer healthHide = new(1f);

        // — 自爆阈值缓存（-1 表示尚未检查）
        public int cachedExplosiveThreshold = -1;

        // — 敌对关系缓存（每60帧更新一次）
        public int lastRelationshipCacheFrame = -1;
        public PawnRelationship cachedRelationship;

        // — 对玩家隐形缓存（每60帧更新一次）
        public int lastHiddenCacheFrame = -1;
        public bool cachedHiddenFromPlayer;

        // — Ammo Readout 兼容状态
        public int belowLabelExtentFrame = -1;   // 记录标签下方延伸量的帧号（Time.frameCount）
        public float belowLabelExtent;           // 本帧血条在标签下方实际延伸的像素高度（UI 空间）
        public float ammoShift;                  // 当前应用的弹药下移量（像素）
        public float ammoShiftFloorUntil;        // 该下移量保底的到期时刻（Time.realtimeSinceStartup）

        // — 死亡动画：绘制快照（记录最近一次绘制时血条的屏幕矩形，不做坐标变换）
        public bool hasDrawSnapshot;
        public int drawBarX;                      // 健康条屏幕矩形
        public int drawBarY;
        public int drawBarW;
        public int drawBarH;
        public bool drawGated;                    // 是否受 noNameMaxZoom 距离限制
        public CameraZoomRange drawMaxZoom;

        // — 死亡动画状态
        public bool dying;
        public int deathPhase;                     // 0 = 缓动到 0；1 = 消失阶段
        public float deathShrinkElapsed;
        public float deathWidthScale = 1f;
        public float deathAlpha = 1f;
        public float deathOffsetX;                 // 血条左上角相对死亡时锚点(单位 DrawPos 屏幕坐标)的偏移
        public float deathOffsetY;
        public int deathMapID = -1;                // 仅记录 Map.uniqueID，避免持有 Map 强引用
        public Vector3 deathWorldPos;
    }

    /// <summary>
    /// 附属条绘制上下文：每帧每个单位一份，避免闭包分配。
    /// </summary>
    private struct SubBarContext
    {
        public EaseData EaseData;
        public int SubBarX;
        public int SubBarW;
        public float ZoomScale;
        public bool Flipped;
        public bool SubBarsActive;
        public bool ShieldReplacesHealth;
        public float PainFillTarget;
        public float BloodLoss;
        public float BleedRate;
        public float ShieldFill;
        public float ShieldEasedValue;
    }

    /// <summary>
    /// 附属条定义：可见性、高度与绘制回调。
    /// 新增条类型时只需在 SimpleHealthBarSettings.SubBarKind 中添加枚举值，
    /// 并在 BuildSubBarDefs 中注册一条定义，堆叠布局与设置界面会自动支持。
    /// </summary>
    private sealed class SubBarDef
    {
        public System.Func<SubBarContext, bool> Visible = null!;
        public System.Func<SubBarContext, int> Height = null!;
        public System.Action<SubBarContext, int, int, int, int> Draw = null!;
        /// <summary>本条绘制透明度（自动隐藏淡出用）。</summary>
        public System.Func<SubBarContext, float> Alpha = null!;
        /// <summary>满血 + 下移时顶替健康条位置（护盾条）。</summary>
        public bool ReplacesHealth;
    }

    // 附属条注册表（按 SubBarKind 索引）
    private static readonly SubBarDef[] SubBarDefs = BuildSubBarDefs();

    private static SubBarDef[] BuildSubBarDefs()
    {
        int count = System.Enum.GetValues(typeof(SimpleHealthBarSettings.SubBarKind)).Length;
        var defs = new SubBarDef[count];

        defs[(int)SimpleHealthBarSettings.SubBarKind.Pain] = new SubBarDef
        {
            Visible = ctx => ctx.SubBarsActive && SimpleHealthBarSettings.enablePainBar
                && !ctx.EaseData.painHide.hidden
                && HasVisibleFill(ctx.PainFillTarget, ctx.EaseData.painEasedValue),
            Height = ctx => Mathf.Max(1, (int)(SimpleHealthBarSettings.painBarHeight * ctx.ZoomScale)),
            Draw = (ctx, x, y, w, h) => DrawPainBar(ctx.EaseData, x, y, w, h, ctx.PainFillTarget),
            Alpha = ctx => ctx.EaseData.painHide.alpha,
        };

        defs[(int)SimpleHealthBarSettings.SubBarKind.Blood] = new SubBarDef
        {
            Visible = ctx => ctx.SubBarsActive && SimpleHealthBarSettings.enableBloodBar
                && !SimpleHealthBarSettings.bloodBarAttachedMode
                && !ctx.EaseData.bloodHide.hidden
                && (ctx.BloodLoss > 0f || ctx.BleedRate > 0f),
            Height = ctx => Mathf.Max(1, (int)(SimpleHealthBarSettings.bloodBarHeight * ctx.ZoomScale)),
            Draw = (ctx, x, y, w, h) => DrawBloodBar(ctx.EaseData, x, y, w, h, ctx.BloodLoss, ctx.BleedRate),
            Alpha = ctx => ctx.EaseData.bloodHide.alpha,
        };

        defs[(int)SimpleHealthBarSettings.SubBarKind.Shield] = new SubBarDef
        {
            Visible = ctx => SimpleHealthBarSettings.enableShieldBar
                && !ctx.EaseData.shieldHide.hidden
                && (ctx.ShieldFill > 0f || ctx.ShieldEasedValue > 0f),
            Height = ctx => Mathf.Max(1, (int)(SimpleHealthBarSettings.shieldBarHeight * ctx.ZoomScale)),
            Draw = (ctx, x, y, w, h) => DrawShieldBar(x, y, w, h, ctx.ShieldFill, ctx.ShieldEasedValue),
            Alpha = ctx => ctx.EaseData.shieldHide.alpha,
            ReplacesHealth = true,
        };

        return defs;
    }

    // 普通字典，在 Thing.RemoveAllReservationsAndDesignationsOnThis Postfix 中手动释放
    private static readonly Dictionary<ThingWithComps, EaseData> easeDataTable = new();

    private static EaseData GetOrCreateEaseData(ThingWithComps thing)
    {
        if (easeDataTable.TryGetValue(thing, out var data))
            return data;
        var newData = new EaseData { cachedMaxHitPoints = thing.MaxHitPoints };
        easeDataTable.Add(thing, newData);
        return newData;
    }

    internal static void RemoveEaseData(ThingWithComps thing)
    {
        easeDataTable.Remove(thing);
    }

    // ——— 死亡残留血条动画 ———
    // 死亡条目持有原本位于 easeDataTable 中的 EaseData，动画结束后回收，避免强引用泄漏
    private static readonly List<EaseData> dyingBars = new List<EaseData>(16);

    /// <summary>
    /// 记录血条最近一次绘制的屏幕矩形，供死亡动画复用。
    /// 仅在 enableDeathBar 开启时写入，且只针对实际被绘制的血条（不含坐标变换）。
    /// </summary>
    private static void CaptureDrawSnapshot(EaseData e,
        int bgX, int bgY, int bgW, int bgH, bool labeled)
    {
        if (!SimpleHealthBarSettings.enableDeathBar) return;

        // 仅记录屏幕矩形，不做坐标变换（死亡登记时再做一次变换推算偏移）
        e.drawBarX = bgX;
        e.drawBarY = bgY;
        e.drawBarW = bgW;
        e.drawBarH = bgH;
        e.drawGated = !labeled;                       // 无标签路径受 noNameMaxZoom 距离限制
        e.drawMaxZoom = SimpleHealthBarSettings.noNameMaxZoom;
        e.hasDrawSnapshot = true;
    }

    /// <summary>Pawn 死亡时登记死亡血条动画（Pawn.Kill Prefix 调用，此时仍 Spawned）。</summary>
    internal static void RegisterPawnDeath(Pawn pawn)
    {
        if (!SimpleHealthBarSettings.enableDeathBar || !SimpleHealthBarSettings.enableImprovedHealthBar)
            return;
        RegisterDeath(pawn, pawn.Map);
    }

    /// <summary>炮台等 ThingWithComps 死亡时登记（Pawn 已由 RegisterPawnDeath 处理）。</summary>
    internal static void RegisterThingDeath(ThingWithComps thing)
    {
        if (!SimpleHealthBarSettings.enableDeathBar || !SimpleHealthBarSettings.enableImprovedHealthBar)
            return;
        if (thing is Pawn) return;
        if (!SimpleHealthBarSettings.enableTurretHealthBar) return;
        if (thing is not Building b || !TurretDefs.Contains(b.def.defNameHash)) return;
        RegisterDeath(thing, thing.Map);
    }

    private static void RegisterDeath(ThingWithComps thing, Map? map)
    {
        if (map == null) return;
        if (!easeDataTable.TryGetValue(thing, out var e)) return; // 从未显示过血条 → 跳过
        if (!e.hasDrawSnapshot || e.dying) return;

        RemoveEaseData(thing);                        // 改由 dyingBars 持有
        e.dying = true;
        e.deathPhase = 0;
        e.deathShrinkElapsed = 0f;
        e.deathWidthScale = 1f;
        e.deathMapID = map.uniqueID;
        e.deathWorldPos = thing.DrawPos;              // Prefix 阶段仍 Spawned
        // 用最近一帧绘制的屏幕矩形与死亡时锚点推算偏移（此处仅一次坐标变换）
        Vector2 anchor = WorldToScreenPoint(e.deathWorldPos);
        e.deathOffsetX = e.drawBarX - anchor.x;
        e.deathOffsetY = e.drawBarY - anchor.y;
        e.startValue = e.easedValue;
        e.currentTarget = 0f;
        e.elapsedTime = 0f;
        dyingBars.Add(e);
    }

    /// <summary>
    /// 驱动并绘制所有死亡残留血条（仅健康条，不含附属条）。
    /// 由 Patch_MapInterface_OnGUI 每帧（Repaint）调用一次，复用 DrawBarCore。
    /// </summary>
    internal static void DrawDyingBars()
    {
        if (dyingBars.Count == 0) return;

        Map? curMap = Find.CurrentMap;
        CameraDriver? cam = Find.CameraDriver;
        bool canDraw = curMap != null && cam != null;

        float dt = Time.deltaTime;
        float deathSpeed = Mathf.Max(0.0001f,
            SimpleHealthBarSettings.easeSpeed * SimpleHealthBarSettings.deathBarEaseSpeedMultiplier);
        float fadeTime = SimpleHealthBarSettings.deathBarFadeTime;

        bool prevBatch = batchModeActive;
        bool useBatch = SimpleHealthBarSettings.enableBatchRendering;
        batchModeActive = useBatch;
        if (canDraw && useBatch)
            BeginBatchCollection();

        for (int i = dyingBars.Count - 1; i >= 0; i--)
        {
            EaseData e = dyingBars[i];

            // 阶段 1：健康条填充缓动到 0
            if (e.deathPhase == 0)
            {
                if (SimpleHealthBarSettings.enableEaseEffect)
                    DoEase(dt, ref e.easedValue, ref e.startValue, ref e.currentTarget, ref e.elapsedTime, 0f, deathSpeed);
                else
                    e.easedValue = 0f;

                if (e.easedValue <= 0.002f)
                {
                    e.easedValue = 0f;
                    if (fadeTime <= 0f)
                    {
                        dyingBars.RemoveAt(i);
                        continue;
                    }
                    e.deathPhase = 1;
                    e.deathShrinkElapsed = 0f;
                }
            }

            // 阶段 2：按设置以淡出或宽度收缩消失
            if (e.deathPhase == 1)
            {
                e.deathShrinkElapsed += dt;
                if (e.deathShrinkElapsed >= fadeTime)
                {
                    dyingBars.RemoveAt(i);
                    continue;
                }
                float t = e.deathShrinkElapsed / fadeTime;
                if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
                if (SimpleHealthBarSettings.deathBarDisappearStyle == SimpleHealthBarSettings.DeathDisappearStyle.Fade)
                {
                    e.deathAlpha = 1f - t;
                    e.deathWidthScale = 1f;
                }
                else
                {
                    e.deathWidthScale = 1f - t;
                    e.deathAlpha = 1f;
                }
            }

            if (!canDraw || e.deathMapID != curMap!.uniqueID) continue;

            Vector2 anchor = WorldToScreenPoint(e.deathWorldPos);
            if (e.drawGated && (int)cam!.CurrentZoom > (int)e.drawMaxZoom) continue;
            if (IsOutsideViewport(anchor)) continue;

            // 健康条（复用 DrawBarCore，fillPercent 固定 0 → 显示受伤色拖尾）
            int barW = e.deathPhase == 1 ? (int)(e.drawBarW * e.deathWidthScale) : e.drawBarW;
            if (barW <= 0) continue;
            int barX = (int)(anchor.x + e.deathOffsetX) + (e.drawBarW - barW) / 2;
            int barY = (int)(anchor.y + e.deathOffsetY);
            drawAlpha = e.deathAlpha;
            DrawBarCore(barX, barY, barW, e.drawBarH, e.cachedRelationship, 0f, e, true);
            drawAlpha = 1f;
        }

        batchModeActive = prevBatch;
    }

    private static float CurrentEaseSpeed => SimpleHealthBarSettings.easeSpeed;
    private static float CurrentShieldEaseSpeed => SimpleHealthBarSettings.shieldEaseSpeed;
    private static float CurrentPainEaseSpeed => SimpleHealthBarSettings.painEaseSpeed;

    /// <summary>
    /// 在 GenMapUI.DrawPawnLabel 中调用，在名称标签上方绘制健康条。
    /// </summary>
    public static void DrawPawnHealthBarOnGUI(Pawn pawn, Rect bgRect, bool showLabel, float textWidth, float zoomScale = 1f)
    {
        if (!ShouldShowHealthBar(pawn, out var rel, out var easeData))
            return;

        float fillPercent;
        if (!pawn.Dead)
        {
            fillPercent = pawn.health.summaryHealth.SummaryHealthPercent;
        }
        else
        {
            var max = pawn.MaxHitPoints;
            fillPercent = max > 0 ? ((float)pawn.HitPoints / max) : 1f;
        }


        int bgX = (int)bgRect.x;
        int bgY = (int)bgRect.y;
        int bgW = (int)bgRect.width;
        int bgH = (int)bgRect.height;

        // 决定是否使用"上方"位置（单位头顶）
        bool useAbovePosition = SimpleHealthBarSettings.noNamePositionAbove
            && (!showLabel || SimpleHealthBarSettings.enableHealthBarOffset);

        // — Ammo Readout 兼容：弹药显示以名称标签下缘为基准，
        //   仅带标签且非头顶模式才可能与血条堆叠重叠，需跟踪标签下方延伸量
        float labelBottom = bgRect.yMax;
        bool belowLabelMode = showLabel && !useAbovePosition;
        int lowestDrawnBottom = (int)labelBottom;

        if (useAbovePosition)
        {
            Vector2 abovePos = GetScreenPosAbove(pawn);
            bgX = (int)(abovePos.x - bgRect.width / 2f);
            bgY = (int)(abovePos.y - SimpleHealthBarSettings.noNameHealthBarHeight);
            bgW = (int)bgRect.width;
            bgH = SimpleHealthBarSettings.noNameHealthBarHeight;
        }
        else
        {
            if (showLabel && SimpleHealthBarSettings.enableHealthBarOffset)
            {
                bgY = bgY + bgH;
                bgH = SimpleHealthBarSettings.healthBarHeight;
            }
            if (!showLabel)
            {
                bgY += SimpleHealthBarSettings.noNameYOffset;
            }
        }

        // 批量渲染模式路由（护盾条不与标签重叠，始终可用批量）
        bool useBatch = SimpleHealthBarSettings.enableBatchRendering;

        bool prevBatchMode = batchModeActive;
        batchModeActive = useBatch;

        if (useBatch)
            BeginBatchCollection();
        float shieldFill = SimpleHealthBarSettings.enableShieldBar ? GetShieldFillPercent(pawn, easeData) : 0f;

        // 耐痛条缓动目标值
        float painFillTarget = 0f;
        if (SimpleHealthBarSettings.enablePainBar)
        {
            RefreshPainCache(pawn, easeData);
            float painThreshold = easeData.cachedPainShockThreshold;
            if (painThreshold > 0f)
            {
                float painRatio = pawn.health.hediffSet.PainTotal / painThreshold;
                float rawPainFill = SimpleHealthBarSettings.invertPainBar ? 1f - painRatio : painRatio;
                painFillTarget = rawPainFill < 0f ? 0f : (rawPainFill > 1f ? 1f : rawPainFill);
            }
        }
        // 血液条数据刷新（先于缓动更新，保证隐藏计时使用最新值）
        float bloodLoss = 0f;
        float bleedRate = 0f;
        if (SimpleHealthBarSettings.enableBloodBar)
        {
            RefreshBloodCache(pawn, easeData);
            bloodLoss = easeData.cachedBloodLoss;
            bleedRate = easeData.cachedBleedRate;
        }

        UpdateEasedValues(easeData, fillPercent, shieldFill, painFillTarget, bloodLoss);

        bool isFullHealth = fillPercent >= FullHealthThreshold;
        bool healthHidden = easeData.healthHide.hidden;
        // 健康条（含附加血液标记）随其自动隐藏淡出
        drawAlpha = easeData.healthHide.alpha;

        if (isFullHealth || healthHidden)
        {
            if (showLabel && !SimpleHealthBarSettings.enableHealthBarOffset)
            {
                Rect labelbgRect;
                if (healthHidden)
                {
                    // 数值不变自动隐藏（隐藏时间 > 0）：复用显示态矩形，避免背景宽度跳变
                    labelbgRect = new Rect(bgX, bgY, bgW, bgH);
                }
                else
                {
                    // 满血时背景宽度只适配角色名称长度，不受 healthBarWidth 限制
                    float num = Prefs.DisableTinyText ? 6f : 4f;
                    float centerX = bgX + bgW / 2f;
                    float textOnlyWidth = textWidth + num * 2f;
                    int labelX = (int)(centerX - textOnlyWidth / 2f);
                    labelbgRect = new Rect(labelX, bgY, (int)textOnlyWidth, bgH);
                }
                GUI.DrawTexture(SnapRectToUIScalePixel(labelbgRect), BgTex);
            }
        }
        else
        {
            // 血条与标签重叠 → showLabel+!offset 时跳过批量，避免覆盖标签文字
            batchModeActive = useBatch && !(showLabel && !SimpleHealthBarSettings.enableHealthBarOffset);
            // 血条位于名称背景中时，其背景框即名称背景，自动隐藏只淡出血条本身
            bool barInLabelBg = showLabel && !SimpleHealthBarSettings.enableHealthBarOffset && !useAbovePosition;
            DrawBarCore(bgX, bgY, bgW, bgH, rel, fillPercent, easeData, !barInLabelBg);
            if (belowLabelMode)
                lowestDrawnBottom = Mathf.Max(lowestDrawnBottom, bgY + bgH);
        }

        // 护盾条在标签下方，不与标签重叠，总是可用批量
        batchModeActive = useBatch;

        // 附加模式血液标记：在健康条之上绘制当前剩余血液量比例的标记
        // 仅在剩余血液量 ≤ 85%（失血 ≥ 15%）时显示，避免满血时干扰
        if (SimpleHealthBarSettings.enableBloodBar
            && SimpleHealthBarSettings.bloodBarAttachedMode
            && SimpleHealthBarSettings.bgBorderSize >= 1
            && !easeData.bloodHide.hidden
            && !easeData.healthHide.hidden
            && bloodLoss >= 0.15f)
        {
            int border = Mathf.Min(SimpleHealthBarSettings.bgBorderSize, (bgH - 1) / 2);
            int barX = bgX + border;
            int barY = bgY + border;
            int barW = bgW - border * 2;
            int barH = bgH - border * 2;
            if (barW > 0 && barH > 0)
            {
                float bloodFill = 1f - bloodLoss;
                int markerX = barX + (int)(barW * bloodFill);
                int markerW = SimpleHealthBarSettings.thinBloodMarker ? 1 : Mathf.Max(1, (int)(2 * zoomScale));
                DrawBarFill(markerX, barY, markerW, barH, BloodCurrentTex, null, null, 0);
            }
        }

        drawAlpha = 1f;

        // 计算附属条（流血、护盾）宽度：有标签时受宽度设置控制
        int subBarX = bgX;
        int subBarW = bgW;
        if (showLabel)
        {
            if (SimpleHealthBarSettings.subBarSyncWithLabel)
            {
                subBarW = Mathf.Max(bgW, SimpleHealthBarSettings.healthBarMinWidth);
            }
            else
            {
                subBarW = SimpleHealthBarSettings.subBarWidth;
            }
            subBarX = bgX + (bgW - subBarW) / 2;
        }

        // ——— 附属条堆叠布局 ———
        // 顺序由设置 subBarOrder 控制，可见性与绘制由注册表 SubBarDefs 驱动（新增条类型无需改动此循环）
        // 游标从健康条矩形边界出发，仅实际可见的条才会占位，隐藏条不留空隙、不会重叠
        bool flipped = useAbovePosition;
        // 满血 + 下移时健康条不渲染，耐痛/血液条随之隐藏，仅护盾条顶替健康条位置
        bool subBarsActive = !isFullHealth || !showLabel || !SimpleHealthBarSettings.enableHealthBarOffset || flipped;
        bool shieldReplacesHealth = isFullHealth && showLabel && SimpleHealthBarSettings.enableHealthBarOffset && !flipped;

        var subBarCtx = new SubBarContext
        {
            EaseData = easeData,
            SubBarX = subBarX,
            SubBarW = subBarW,
            ZoomScale = zoomScale,
            Flipped = flipped,
            SubBarsActive = subBarsActive,
            ShieldReplacesHealth = shieldReplacesHealth,
            PainFillTarget = painFillTarget,
            BloodLoss = bloodLoss,
            BleedRate = bleedRate,
            ShieldFill = shieldFill,
            ShieldEasedValue = easeData.shieldEasedValue,
        };

        int cursorTop = bgY;
        int cursorBottom = bgY + bgH;

        // 按配置顺序逐条绘制附属条（满血 + 下移时护盾条顶替健康条位置）
        foreach (SimpleHealthBarSettings.SubBarKind kind in SimpleHealthBarSettings.GetSubBarOrder())
        {
            SubBarDef def = SubBarDefs[(int)kind];
            if (!def.Visible(subBarCtx))
                continue;
            int barH = def.Height(subBarCtx);
            int barY = def.ReplacesHealth && subBarCtx.ShieldReplacesHealth
                ? bgY
                : StackBarTop(ref cursorTop, ref cursorBottom, barH, subBarCtx.Flipped);
            drawAlpha = def.Alpha(subBarCtx); // 各条按自身自动隐藏淡出
            def.Draw(subBarCtx, subBarCtx.SubBarX, barY, subBarCtx.SubBarW, barH);
            if (belowLabelMode)
                lowestDrawnBottom = Mathf.Max(lowestDrawnBottom, barY + barH);
        }
        drawAlpha = 1f;

        // — Ammo Readout 兼容：记录本帧标签下方实际延伸量（弹药显示下移依据）
        easeData.belowLabelExtent = belowLabelMode
            ? Mathf.Max(0f, lowestDrawnBottom - labelBottom)
            : 0f;
        easeData.belowLabelExtentFrame = Time.frameCount;

        // 记录绘制快照，供死亡动画复用
        CaptureDrawSnapshot(easeData, bgX, bgY, bgW, bgH, showLabel);

        batchModeActive = prevBatchMode;
    }

    /// <summary>
    /// 计算该 pawn 弹药显示的本次下移量（Ammo Readout 兼容，含 5 秒防跳变：
    /// 下移量在最近一次增大后 5 秒内只增不减，避免血条出现/消失时弹药显示上下跳动）。
    /// </summary>
    public static float GetAmmoReadoutShift(Pawn pawn)
    {
        if (!AmmoReadoutCompat.Enabled || !SimpleHealthBarSettings.enableImprovedHealthBar)
            return 0f;
        if (pawn == null || !easeDataTable.TryGetValue(pawn, out var data))
            return 0f;

        // 仅当本帧血条确实绘制在标签下方时才需要下移；否则需要量归零
        float needed = data.belowLabelExtentFrame == Time.frameCount
            ? data.belowLabelExtent
            : 0f;

        float now = Time.realtimeSinceStartup;
        if (needed >= data.ammoShift)
        {
            // 需要量增大（或持平）：立即生效并重置 5 秒保底
            data.ammoShift = needed;
            data.ammoShiftFloorUntil = now;
        }
        else if (now >= data.ammoShiftFloorUntil)
        {
            // 距上次增大已超过 5 秒，才允许下移量减少
            data.ammoShift = needed;
        }
        // 否则：5 秒内保持当前下移量不变（防止跳变）
        return data.ammoShift;
    }

    /// <summary>
    /// 为炮台建筑（ThingWithComps）绘制健康条和护盾条。
    /// 血量 = HitPoints / cachedMaxHitPoints
    /// </summary>
    public static void DrawThingHealthBarOnGUI(ThingWithComps thing, Rect bgRect, float zoomScale = 1f)
    {
        if (!ShouldShowTurretHealthBar(thing, out var rel, out var easeData))
            return;
        if (!SimpleHealthBarSettings.enableTurretHealthBar)
            return;

        float fillPercent = easeData.cachedMaxHitPoints > 0
            ? (float)thing.HitPoints / easeData.cachedMaxHitPoints
            : 1f;

        // 缓存自爆阈值（仅首次检查，无需刷新）
        if (easeData.cachedExplosiveThreshold == -1 && SimpleHealthBarSettings.enableExplosiveThreshold)
        {
            var explosive = thing.TryGetComp<CompExplosive>();
            if (explosive != null)
            {
                var props = explosive.Props as CompProperties_Explosive;
                if (props != null && props.startWickHitPointsPercent > 0f)
                {
                    easeData.cachedExplosiveThreshold = Mathf.RoundToInt(props.startWickHitPointsPercent * easeData.cachedMaxHitPoints);
                }
                else
                {
                    easeData.cachedExplosiveThreshold = 0;
                }
            }
            else
            {
                easeData.cachedExplosiveThreshold = 0;
            }
        }

        int bgX = (int)bgRect.x;
        int bgY = (int)bgRect.y;
        int bgW = (int)bgRect.width;
        int bgH = (int)bgRect.height;

        if (SimpleHealthBarSettings.noNamePositionAbove)
        {
            Vector2 abovePos = GetScreenPosAbove(thing, 0.3f);
            bgX = (int)(abovePos.x - bgW / 2f);
            bgY = (int)(abovePos.y - SimpleHealthBarSettings.turretHealthBarHeight);
            bgH = SimpleHealthBarSettings.turretHealthBarHeight;
        }
        bgY += SimpleHealthBarSettings.noNameYOffset;

        float shieldFill = SimpleHealthBarSettings.enableShieldBar
            ? GetTurretShieldFillPercent(thing, easeData)
            : 0f;
        UpdateEasedValues(easeData, fillPercent, shieldFill, 0f, 0f);

        // 炮台无标签，直接按批量设置
        bool useBatch = SimpleHealthBarSettings.enableBatchRendering;
        bool prevBatchMode = batchModeActive;
        batchModeActive = useBatch;

        if (useBatch)
            BeginBatchCollection();

        bool isFullHealth = fillPercent >= FullHealthThreshold;

        drawAlpha = 1f; // 炮台健康条不参与自动隐藏淡出
        if (!isFullHealth)
            DrawBarCore(bgX, bgY, bgW, bgH, rel, fillPercent, easeData, true);

        // 绘制自爆阈值标记
        if (SimpleHealthBarSettings.enableExplosiveThreshold && easeData.cachedExplosiveThreshold > 0 && !isFullHealth)
        {
            float thresholdPercent = (float)easeData.cachedExplosiveThreshold / easeData.cachedMaxHitPoints;
            if (thresholdPercent > 0f && thresholdPercent < 1f)
            {
                int border = Mathf.Min(SimpleHealthBarSettings.bgBorderSize, (bgH - 1) / 2);
                int barX = bgX + border;
                int barY = bgY + border;
                int barW = bgW - border * 2;
                int barH = bgH - border * 2;
                if (barW > 0 && barH > 0)
                {
                    int markerX = barX + (int)(barW * thresholdPercent);
                    int markerW = SimpleHealthBarSettings.thinExplosiveMarker ? 1 : Mathf.Max(1, (int)(2 * zoomScale));
                    DrawBarFill(markerX, barY, markerW, barH, ExplosiveThresholdTex, null, null, 0);
                }
            }
        }

        // 护盾条：翻转布局时在健康条上方，否则在健康条下方
        if (SimpleHealthBarSettings.enableShieldBar
            && !easeData.shieldHide.hidden
            && (shieldFill > 0f || easeData.shieldEasedValue > 0f))
        {
            int shieldBgH = Mathf.Max(1, (int)(SimpleHealthBarSettings.shieldBarHeight * zoomScale));
            int shieldBgY = SimpleHealthBarSettings.noNamePositionAbove
                ? bgY - shieldBgH - SimpleHealthBarSettings.barSpacing + 1
                : bgY + bgH + SimpleHealthBarSettings.barSpacing - 1;
            drawAlpha = easeData.shieldHide.alpha; // 随护盾条自动隐藏淡出
            DrawShieldBar(bgX, shieldBgY, bgW, shieldBgH, shieldFill, easeData.shieldEasedValue);
            drawAlpha = 1f;
        }

        // 记录绘制快照，供死亡动画复用
        CaptureDrawSnapshot(easeData, bgX, bgY, bgW, bgH, false);

        batchModeActive = prevBatchMode;
    }

    /// <summary>
    /// 绘制血条的一段填充，支持左右两端渐变。
    /// 批量模式下将矩形数据加入缓冲区，不直接绘制。
    /// 接收整数像素坐标，由调用方统一完成 float→int 转换，消除拼接缝隙。
    /// </summary>
    private static void DrawBarFill(int x, int y, int w, int h, Texture2D solidTex,
        Texture2D? leftGradTex, Texture2D? rightGradTex, int rightGradPixels)
    {
        if (w <= 0) return;

        int leftPx = leftGradTex != null ? Mathf.Min(GradWidth, w) : 0;
        int rightPx = rightGradTex != null ? Mathf.Min(rightGradPixels, w) : 0;

        // 左右渐变不可重叠，若超出则各分一半
        if (leftPx + rightPx > w)
        {
            int half = w / 2;
            leftPx = half;
            rightPx = w - half;
        }

        int midPx = w - leftPx - rightPx;

        if (batchModeActive)
        {
            // 批量模式：收集到缓冲区（携带当前 drawAlpha，由 FlushBatch 输出为顶点色）
            if (leftPx > 0 && leftGradTex != null)
                batchTexRects.Add(new BatchTexRect { x = x, y = y, w = leftPx, h = h, tex = leftGradTex, alpha = drawAlpha });

            if (midPx > 0)
                batchTexRects.Add(new BatchTexRect { x = x + leftPx, y = y, w = midPx, h = h, tex = solidTex, alpha = drawAlpha });

            if (rightPx > 0 && rightGradTex != null)
                batchTexRects.Add(new BatchTexRect { x = x + w - rightPx, y = y, w = rightPx, h = h, tex = rightGradTex, alpha = drawAlpha });
        }
        else
        {
            // 传统模式：直接绘制（UIScale ≠ 1.0 时对齐到像素网格避免亚像素间隙）
            Color prevColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, drawAlpha);

            // 左渐变段（透明 → 实心）— 始于 (x, y)
            if (leftPx > 0)
                GUI.DrawTexture(SnapRectToUIScalePixel(new Rect(x, y, leftPx, h)), leftGradTex);

            // 中间实心段 — 始于 (x + leftPx, y)
            if (midPx > 0)
                GUI.DrawTexture(SnapRectToUIScalePixel(new Rect(x + leftPx, y, midPx, h)), solidTex);

            // 右渐变段（实心 → 透明）— 始于 (x + leftPx + midPx, y)
            if (rightPx > 0)
                GUI.DrawTexture(SnapRectToUIScalePixel(new Rect(x + w - rightPx, y, rightPx, h)), rightGradTex);

            GUI.color = prevColor;
        }
    }

    /// <summary>
    /// 绘制条背景框（携带当前 drawAlpha，兼容批量/传统两种模式）。
    /// </summary>
    private static void DrawBarBackground(int x, int y, int w, int h, float alpha)
    {
        if (batchModeActive)
        {
            batchTexRects.Add(new BatchTexRect { x = x, y = y, w = w, h = h, tex = BgTex, alpha = alpha });
        }
        else
        {
            Color prevColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(SnapRectToUIScalePixel(new Rect(x, y, w, h)), BgTex);
            GUI.color = prevColor;
        }
    }

    /// <summary>
    /// 绘制带 easing 的填充条（实心 + 受伤/治疗过渡色），供健康条、护盾条、耐痛条共用。
    /// damageLeftGrad：受伤过渡段从最左侧开始（如死亡动画填充为 0）时使用的左渐变，
    /// 使该段的左端与普通血条一样淡入，避免硬边；为 null 时保持原行为。
    /// </summary>
    private static void DrawEasedBarFill(int barX, int barY, int barW, int barH,
        Texture2D solidTex, Texture2D damageTex, Texture2D healTex,
        Texture2D? leftGrad, Texture2D? rightGrad, int rightGradPixels,
        float fillPercent, float easedValue, Texture2D? damageLeftGrad = null)
    {
        int fillPx = (int)(barW * fillPercent);
        int easedPx = (int)(barW * easedValue);

        if (easedValue > fillPercent)
        {
            int diffPx = easedPx - fillPx;
            DrawBarFill(barX, barY, fillPx, barH, solidTex, leftGrad, null, rightGradPixels);
            if (diffPx > 0)
                DrawBarFill(barX + fillPx, barY, diffPx, barH, damageTex,
                    fillPx > 0 ? null : damageLeftGrad, null, 0);
        }
        else if (easedValue < fillPercent)
        {
            int diffPx = fillPx - easedPx;
            DrawBarFill(barX, barY, easedPx, barH, solidTex, leftGrad, null, rightGradPixels);
            if (diffPx > 0)
                DrawBarFill(barX + easedPx, barY, diffPx, barH, healTex, null, null, 0);
        }
        else
        {
            DrawBarFill(barX, barY, fillPx, barH, solidTex, leftGrad, rightGrad, rightGradPixels);
        }
    }

    /// <summary>
    /// 根据关系选择纹理和渐变，绘制带 easing 的血条核心逻辑。
    /// fadeBackground=false 时背景框保持不透明（血条位于名称背景中时，名称背景不应随自动隐藏淡出）。
    /// </summary>
    private static void DrawBarCore(int bgX, int bgY, int bgW, int bgH, PawnRelationship rel,
        float fillPercent, EaseData easeData, bool fadeBackground)
    {
        DrawBarBackground(bgX, bgY, bgW, bgH, fadeBackground ? drawAlpha : 1f);

        Texture2D at = rel switch
        {
            PawnRelationship.Player => PlayerActualBarTex,
            PawnRelationship.Ally => FriendActualBarTex,
            PawnRelationship.Neutral => NeutralActualBarTex,
            _ => ActualBarTex
        };
        Texture2D damageTex = rel == PawnRelationship.Enemy ? EasedEnemyDamageTex : EasedDamageTex;
        bool useGrad = SimpleHealthBarSettings.enableBarGradient;
        // 受伤过渡段若从最左侧开始（死亡动画填充恒为 0），用与受伤色一致的左渐变避免硬边
        Texture2D? damageLeftGrad = useGrad
            ? (rel == PawnRelationship.Enemy ? LeftGradEnemyDamage : LeftGradDamage)
            : null;
        Texture2D? leftGrad;
        Texture2D? rightGrad;
        if (useGrad)
        {
            (leftGrad, rightGrad) = rel switch
            {
                PawnRelationship.Player => (LeftGradPlayer, RightGradPlayer),
                PawnRelationship.Ally => (LeftGradFriend, RightGradFriend),
                PawnRelationship.Neutral => (LeftGradNeutral, RightGradNeutral),
                _ => (LeftGradActual, RightGradActual)
            };
        }
        else
        {
            leftGrad = null;
            rightGrad = null;
        }

        int border = Mathf.Min(SimpleHealthBarSettings.bgBorderSize, (bgH - 1) / 2);
        int barX = bgX + border;
        int barY = bgY + border;
        int barW = bgW - border * 2;
        int barH = bgH - border * 2;
        if (barW > 0)
        {
            int rightGradPixels = (int)(GradWidth * (fillPercent <= GradThreshold ? 0f : fillPercent >= 1f ? 1f : (fillPercent - GradThreshold) / (1f - GradThreshold)));
            DrawEasedBarFill(barX, barY, barW, barH, at, damageTex, EasedHealTex,
                leftGrad, rightGrad, rightGradPixels, fillPercent, easeData.easedValue, damageLeftGrad);
        }
    }

    /// <summary>
    /// 条内是否仍有可显示的内容：缓动尚未完全归零（<code>0</code>）或完全充满（<code>1</code>）。
    /// 用于耐痛条等"空/满即隐藏"的显示判定，与缓动收敛后的最终状态保持一致。
    /// </summary>
    private static bool HasVisibleFill(float target, float eased)
        => !(target <= 0f && eased <= 0.002f) && !(target >= 1f && eased >= 0.998f);

    /// <summary>
    /// 从健康条矩形向外堆叠附属条：flipped=false 向下堆叠，flipped=true 向上堆叠。
    /// 始终基于上一根实际放置的条计算新条位置，隐藏条不占位，避免重叠或空隙。
    /// 返回新条顶部 Y 坐标，并同步更新游标。
    /// </summary>
    private static int StackBarTop(ref int cursorTop, ref int cursorBottom, int barH, bool flipped)
    {
        int barTop = flipped
            ? cursorTop - barH - SimpleHealthBarSettings.barSpacing + 1
            : cursorBottom + SimpleHealthBarSettings.barSpacing - 1;
        if (flipped)
            cursorTop = barTop;
        else
            cursorBottom = barTop + barH;
        return barTop;
    }

    /// <summary>
    /// 绘制护盾条。位置与可见性由调用方通过堆叠布局计算并判定，本方法只负责在给定矩形内绘制。
    /// </summary>
    private static void DrawShieldBar(int bgX, int bgY, int bgW, int bgH, float shieldFill, float easedShield)
    {
        DrawBarBackground(bgX, bgY, bgW, bgH, drawAlpha);

        int shieldBorder = Mathf.Min(SimpleHealthBarSettings.bgBorderSize, (bgH - 1) / 2);
        int barX = bgX + shieldBorder;
        int barY = bgY + shieldBorder;
        int barW = bgW - shieldBorder * 2;
        int barH = bgH - shieldBorder * 2;
        if (barW <= 0 || barH <= 0) return;

        bool useGrad = SimpleHealthBarSettings.enableBarGradient;
        Texture2D? leftGrad = useGrad ? LeftGradShield : null;
        Texture2D? rightGrad = useGrad ? RightGradShield : null;
        int rightGradPixels = useGrad ? (int)(GradWidth * (shieldFill <= GradThreshold ? 0f : shieldFill >= 1f ? 1f : (shieldFill - GradThreshold) / (1f - GradThreshold))) : 0;

        DrawEasedBarFill(barX, barY, barW, barH, ShieldBarTex, ShieldDamageTex, ShieldHealTex,
            leftGrad, rightGrad, rightGradPixels, shieldFill, easedShield);
    }

    /// <summary>
    /// 绘制耐痛条。位置与可见性由调用方通过堆叠布局计算并判定。
    /// 显示值 = PainTotal / PainShockThreshold（Clamp01），阈值每60帧刷新缓存。
    /// 支持缓动（+：条增长，-：条减少），缓动受全局缓动开关控制。
    /// </summary>
    private static void DrawPainBar(EaseData easeData, int bgX, int bgY, int bgW, int bgH, float painFill)
    {
        DrawBarBackground(bgX, bgY, bgW, bgH, drawAlpha);

        int border = Mathf.Min(SimpleHealthBarSettings.bgBorderSize, (bgH - 1) / 2);
        int barX = bgX + border;
        int barY = bgY + border;
        int barW = bgW - border * 2;
        int barH = bgH - border * 2;
        if (barW <= 0 || barH <= 0) return;

        bool useGrad = SimpleHealthBarSettings.enableBarGradient;
        Texture2D? leftGrad = useGrad ? LeftGradPain : null;
        Texture2D? rightGrad = useGrad ? RightGradPain : null;
        int rightGradPixels = useGrad ? (int)(GradWidth * (painFill <= GradThreshold ? 0f : painFill >= 1f ? 1f : (painFill - GradThreshold) / (1f - GradThreshold))) : 0;

        DrawEasedBarFill(barX, barY, barW, barH, PainBarTex, PainMinusTex, PainPlusTex,
            leftGrad, rightGrad, rightGradPixels, painFill, easeData.painEasedValue);
    }

    /// <summary>
    /// 绘制独立模式血液条。位置与可见性由调用方通过堆叠布局计算并判定。
    /// 附加模式下的血液标记由 DrawPawnHealthBarOnGUI 直接绘制在健康条上。
    /// </summary>
    private static void DrawBloodBar(EaseData easeData, int bgX, int bgY, int bgW, int bgH,
        float bloodLoss, float bleedRate)
    {
        DrawBarBackground(bgX, bgY, bgW, bgH, drawAlpha);

        int border = Mathf.Min(SimpleHealthBarSettings.bgBorderSize, (bgH - 1) / 2);
        int barX = bgX + border;
        int barY = bgY + border;
        int barW = bgW - border * 2;
        int barH = bgH - border * 2;
        if (barW <= 0 || barH <= 0) return;

        float rawBleedRatio = bleedRate * SimpleHealthBarSettings.bloodBleedFactor / 10f;
        float bleedRateRatio = rawBleedRatio < 0f ? 0f : (rawBleedRatio > 1f ? 1f : rawBleedRatio);
        float bloodFill = 1f - bloodLoss;
        bool useGrad = SimpleHealthBarSettings.enableBarGradient;
        Texture2D? leftGrad = useGrad ? LeftGradBlood : null;
        Texture2D? rightGrad = useGrad ? RightGradBlood : null;
        int rightGradPixels = useGrad ? (int)(GradWidth * (bloodFill <= GradThreshold ? 0f : bloodFill >= 1f ? 1f : (bloodFill - GradThreshold) / (1f - GradThreshold))) : 0;

        int bleedRightGradPixels = useGrad ? (int)(GradWidth * (bleedRateRatio <= GradThreshold ? 0f : bleedRateRatio >= 1f ? 1f : (bleedRateRatio - GradThreshold) / (1f - GradThreshold))) : 0;
        int bloodFillPx = (int)(barW * bloodFill);
        int bleedPx = (int)(barW * bleedRateRatio);

        if (bleedPx > 0 && bloodFillPx > 0)
        {
            // 当前血液不包含流血速率的部分
            int stablePx = Mathf.Max(0, bloodFillPx - bleedPx);
            int bleedPxClamped = Mathf.Min(bleedPx, barW - stablePx);
            if (stablePx > 0)
                DrawBarFill(barX, barY, stablePx, barH, BloodCurrentTex, leftGrad, null, rightGradPixels);
            // 流血速率覆盖部分（在血液条右端），使用独立的右端渐变
            int bleedStartX = barX + stablePx;
            if (bleedPxClamped > 0)
                DrawBarFill(bleedStartX, barY, bleedPxClamped, barH, BloodBleedTex, null, rightGrad, bleedRightGradPixels);
        }
        else if (bloodFillPx > 0)
        {
            DrawBarFill(barX, barY, bloodFillPx, barH, BloodCurrentTex, leftGrad, rightGrad, rightGradPixels);
        }
    }
}
