using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ASQHPBar;

public static partial class HealthBarEaseHelper
{
    #region 屏幕坐标转换与缓存

    /// <summary>上次更新屏幕缓存时的帧号</summary>
    private static int _cacheLastFrame = -1;
    private static float _cachedUIScale = 1f;
    private static float _cachedScreenWidth;
    private static float _cachedScreenHeight;

    public static void UpdateCachedTimeValues()
    {
        _currentFrame = Time.frameCount;
        _currentTick = Find.TickManager.TicksGame;
    }

    /// <summary>
    /// 刷新屏幕尺寸缓存（每帧一次）。
    /// </summary>
    private static void RefreshScreenCache()
    {
        _cacheLastFrame = _currentFrame;
        _cachedUIScale = Prefs.UIScale;
        _cachedScreenWidth = Screen.width;
        _cachedScreenHeight = Screen.height;
    }

    /// <summary>
    /// 将世界坐标转为屏幕坐标（UI 空间，左上角原点），与 GenMapUI.LabelDrawPosFor 逻辑一致。
    /// </summary>
    private static Vector2 WorldToScreenPoint(Vector3 worldPos)
    {
        if (_currentFrame != _cacheLastFrame)
            RefreshScreenCache();

        Vector2 result = Find.Camera.WorldToScreenPoint(worldPos);
        result.y = _cachedScreenHeight - result.y;
        result /= _cachedUIScale;
        return result;
    }

    #endregion

    #region Jitter 偏移委托（表达式树）

    internal static Func<Pawn, Vector3>? GetJitterOffsetDelegate
    {
        get
        {
            if (_getJitterOffset == null)
                InitJitterOffsetDelegate();
            return _getJitterOffset;
        }
    }

    private static Func<Pawn, Vector3>? _getJitterOffset;

    private static void InitJitterOffsetDelegate()
    {
        try
        {
            var jittererField = typeof(Pawn_DrawTracker).GetField("jitterer",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var currentOffsetProp = typeof(JitterHandler).GetProperty("CurrentOffset",
                BindingFlags.Instance | BindingFlags.Public);

            if (jittererField == null || currentOffsetProp == null)
                return;

            var pawnParam = Expression.Parameter(typeof(Pawn), "pawn");
            // pawn.Drawer → Pawn_DrawTracker
            var drawerExpr = Expression.Property(pawnParam, "Drawer");
            // pawn.Drawer.jitterer → JitterHandler
            var jittererExpr = Expression.Field(drawerExpr, jittererField);
            // pawn.Drawer.jitterer.CurrentOffset → Vector3
            var currentOffsetExpr = Expression.Property(jittererExpr, currentOffsetProp);

            _getJitterOffset = Expression.Lambda<Func<Pawn, Vector3>>(
                currentOffsetExpr, pawnParam).Compile();
        }
        catch (Exception e)
        {
            Log.Error($"[HealthBar] Failed to init jitter offset delegate: {e}");
        }
    }

    /// <summary>
    /// 获取 Pawn 的抖动偏移量（jitter），非 Pawn 或 enableStableLabel 关闭时返回 Vector3.zero。
    /// </summary>
    private static Vector3 GetJitterOffset(Pawn pawn)
    {
        if (SimpleHealthBarSettings.enableStableLabel)
        {
            if (_getJitterOffset == null)
                InitJitterOffsetDelegate();
            return _getJitterOffset?.Invoke(pawn) ?? Vector3.zero;
        }
        return Vector3.zero;
    }

    #endregion

    #region 屏幕位置计算（上/下方）

    /// <summary>
    /// 获取thing上方的屏幕位置。
    /// 通过 DrawSize.y + drawOffset 计算视觉上边缘，适配不同尺寸的单位。
    /// </summary>
    /// <param name="thing">目标物体</param>
    /// <param name="extraOffset">上方额外偏移量（世界单位，正值 = 更靠上）</param>
    /// <returns>屏幕坐标（左上角为原点）</returns>
    public static Vector2 GetScreenPosAbove(Thing thing, float extraOffset = 0f)
    {
        Vector3 drawPos = thing.DrawPos;
        // 用 DrawSize.y 计算单位的视觉高度，偏移到视觉上边缘 + extraOffset
        drawPos.z += thing.DrawSize.y / 2f + extraOffset;

        return WorldToScreenPoint(drawPos);
    }

    /// <summary>
    /// 获取thing下方的屏幕位置。
    /// 通过 DrawSize.y + drawOffset 计算视觉下边缘，适配不同尺寸的单位。
    /// </summary>
    /// <param name="thing">目标物体</param>
    /// <param name="extraOffset">下方额外偏移量（世界单位，正值 = 更靠下）</param>
    /// <returns>屏幕坐标（左上角为原点）</returns>
    public static Vector2 GetScreenPosBelow(Thing thing, float extraOffset = 0f)
    {
        Vector3 drawPos = thing.DrawPos;
        // 用 DrawSize.y 计算单位的视觉高度，偏移到视觉下边缘 + extraOffset
        drawPos.z -= thing.DrawSize.y / 2f + extraOffset;

        return WorldToScreenPoint(drawPos);
    }

    /// <summary>
    /// 获取单位上方的屏幕位置。
    /// 通过 DrawSize.y + drawOffset 计算视觉上边缘，适配不同尺寸的单位。
    /// </summary>
    /// <param name="pawn">目标物体</param>
    /// <param name="extraOffset">上方额外偏移量（世界单位，正值 = 更靠上）</param>
    /// <returns>屏幕坐标（左上角为原点）</returns>
    public static Vector2 GetScreenPosAbove(Pawn pawn, float extraOffset = 0f)
    {
        Vector3 drawPos = pawn.DrawPos;
        // 抵消 jitter 使血条位置不受受伤/格挡等抖动影响
        drawPos -= GetJitterOffset(pawn);
        // 用 DrawSize.y 计算单位的视觉高度，偏移到视觉上边缘 + extraOffset
        drawPos.z += pawn.DrawSize.y / 2f + extraOffset;

        return WorldToScreenPoint(drawPos);
    }

    /// <summary>
    /// 获取单位下方的屏幕位置。
    /// 通过 DrawSize.y + drawOffset 计算视觉下边缘，适配不同尺寸的单位。
    /// </summary>
    /// <param name="pawn">目标物体</param>
    /// <param name="extraOffset">下方额外偏移量（世界单位，正值 = 更靠下）</param>
    /// <returns>屏幕坐标（左上角为原点）</returns>
    public static Vector2 GetScreenPosBelow(Pawn pawn, float extraOffset = 0f)
    {
        Vector3 drawPos = pawn.DrawPos;
        // 抵消 jitter 使血条位置不受受伤/格挡等抖动影响
        drawPos -= GetJitterOffset(pawn);
        // 用 DrawSize.y 计算单位的视觉高度，偏移到视觉下边缘 + extraOffset
        drawPos.z -= pawn.DrawSize.y / 2f + extraOffset;

        return WorldToScreenPoint(drawPos);
    }

    #endregion

    #region 视口剔除检测

    /// <summary>
    /// 检查屏幕坐标是否在屏幕视野外（含指定像素余量）。
    /// </summary>
    /// <param name="screenPos">屏幕坐标（UI 空间，左上角原点）</param>
    /// <param name="marginPx">像素余量，默认 200px</param>
    /// <returns>true 表示在视野外，应剔除</returns>
    public static bool IsOutsideViewport(Vector2 screenPos, float marginPx = 200f)
    {
        float uiW = _cachedScreenWidth / _cachedUIScale;
        float uiH = _cachedScreenHeight / _cachedUIScale;
        return screenPos.x < -marginPx
            || screenPos.x > uiW + marginPx
            || screenPos.y < -marginPx
            || screenPos.y > uiH + marginPx;
    }

    /// <summary>
    /// 检查世界坐标是否在屏幕视野外（含指定像素余量）。
    /// 利用已有的 VP 矩阵缓存和 <see cref="WorldToScreenPoint(Vector3)"/> 方法，
    /// 无需额外计算开销。
    /// </summary>
    /// <param name="worldPos">世界坐标</param>
    /// <param name="marginPx">像素余量，默认 200px</param>
    /// <returns>true 表示在视野外，应剔除</returns>
    public static bool IsOutsideViewport(Vector3 worldPos, float marginPx = 200f)
    {
        return IsOutsideViewport(WorldToScreenPoint(worldPos), marginPx);
    }

    /// <summary>
    /// 检查 Thing 的 <see cref="Thing.DrawPos"/> 是否在屏幕视野外。
    /// </summary>
    /// <param name="thing">目标物体</param>
    /// <param name="marginPx">像素余量，默认 200px</param>
    /// <returns>true 表示在视野外，应剔除</returns>
    public static bool IsOutsideViewport(Thing thing, float marginPx = 200f)
    {
        return IsOutsideViewport(thing.DrawPos, marginPx);
    }

    #endregion

    #region 阵营关系判定

    private enum PawnRelationship { Player, Ally, Neutral, Enemy }

    private static PawnRelationship GetRelationship(ThingWithComps thing, out EaseData easeData)
    {
        easeData = GetOrCreateEaseData(thing);

        // 缓存：每60帧更新一次
        if (_currentFrame - easeData.lastRelationshipCacheFrame < 60)
            return easeData.cachedRelationship;

        easeData.lastRelationshipCacheFrame = _currentFrame;

        Faction playerFaction = Faction.OfPlayerSilentFail;
        if (playerFaction == null)
        {
            easeData.cachedRelationship = PawnRelationship.Neutral;
            return easeData.cachedRelationship;
        }

        Faction faction = thing.Faction;
        if (faction == playerFaction)
        {
            easeData.cachedRelationship = PawnRelationship.Player;
            return easeData.cachedRelationship;
        }

        // 实际敌对检测（含阵营敌对、精神状态、越狱、奴隶叛乱等）
        if (GenHostility.HostileTo(thing, playerFaction))
        {
            easeData.cachedRelationship = PawnRelationship.Enemy;
            return easeData.cachedRelationship;
        }

        // 非敌对，检查外交关系
        if (faction != null)
        {
            var relationKind = faction.RelationWith(playerFaction, true).kind;
            if (relationKind == FactionRelationKind.Ally)
            {
                easeData.cachedRelationship = PawnRelationship.Ally;
                return easeData.cachedRelationship;
            }
        }

        easeData.cachedRelationship = PawnRelationship.Neutral;
        return easeData.cachedRelationship;
    }

    #endregion

    #region 数值缓动更新

    private static void UpdateEasedValues(EaseData data, float healthTarget, float shieldTarget, float painTarget, float bloodLoss)
    {
        if (data.lastUpdateFrame == _currentFrame)
            return;

        // 首次初始化：将 easedValue / shieldEasedValue / painEasedValue 直接设为当前目标值，
        // 避免从默认值 1f 开始缓动导致的视觉跳跃
        if (data.lastUpdateFrame == -1)
        {
            data.easedValue = healthTarget;
            data.startValue = healthTarget;
            data.currentTarget = healthTarget;
            data.elapsedTime = 0f;
            data.shieldEasedValue = shieldTarget;
            data.shieldStartValue = shieldTarget;
            data.shieldCurrentTarget = shieldTarget;
            data.shieldElapsedTime = 0f;
            data.painEasedValue = painTarget;
            data.painStartValue = painTarget;
            data.painCurrentTarget = painTarget;
            data.painElapsedTime = 0f;
            // 隐藏计时以当前值作为基准，避免首帧误判数值变化
            data.shieldHide = new HideTimer(shieldTarget);
            data.bloodHide = new HideTimer(bloodLoss);
            data.painHide = new HideTimer(painTarget);
            data.healthHide = new HideTimer(healthTarget);
            data.lastUpdateFrame = _currentFrame;
            return;
        }

        float deltaTime = Time.deltaTime; // 每帧只取一次，隐藏计时与缓动共用
        // 关闭缓动，或单位上一帧未被绘制（不在画面内/被剔除/受缩放距离限制）：
        // 期间数值变化不可见，直接吸附到当前目标值，避免移入画面时才补播缓动动画
        if (!SimpleHealthBarSettings.enableEaseEffect || _currentFrame - data.lastUpdateFrame > 1)
        {
            data.easedValue = healthTarget;
            data.startValue = healthTarget;
            data.currentTarget = healthTarget;
            data.elapsedTime = 0f;
            data.shieldEasedValue = shieldTarget;
            data.shieldStartValue = shieldTarget;
            data.shieldCurrentTarget = shieldTarget;
            data.shieldElapsedTime = 0f;
            data.painEasedValue = painTarget;
            data.painStartValue = painTarget;
            data.painCurrentTarget = painTarget;
            data.painElapsedTime = 0f;
        }
        else
        {
            DoEase(deltaTime, ref data.easedValue, ref data.startValue, ref data.currentTarget, ref data.elapsedTime, healthTarget, CurrentEaseSpeed);
            DoEase(deltaTime, ref data.shieldEasedValue, ref data.shieldStartValue, ref data.shieldCurrentTarget, ref data.shieldElapsedTime, shieldTarget, CurrentShieldEaseSpeed);
            DoEase(deltaTime, ref data.painEasedValue, ref data.painStartValue, ref data.painCurrentTarget, ref data.painElapsedTime, painTarget, CurrentPainEaseSpeed);
        }

        // 不变后隐藏计时：数值不变且不缓动达到设定时间后隐藏对应附属条
        data.shieldHide.TickEased(shieldTarget, data.shieldEasedValue, SimpleHealthBarSettings.shieldBarHideDelay, deltaTime);
        data.painHide.TickEased(painTarget, data.painEasedValue, SimpleHealthBarSettings.painBarHideDelay, deltaTime);
        data.bloodHide.TickStable(bloodLoss, SimpleHealthBarSettings.bloodBarHideDelay, deltaTime);
        data.healthHide.TickEased(healthTarget, data.easedValue, SimpleHealthBarSettings.healthBarHideDelay, deltaTime);

        data.lastUpdateFrame = _currentFrame;
    }

    /// <summary>
    /// 不变后隐藏计时器：数值保持不变（且缓动已收敛）持续达到延迟时间后标记隐藏。
    /// delaySeconds &lt;= 0 表示禁用（始终不隐藏）。
    /// </summary>
    private struct HideTimer
    {
        public float lastTarget;
        public float stableSeconds;
        public bool hidden;
        /// <summary>绘制透明度（数值不变自动隐藏淡出用，1 = 完全可见）。</summary>
        public float alpha;

        public HideTimer(float target)
        {
            lastTarget = target;
            stableSeconds = 0f;
            hidden = false;
            alpha = 1f;
        }

        /// <summary>带缓动条（健康/耐痛/护盾）：目标值不变且缓动收敛才累积计时</summary>
        public void TickEased(float target, float easedValue, float delaySeconds, float deltaTime)
        {
            float targetDelta = target - lastTarget;
            float easeDelta = easedValue - target;
            bool stable = (targetDelta >= 0f ? targetDelta : -targetDelta) < 0.0001f
                && (easeDelta >= 0f ? easeDelta : -easeDelta) < 0.002f;
            lastTarget = target;
            Tick(stable, delaySeconds, deltaTime);
        }

        /// <summary>无缓动条（血液）：仅失血值不变才累积计时</summary>
        public void TickStable(float target, float delaySeconds, float deltaTime)
        {
            float targetDelta = target - lastTarget;
            bool stable = (targetDelta >= 0f ? targetDelta : -targetDelta) < 0.0001f;
            lastTarget = target;
            Tick(stable, delaySeconds, deltaTime);
        }

        /// <summary>
        /// 稳定计时：超过 delay 后进入淡出（alpha 1→0），淡出结束才标记 hidden。
        /// hidden 仅在淡出完成后为 true，因此淡出期间条仍参与可见性判定与堆叠布局，不会瞬间跳位。
        /// </summary>
        private void Tick(bool stable, float delaySeconds, float deltaTime)
        {
            if (delaySeconds <= 0f || !stable)
            {
                stableSeconds = 0f;
                hidden = false;
                alpha = 1f;
                return;
            }

            stableSeconds += deltaTime;
            if (stableSeconds < delaySeconds)
                return; // 延迟等待期内保持完全可见

            // 已超过延迟：淡出（关闭淡出效果时直接隐藏）
            if (!SimpleHealthBarSettings.enableHideFadeEffect || SimpleHealthBarSettings.hideFadeTime <= 0f)
            {
                alpha = 0f;
                hidden = true;
                return;
            }

            alpha -= deltaTime / SimpleHealthBarSettings.hideFadeTime;
            if (alpha <= 0f)
            {
                alpha = 0f;
                hidden = true;
            }
        }
    }

    #endregion

    #region 血条显示判定

    private static bool ShouldShowHealthBar(Pawn pawn, out PawnRelationship relationship, out EaseData easeData)
    {
        bool isAnimal = pawn.RaceProps.Animal;
        bool isMech = pawn.RaceProps.IsMechanoid;
        relationship = GetRelationship(pawn, out easeData);

        // 对隐形单位隐藏健康条：IsHiddenFromPlayer 每60帧缓存一次
        if (SimpleHealthBarSettings.hideHealthBarForInvisible)
        {
            RefreshHiddenCache(pawn, easeData);
            if (easeData.cachedHiddenFromPlayer)
                return false;
        }

        if (isAnimal)
        {
            return relationship switch
            {
                PawnRelationship.Player => SimpleHealthBarSettings.showAnimalPlayer,
                PawnRelationship.Ally => SimpleHealthBarSettings.showAnimalAlly,
                PawnRelationship.Enemy => SimpleHealthBarSettings.showAnimalEnemy,
                _ => SimpleHealthBarSettings.showAnimalNeutral,
            };
        }
        if (isMech)
        {
            return relationship switch
            {
                PawnRelationship.Player => SimpleHealthBarSettings.showMechPlayer,
                PawnRelationship.Ally => SimpleHealthBarSettings.showMechAlly,
                PawnRelationship.Enemy => SimpleHealthBarSettings.showMechEnemy,
                _ => SimpleHealthBarSettings.showMechNeutral,
            };
        }
        return relationship switch
        {
            PawnRelationship.Player => SimpleHealthBarSettings.showOtherPlayer,
            PawnRelationship.Ally => SimpleHealthBarSettings.showOtherAlly,
            PawnRelationship.Enemy => SimpleHealthBarSettings.showOtherEnemy,
            _ => SimpleHealthBarSettings.showOtherNeutral,
        };
    }

    private static bool ShouldShowTurretHealthBar(ThingWithComps thing, out PawnRelationship relationship, out EaseData easeData)
    {
        relationship = GetRelationship(thing, out easeData);
        return relationship switch
        {
            PawnRelationship.Player => SimpleHealthBarSettings.showTurretPlayer,
            PawnRelationship.Ally => SimpleHealthBarSettings.showTurretAlly,
            PawnRelationship.Enemy => SimpleHealthBarSettings.showTurretEnemy,
            _ => SimpleHealthBarSettings.showTurretNeutral,
        };
    }

    #endregion

    #region 通用辅助方法

    /// <summary>
    /// 安全地获取单位的生命系数（HealthScale）
    /// 计算公式：ageTracker.CurLifeStage.healthScaleFactor * RaceProps.baseHealthScale
    /// </summary>
    public static float GetHealthScaleSafe(Pawn? pawn)
    {
        if (pawn?.ageTracker?.CurLifeStage is LifeStageDef ls && pawn.RaceProps is RaceProperties r)
            return ls.healthScaleFactor * r.baseHealthScale;
        return 1f;
    }

    private static void DoEase(float deltaTime, ref float easedValue, ref float startValue, ref float currentTarget, ref float elapsedTime, float target, float speed)
    {
        target = target < 0f ? 0f : (target > 1f ? 1f : target);

        float delta = currentTarget - target;
        if (delta >= 0f ? delta > 1e-6f : -delta > 1e-6f)
        {
            // 如果 easedValue 距新 target 已足够近（<0.2%），直接 snap 避免小幅度波动导致缓动反复重启
            float valDelta = easedValue - target;
            if ((valDelta >= 0f ? valDelta : -valDelta) < 0.002f)
            {
                easedValue = target;
                startValue = target;
                currentTarget = target;
                elapsedTime = 0f;
                return;
            }

            startValue = easedValue;
            currentTarget = target;
            elapsedTime = 0f;
        }

        float totalDelta = currentTarget - startValue;
        if ((totalDelta >= 0f ? totalDelta : -totalDelta) < 1e-6f)
        {
            easedValue = currentTarget;
            return;
        }

        elapsedTime += deltaTime;
        float absTotalDelta = totalDelta >= 0f ? totalDelta : -totalDelta;
        float duration = absTotalDelta / speed;
        if (elapsedTime >= duration)
        {
            easedValue = currentTarget;
            startValue = currentTarget;
            return;
        }

        float t = elapsedTime / duration;
        if (SimpleHealthBarSettings.easingStyle == SimpleHealthBarSettings.EasingStyle.EaseIn)
            t = t * t;
        else if (SimpleHealthBarSettings.easingStyle == SimpleHealthBarSettings.EasingStyle.EaseOut)
            t = 1f - (1f - t) * (1f - t);
        // Linear: t 保持不变

        // inline Mathf.Lerp
        easedValue = startValue + (currentTarget - startValue) * t;
    }

    #endregion

    #region Mod 护盾反射兼容

    #region Axolotl 护盾（Axolotl.CompAxolotlEnergy）

    /// <summary>Axolotl CompAxolotlEnergy 类型（反射缓存）</summary>
    internal static Type? axolotlShieldType;
    /// <summary>Axolotl 护盾组件 getter</summary>
    private static Func<object, float>? axolotlShieldGetter;
    private static Func<object, int>? axolotlShieldMaxGetter;

    /// <summary>标记 Axolotl 程序集存在但反射初始化失败，避免重复 Log.Error</summary>
    private static bool axolotlReflectionFailed;

    /// <summary>
    /// 初始化 Axolotl 护盾反射。未安装 Axolotl 时静默返回 false。
    /// </summary>
    internal static bool TryInitAxolotlShieldReflection()
    {
        if (axolotlShieldType != null)
            return true;
        if (axolotlReflectionFailed)
            return false;

        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.GetName().Name == "Axolotl")
            {
                axolotlShieldType = asm.GetType("Axolotl.CompAxolotlEnergy");
                break;
            }
        }
        if (axolotlShieldType == null)
            return false;

        var shieldProp = axolotlShieldType.GetProperty("Shield", BindingFlags.Instance | BindingFlags.Public);
        var shieldMaxProp = axolotlShieldType.GetProperty("ShieldMax", BindingFlags.Instance | BindingFlags.Public);
        if (shieldProp == null || shieldMaxProp == null)
        {
            Log.Error("[HealthBar] 检测到 Axolotl 程序集但反射获取 CompAxolotlEnergy 属性失败。");
            goto Fail;
        }

        var param = Expression.Parameter(typeof(object), "comp");
        var cast = Expression.Convert(param, axolotlShieldType);

        // 委托编译可能因属性类型与委托签名不匹配而抛异常，捕获后按失败处理
        try
        {
            axolotlShieldGetter = Expression.Lambda<Func<object, float>>(
                Expression.Convert(Expression.Property(cast, shieldProp), typeof(float)), param).Compile();

            axolotlShieldMaxGetter = Expression.Lambda<Func<object, int>>(
                Expression.Property(cast, shieldMaxProp), param).Compile();
        }
        catch (Exception e)
        {
            Log.Error("[HealthBar] Axolotl 护盾反射委托编译失败，护盾条将不显示：" + e);
            goto Fail;
        }

        return true;

    Fail:
        axolotlReflectionFailed = true;
        axolotlShieldType = null;
        return false;
    }

    /// <summary>
    /// 从 ThingWithComps 的身体组件中获取第一个 Axolotl 护盾组件（仅身体，不含服装）。
    /// 与 ProjectileInterceptor 机制相同。
    /// </summary>
    internal static object? GetAxolotlShieldComp(ThingWithComps thing)
    {
        if (!TryInitAxolotlShieldReflection())
            return null;
        var allComps = thing.AllComps;
        for (int i = 0; i < allComps.Count; i++)
        {
            if (axolotlShieldType!.IsInstanceOfType(allComps[i]))
                return allComps[i];
        }
        return null;
    }

    /// <summary>
    /// 尝试获取单个 Axolotl 护盾组件的当前能量和最大能量。
    /// </summary>
    /// <returns>护盾激活且有能量上限时返回 true。</returns>
    internal static bool TryGetAxolotlShieldEnergy(object? shieldComp, out float current, out float max)
    {
        current = 0f;
        max = 0f;
        if (shieldComp == null)
            return false;
        current = axolotlShieldGetter?.Invoke(shieldComp) ?? 0f;
        max = axolotlShieldMaxGetter?.Invoke(shieldComp) ?? 0;
        return max > 0f && current > 0f;
    }

    #endregion

    #region VFE 护盾（VEF.Apparels.CompShieldBubble）

    /// <summary>VFE 护盾 CompShieldBubble 类型（反射缓存）</summary>
    internal static Type? vfeShieldType;

    // — 编译后的委托（替代 PropertyInfo.GetValue，热路径性能提升 10-50 倍）
    private static Func<object, float>? vfeShieldEnergyGetter;
    private static Func<object, float>? vfeShieldEnergyMaxGetter;
    private static Func<object, ShieldState>? vfeShieldStateGetter;

    /// <summary>标记 VEF 程序集存在但反射初始化失败，避免重复 Log.Error</summary>
    private static bool vefReflectionFailed;

    /// <summary>
    /// 初始化 VFE 护盾反射。未安装 VFE 时静默返回 false。
    /// </summary>
    internal static bool TryInitVfeShieldReflection()
    {
        if (vfeShieldType != null)
            return true;
        if (vefReflectionFailed)
            return false;

        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.GetName().Name == "VEF")
            {
                vfeShieldType = asm.GetType("VEF.Apparels.CompShieldBubble");
                break;
            }
        }
        if (vfeShieldType == null)
            return false;

        // 获取 PropertyInfo 并编译为委托，避免运行时反复 GetValue
        var energyProp = vfeShieldType.GetProperty("Energy", BindingFlags.Instance | BindingFlags.Public);
        var energyMaxProp = vfeShieldType.GetProperty("EnergyMax", BindingFlags.Instance | BindingFlags.Public);
        var stateProp = vfeShieldType.GetProperty("ShieldState", BindingFlags.Instance | BindingFlags.Public);
        if (energyProp == null || energyMaxProp == null || stateProp == null)
        {
            Log.Error("[HealthBar] 检测到 VEF 程序集但反射获取 CompShieldBubble 属性失败。");
            goto Fail;
        }

        var param = Expression.Parameter(typeof(object), "comp");
        var cast = Expression.Convert(param, vfeShieldType);

        vfeShieldEnergyGetter = Expression.Lambda<Func<object, float>>(
            Expression.Convert(Expression.Property(cast, energyProp), typeof(float)), param).Compile();

        vfeShieldEnergyMaxGetter = Expression.Lambda<Func<object, float>>(
            Expression.Convert(Expression.Property(cast, energyMaxProp), typeof(float)), param).Compile();

        // ShieldState 是 RimWorld.ShieldState 原版枚举，可直接转换，无需反射解析枚举值
        vfeShieldStateGetter = Expression.Lambda<Func<object, ShieldState>>(
            Expression.Convert(Expression.Property(cast, stateProp), typeof(ShieldState)), param).Compile();

        return true;

    Fail:
        vefReflectionFailed = true;
        vfeShieldType = null;
        return false;
    }

    /// <summary>
    /// 检查一个 ThingWithComps 是否包含 VFE 护盾组件。
    /// </summary>
    internal static bool HasVfeShield(ThingWithComps thing)
    {
        if (!TryInitVfeShieldReflection())
            return false;
        var allComps = thing.AllComps;
        for (int i = 0; i < allComps.Count; i++)
        {
            if (vfeShieldType!.IsInstanceOfType(allComps[i]))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 获取 VFE 护盾组件的当前能量比例（0~1）。未激活或无护盾时返回 0。
    /// </summary>
    internal static float GetVfeShieldFillPercent(object shieldComp)
    {
        if (shieldComp == null)
            return 0f;
        if (vfeShieldStateGetter?.Invoke(shieldComp) != ShieldState.Active)
            return 0f;
        float energy = vfeShieldEnergyGetter?.Invoke(shieldComp) ?? 0f;
        float energyMax = vfeShieldEnergyMaxGetter?.Invoke(shieldComp) ?? 0f;
        if (energyMax <= 0f)
            return 0f;
        float ratio = energy / energyMax;
        return ratio < 0f ? 0f : (ratio > 1f ? 1f : ratio);
    }

    /// <summary>
    /// 从 ThingWithComps 中收集所有 VFE 护盾组件到 result 列表。
    /// 未来增加其它 Mod 护盾兼容时，在此方法中追加收集逻辑。
    /// </summary>
    internal static void CollectVfeShieldComps(ThingWithComps thing, List<object> result)
    {
        if (!TryInitVfeShieldReflection())
            return;
        var allComps = thing.AllComps;
        for (int i = 0; i < allComps.Count; i++)
        {
            if (vfeShieldType!.IsInstanceOfType(allComps[i]))
                result.Add(allComps[i]);
        }
    }

    /// <summary>
    /// 尝试获取单个 VFE 护盾组件的当前能量和最大能量。
    /// </summary>
    /// <returns>护盾激活且有能量上限时返回 true。</returns>
    internal static bool TryGetVfeShieldEnergy(object shieldComp, out float current, out float max)
    {
        current = 0f;
        max = 0f;
        if (shieldComp == null)
            return false;
        if (vfeShieldStateGetter?.Invoke(shieldComp) != ShieldState.Active)
            return false;
        current = vfeShieldEnergyGetter?.Invoke(shieldComp) ?? 0f;
        max = vfeShieldEnergyMaxGetter?.Invoke(shieldComp) ?? 0f;
        return max > 0f;
    }

    #endregion

    #region Exosuit 外骨骼护盾（Exosuit.Exosuit_Core）

    /// <summary>Exosuit_Core 类型（反射缓存）</summary>
    internal static Type? exosuitCoreType;

    private static Func<object, float>? exosuitHealthGetter;
    private static Func<object, float>? exosuitHealthMaxGetter;

    /// <summary>标记 Exosuit 程序集存在但反射初始化失败</summary>
    private static bool exosuitReflectionFailed;

    /// <summary>
    /// 初始化 Exosuit 反射。未安装 Exosuit 时静默返回 false。
    /// </summary>
    internal static bool TryInitExosuitReflection()
    {
        if (exosuitCoreType != null)
            return true;
        if (exosuitReflectionFailed)
            return false;

        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.GetName().Name == "Exosuit")
            {
                exosuitCoreType = asm.GetType("Exosuit.Exosuit_Core");
                break;
            }
        }
        if (exosuitCoreType == null)
            return false;

        var healthProp = exosuitCoreType.GetProperty("Health", BindingFlags.Instance | BindingFlags.Public);
        var healthMaxProp = exosuitCoreType.GetProperty("HealthMax", BindingFlags.Instance | BindingFlags.Public);
        if (healthProp == null || healthMaxProp == null)
        {
            Log.Error("[HealthBar] 检测到 Exosuit 程序集但反射获取 Exosuit_Core 属性失败。");
            goto Fail;
        }

        var param = Expression.Parameter(typeof(object), "comp");
        var cast = Expression.Convert(param, exosuitCoreType);

        exosuitHealthGetter = Expression.Lambda<Func<object, float>>(
            Expression.Convert(Expression.Property(cast, healthProp), typeof(float)), param).Compile();

        exosuitHealthMaxGetter = Expression.Lambda<Func<object, float>>(
            Expression.Convert(Expression.Property(cast, healthMaxProp), typeof(float)), param).Compile();

        return true;

    Fail:
        exosuitReflectionFailed = true;
        exosuitCoreType = null;
        return false;
    }

    /// <summary>
    /// 从 pawn 的穿戴服装中查找 Exosuit_Core 实例（一个 pawn 最多一个）。
    /// </summary>
    internal static object? FindExosuitCoreOnPawn(Pawn pawn)
    {
        if (!TryInitExosuitReflection())
            return null;
        if (pawn.apparel == null)
            return null;
        var wornApparel = pawn.apparel.WornApparel;
        for (int i = 0; i < wornApparel.Count; i++)
        {
            var apparel = wornApparel[i];
            if (exosuitCoreType!.IsInstanceOfType(apparel))
                return apparel;
        }
        return null;
    }

    /// <summary>
    /// 尝试获取单个 Exosuit_Core 实例的当前 Health 和 HealthMax。
    /// </summary>
    internal static bool TryGetExosuitHealth(object? core, out float current, out float max)
    {
        current = 0f;
        max = 0f;
        if (core == null)
            return false;
        current = exosuitHealthGetter?.Invoke(core) ?? 0f;
        max = exosuitHealthMaxGetter?.Invoke(core) ?? 0f;
        return max > 0f;
    }

    #endregion

    #endregion

    #region 护盾/流血/疼痛状态获取

    private static float GetTurretShieldFillPercent(ThingWithComps thing, EaseData easeData)
    {
        if (_currentFrame - easeData.lastShieldCacheFrame >= 180)
        {
            easeData.cachedInterceptor = thing.TryGetComp<CompProjectileInterceptor>();
            easeData.lastShieldCacheFrame = _currentFrame;
        }

        var interceptor = easeData.cachedInterceptor;
        if (interceptor == null || !interceptor.Active)
            return 0f;

        int maxHP = interceptor.HitPointsMax;
        if (maxHP <= 0)
            return GetInterceptorTimeFraction(interceptor);

        int curHP = interceptor.currentHitPoints;
        if (curHP < 0) curHP = maxHP;
        float ratio = (float)curHP / maxHP;
        return ratio < 0f ? 0f : (ratio > 1f ? 1f : ratio);
    }

    private static float GetShieldFillPercent(Pawn pawn, EaseData easeData)
    {
        float totalCurrent = 0f;
        float totalMax = 0f;

        if (_currentFrame - easeData.lastShieldCacheFrame >= ShieldRefreshFrames)
            RefreshShieldCache(pawn, easeData);

        var interceptor = easeData.cachedInterceptor;
        if (interceptor != null && interceptor.Active)
        {
            int maxHP = interceptor.HitPointsMax;
            if (maxHP <= 0)
                return GetInterceptorTimeFraction(interceptor);
            int curHP = interceptor.currentHitPoints;
            if (curHP < 0) curHP = maxHP;
            totalCurrent += curHP;
            totalMax += maxHP;
        }

        // Axolotl 护盾（反射读取，仅从身体组件获取）
        if (TryGetAxolotlShieldEnergy(easeData.cachedAxolotlShield, out float axCur, out float axMax))
        {
            totalCurrent += axCur;
            totalMax += axMax;
        }

        for (int i = 0; i < easeData.cachedShieldComps.Count; i++)
        {
            var shield = easeData.cachedShieldComps[i];
            if (shield == null || shield.parent == null || shield.parent.Destroyed)
                continue;
            if (shield.ShieldState == ShieldState.Active)
            {
                totalCurrent += shield.Energy;
                totalMax += shield.parent.GetStatValue(StatDefOf.EnergyShieldEnergyMax);
            }
        }

        // VFE 护盾（反射读取）
        for (int i = 0; i < easeData.cachedVfeShields.Count; i++)
        {
            if (TryGetVfeShieldEnergy(easeData.cachedVfeShields[i], out float cur, out float max))
            {
                totalCurrent += cur;
                totalMax += max;
            }
        }

        // Exosuit 外骨骼护盾（反射读取，一个 pawn 最多一个）
        if (TryGetExosuitHealth(easeData.cachedExosuitCore, out float exCur, out float exMax))
        {
            totalCurrent += exCur;
            totalMax += exMax;
        }

        if (totalMax <= 0f)
            return 0f;
        float ratio = totalCurrent / totalMax;
        return ratio < 0f ? 0f : (ratio > 1f ? 1f : ratio);
    }

    private static void RefreshShieldCache(Pawn pawn, EaseData easeData)
    {
        // 服装数量未变则跳过刷新（仍更新帧计数，避免每帧都走此判断）
        int apparelCount = pawn.apparel?.WornApparel.Count ?? 0;
        if (apparelCount == easeData.cachedApparelCount)
        {
            easeData.lastShieldCacheFrame = _currentFrame;
            return;
        }
        easeData.cachedApparelCount = apparelCount;

        easeData.cachedShieldComps.Clear();
        easeData.cachedVfeShields.Clear();
        easeData.cachedExosuitCore = null;
        easeData.cachedAxolotlShield = null;
        easeData.cachedInterceptor = pawn.GetComp<CompProjectileInterceptor>();

        // 检测 Pawn 自身的 Axolotl 护盾组件（仅身体组件）
        easeData.cachedAxolotlShield = GetAxolotlShieldComp(pawn);


        // 检测 Pawn 自身的内置 VFE 护盾
        CollectVfeShieldComps(pawn, easeData.cachedVfeShields);

        if (pawn.apparel != null)
        {
            var wornApparel = pawn.apparel.WornApparel;
            for (int i = 0; i < wornApparel.Count; i++)
            {
                var apparel = wornApparel[i];
                // 单次遍历 AllComps，同时检测原版 CompShield、VFE 护盾
                var apparelComps = apparel.AllComps;
                for (int j = 0; j < apparelComps.Count; j++)
                {
                    var comp = apparelComps[j];
                    if (comp is CompShield cs)
                        easeData.cachedShieldComps.Add(cs);
                    else if (vfeShieldType != null && vfeShieldType.IsInstanceOfType(comp))
                        easeData.cachedVfeShields.Add(comp);
                }
            }
        }
        // 收集 Exosuit 外骨骼核心（一个 pawn 最多一个）
        easeData.cachedExosuitCore = FindExosuitCoreOnPawn(pawn);
        easeData.lastShieldCacheFrame = _currentFrame;
    }

    private static float GetInterceptorTimeFraction(CompProjectileInterceptor interceptor)
    {
        var props = interceptor.Props;
        int ticksGame = Find.TickManager.TicksGame;

        if (props.activated)
        {
            int activatedTick = (int)ActivatedTickField.GetValue(interceptor);
            int remaining = activatedTick + props.activeDuration - ticksGame;
            if (remaining <= 0 || props.activeDuration <= 0) return 0f;
            float ratio = (float)remaining / props.activeDuration;
            return ratio < 0f ? 0f : (ratio > 1f ? 1f : ratio);
        }

        if (props.chargeIntervalTicks > 0)
        {
            int remaining = interceptor.ChargeCycleStartTick - ticksGame;
            int activeDuration = props.chargeIntervalTicks - props.chargeDurationTicks;
            if (remaining <= 0 || activeDuration <= 0) return 0f;
            float ratio = (float)remaining / activeDuration;
            return ratio < 0f ? 0f : (ratio > 1f ? 1f : ratio);
        }

        // 被动常开拦截器且无 HP 限制 → 永久满额，显示护盾条无意义
        if (interceptor.HitPointsMax <= 0)
            return 0f;

        return 1f;
    }

    /// <summary>
    /// 刷新血液缓存（每20刻更新一次）。
    /// </summary>
    private static void RefreshBloodCache(Pawn pawn, EaseData easeData)
    {
        if (_currentFrame - easeData.lastBloodCacheFrame < 20)
            return;
        easeData.lastBloodCacheFrame = _currentFrame;

        var hediffSet = pawn.health.hediffSet;
        var bloodLossHediff = hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
        float severity = bloodLossHediff != null ? bloodLossHediff.Severity : 0f;
        easeData.cachedBloodLoss = severity < 0f ? 0f : (severity > 1f ? 1f : severity);
        easeData.cachedBleedRate = hediffSet.BleedRateTotal;
    }

    /// <summary>
    /// 刷新疼痛休克阈值缓存（每120帧更新一次）。
    /// </summary>
    private static void RefreshPainCache(Pawn pawn, EaseData easeData)
    {
        if (_currentFrame - easeData.lastPainCacheFrame < 120)
            return;
        easeData.lastPainCacheFrame = _currentFrame;
        easeData.cachedPainShockThreshold = pawn.GetStatValue(StatDefOf.PainShockThreshold);
    }

    /// <summary>
    /// 刷新"对玩家隐藏"缓存（每60帧更新一次）。
    /// </summary>
    private static void RefreshHiddenCache(Pawn pawn, EaseData easeData)
    {
        if (_currentFrame - easeData.lastHiddenCacheFrame < 60)
            return;
        easeData.lastHiddenCacheFrame = _currentFrame;
        easeData.cachedHiddenFromPlayer = pawn.IsHiddenFromPlayer();
    }

    #endregion
}
