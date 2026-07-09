using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ASQHPBar;

public class SimpleHealthBarSettings : ModSettings
{
    public enum EasingStyle
    {
        Linear,
        EaseIn,
        EaseOut
    }

    /// <summary>
    /// 死亡血条消失方式：Fade = 透明度渐降；Shrink = 宽度收缩。
    /// </summary>
    public enum DeathDisappearStyle
    {
        Fade,
        Shrink
    }

    /// <summary>
    /// 附属条类型（新增类型时在此添加枚举值，并在 HealthBarEaseHelper 的条注册表中登记）。
    /// </summary>
    public enum SubBarKind
    {
        Pain,
        Blood,
        Shield
    }

    /// <summary>
    /// 设置界面的分页选项卡。
    /// </summary>
    public enum SettingsPage
    {
        General,
        AppearanceColors,
        SubBars,
        Filters
    }

    public static bool enableImprovedHealthBar = true;
    public static bool enableImprovedCalculation = true;
    public static bool enableCalcBleed = true;
    public static bool enableCalcBrain = true;
    public static bool enableCalcMovement = true;
    public static bool enableCalcHeart = true;
    public static bool enableCalcCore = true;
    public static bool enableCalcMissingParts = true;
    public static bool enableCalcInjury = true;
    /// <summary>计算永久伤口因素：关闭时所有伤害计算无视永久伤口（IsPermanent）。</summary>
    public static bool enableCalcPermanent = false;

    public static int healthBarMinWidth = 40;
    public static int healthScaleWidthMultiplier = 0;
    public static int healthBarMaxWidth = 0; // 最大健康条宽度上限（0=不限制）

    /// <summary>
    /// 实际生效的最大健康条宽度上限（0 = 不限制），恒不低于 healthBarMinWidth。
    /// </summary>
    public static int EffectiveMaxBarWidth => healthBarMaxWidth <= 0 ? 0 : Mathf.Max(healthBarMinWidth, healthBarMaxWidth);
    public static float easeSpeed = 0.1f;
    public static float healthBarHideDelay = 0f; // 健康条不变后隐藏时间（秒，0=禁用）
    public static float shieldEaseSpeed = 0.3f;
    public static int noNameHealthBarHeight = 8;
    public static bool enableBarGradient = true;
    public static int injuryLethalThreshold = 150;
    public static bool enableVerboseLogging = false;
    public static bool enableStableLabel = true;
    public static bool enableCustomBg = true;
    public static bool enableHealthBarOffset = false;
    public static bool enableCompatibilityMode = false;
    public static EasingStyle easingStyle = EasingStyle.Linear;
    public static int gradWidth = 6;
    public static float bgGradRatio = 0.15f;
    public static int healthBarHeight = 8;
    public static int turretHealthBarHeight = 8;
    public static float turretHealthBarWidthMultiplier = 20f;
    public static bool enableTurretHealthBar = false;
    public static bool enableShieldBar = false;
    public static int shieldBarHeight = 6;
    public static float shieldBarHideDelay = 5f; // 护盾条不变后隐藏时间（秒，0=禁用）
    public static bool enableZoomScale = true;
    public static bool enableBatchRendering = false;
    public static bool enableEaseEffect = true;
    public static int bgBorderSize = 1;
    public static int barSpacing = 0;

    // 死亡后血条保留动画
    public static bool enableDeathBar = true;
    public static float deathBarEaseSpeedMultiplier = 5f; // 死亡缓动 = easeSpeed × 该倍率
    public static float deathBarFadeTime = 0.5f;          // 消失效果持续时间（秒）
    public static DeathDisappearStyle deathBarDisappearStyle = DeathDisappearStyle.Fade;

    // 数值不变自动隐藏时的淡出效果
    public static bool enableHideFadeEffect = true;
    public static float hideFadeTime = 0.5f;              // 自动隐藏淡出时长（秒）

    // 附属条（耐痛/血液/护盾）显示顺序（越靠前越贴近健康条，翻转布局时方向相反）
    public static List<SubBarKind> subBarOrder = new() { SubBarKind.Pain, SubBarKind.Blood, SubBarKind.Shield };
    public static int noNameYOffset = 0;
    public static bool noNamePositionAbove = false;
    public static CameraZoomRange noNameMaxZoom = CameraZoomRange.Middle;

    // 血液条设置
    public static bool enableBloodBar = false;
    public static bool bloodBarAttachedMode = true;
    public static int bloodBarHeight = 3;
    public static float bloodBleedFactor = 1f;
    public static float bloodBarHideDelay = 0f; // 流血条不变后隐藏时间（秒，0=禁用）
    public static bool thinBloodMarker = false; // 附加模式血液标记宽度缩至1px

    // 耐痛条设置
    public static bool enablePainBar = false;
    public static int painBarHeight = 3;
    public static bool invertPainBar = true;
    public static float painEaseSpeed = 0.1f;
    public static float painBarHideDelay = 5f; // 耐痛条不变后隐藏时间（秒，0=禁用）

    // 自爆阈值标记设置
    public static bool enableExplosiveThreshold = true;
    public static bool thinExplosiveMarker = false; // 自爆阈值标记宽度缩至1px

    // 有标签时附属条宽度设置
    public static int subBarWidth = 40;
    public static bool subBarSyncWithLabel = true;

    // 单位类型 × 势力 过滤开关 (3×4)
    public static bool showAnimalPlayer = true;
    public static bool showAnimalAlly = true;
    public static bool showAnimalEnemy = true;
    public static bool showAnimalNeutral = true;
    public static bool showMechPlayer = true;
    public static bool showMechAlly = true;
    public static bool showMechEnemy = true;
    public static bool showMechNeutral = true;
    public static bool showOtherPlayer = true;
    public static bool showOtherAlly = true;
    public static bool showOtherEnemy = true;
    public static bool showOtherNeutral = true;
    public static bool showTurretPlayer = true;
    public static bool showTurretAlly = true;
    public static bool showTurretEnemy = true;
    public static bool showTurretNeutral = true;

    // 对隐形单位隐藏健康条（IsHiddenFromPlayer）
    public static bool hideHealthBarForInvisible = true;

    // 设置界面分页：当前选项卡、每页独立滚动位置与内容高度
    private static SettingsPage currentPage = SettingsPage.General;
    private static readonly Vector2[] pageScroll = new Vector2[4];
    private static readonly float[] pageHeight = { 920f, 1600f, 1600f, 300f };

    // 默认颜色常量
    public const string DefaultColorActualStr = "0.7,0,0,0.5";
    public const string DefaultColorFriendStr = "0.4,0.7,1,0.5";
    public const string DefaultColorPlayerStr = "0.4,0.7,1,0.5";
    public const string DefaultColorNeutralStr = "0.7,0,0,0.5";
    public const string DefaultColorDamageStr = "1,1,0,0.5";
    public const string DefaultColorEnemyDamageStr = "1,1,0,0.5";
    public const string DefaultColorHealStr = "0,1,0,0.5";
    public const string DefaultColorBgStr = "0.08,0.08,0.08,0.5";
    public const string DefaultColorShieldStr = "0.6,0.6,0.6,0.5";
    public const string DefaultColorShieldDamageStr = "1,1,0,0.5";
    public const string DefaultColorShieldHealStr = "0,1,0,0.5";
    public const string DefaultColorBloodCurrentStr = "0.8,0.35,0.35,0.5";
    public const string DefaultColorBloodBleedStr = "0.9,0.7,0.7,0.5";
    public const string DefaultColorExplosiveThresholdStr = "1,1,0,0.4";
    public const string DefaultColorPainStr = "1,0.95,0.55,0.5";
    public const string DefaultColorPainPlusStr = "1,0.95,0.55,0.2";
    public const string DefaultColorPainMinusStr = "1,0.95,0.55,0.2";

    // 默认 Color 由对应 Str 解析生成，避免重复配置
    private static readonly Color DefaultColorActual = ParseColor(DefaultColorActualStr, Color.magenta);
    private static readonly Color DefaultColorFriend = ParseColor(DefaultColorFriendStr, Color.magenta);
    private static readonly Color DefaultColorPlayer = ParseColor(DefaultColorPlayerStr, Color.magenta);
    private static readonly Color DefaultColorNeutral = ParseColor(DefaultColorNeutralStr, Color.magenta);
    private static readonly Color DefaultColorDamage = ParseColor(DefaultColorDamageStr, Color.magenta);
    private static readonly Color DefaultColorEnemyDamage = ParseColor(DefaultColorEnemyDamageStr, Color.magenta);
    private static readonly Color DefaultColorHeal = ParseColor(DefaultColorHealStr, Color.magenta);
    private static readonly Color DefaultColorBg = ParseColor(DefaultColorBgStr, Color.magenta);
    private static readonly Color DefaultColorShield = ParseColor(DefaultColorShieldStr, Color.magenta);
    private static readonly Color DefaultColorShieldDamage = ParseColor(DefaultColorShieldDamageStr, Color.magenta);
    private static readonly Color DefaultColorShieldHeal = ParseColor(DefaultColorShieldHealStr, Color.magenta);
    private static readonly Color DefaultColorBloodCurrent = ParseColor(DefaultColorBloodCurrentStr, Color.magenta);
    private static readonly Color DefaultColorBloodBleed = ParseColor(DefaultColorBloodBleedStr, Color.magenta);
    private static readonly Color DefaultColorExplosiveThreshold = ParseColor(DefaultColorExplosiveThresholdStr, Color.magenta);
    private static readonly Color DefaultColorPain = ParseColor(DefaultColorPainStr, Color.magenta);
    private static readonly Color DefaultColorPainPlus = ParseColor(DefaultColorPainPlusStr, Color.magenta);
    private static readonly Color DefaultColorPainMinus = ParseColor(DefaultColorPainMinusStr, Color.magenta);

    // 颜色设置（以字符串存储 "R,G,B,A"）
    public static string colorActualStr = DefaultColorActualStr;
    public static string colorFriendStr = DefaultColorFriendStr;
    public static string colorPlayerStr = DefaultColorPlayerStr;
    public static string colorNeutralStr = DefaultColorNeutralStr;
    public static string colorDamageStr = DefaultColorDamageStr;
    public static string colorEnemyDamageStr = DefaultColorEnemyDamageStr;
    public static string colorHealStr = DefaultColorHealStr;
    public static string bgColorStr = DefaultColorBgStr;
    public static string colorShieldStr = DefaultColorShieldStr;
    public static string colorShieldDamageStr = DefaultColorShieldDamageStr;
    public static string colorShieldHealStr = DefaultColorShieldHealStr;
    public static string colorBloodCurrentStr = DefaultColorBloodCurrentStr;
    public static string colorBloodBleedStr = DefaultColorBloodBleedStr;
    public static string colorExplosiveThresholdStr = DefaultColorExplosiveThresholdStr;
    public static string colorPainStr = DefaultColorPainStr;
    public static string colorPainPlusStr = DefaultColorPainPlusStr;
    public static string colorPainMinusStr = DefaultColorPainMinusStr;

    public static Color ColorActual => ParseColor(colorActualStr, DefaultColorActual);
    public static Color ColorFriend => ParseColor(colorFriendStr, DefaultColorFriend);
    public static Color ColorPlayer => ParseColor(colorPlayerStr, DefaultColorPlayer);
    public static Color ColorNeutral => ParseColor(colorNeutralStr, DefaultColorNeutral);
    public static Color ColorDamage => ParseColor(colorDamageStr, DefaultColorDamage);
    public static Color ColorEnemyDamage => ParseColor(colorEnemyDamageStr, DefaultColorEnemyDamage);
    public static Color ColorHeal => ParseColor(colorHealStr, DefaultColorHeal);
    public static Color ColorBg => ParseColor(bgColorStr, DefaultColorBg);
    public static Color ColorShield => ParseColor(colorShieldStr, DefaultColorShield);
    public static Color ColorShieldDamage => ParseColor(colorShieldDamageStr, DefaultColorShieldDamage);
    public static Color ColorShieldHeal => ParseColor(colorShieldHealStr, DefaultColorShieldHeal);
    public static Color ColorBloodCurrent => ParseColor(colorBloodCurrentStr, DefaultColorBloodCurrent);
    public static Color ColorBloodBleed => ParseColor(colorBloodBleedStr, DefaultColorBloodBleed);
    public static Color ColorExplosiveThreshold => ParseColor(colorExplosiveThresholdStr, DefaultColorExplosiveThreshold);
    public static Color ColorPain => ParseColor(colorPainStr, DefaultColorPain);
    public static Color ColorPainPlus => ParseColor(colorPainPlusStr, DefaultColorPainPlus);
    public static Color ColorPainMinus => ParseColor(colorPainMinusStr, DefaultColorPainMinus);

    private static Color ParseColor(string str, Color defaultColor)
    {
        string[] parts = str.Split(',');
        if (parts.Length == 4 &&
            float.TryParse(parts[0], out float r) &&
            float.TryParse(parts[1], out float g) &&
            float.TryParse(parts[2], out float b) &&
            float.TryParse(parts[3], out float a))
        {
            return new Color(r, g, b, a);
        }
        return defaultColor;
    }

    private static string ColorToString(Color c)
    {
        return $"{c.r:F4},{c.g:F4},{c.b:F4},{c.a:F4}";
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref enableImprovedHealthBar, "enableImprovedHealthBar", true);
        Scribe_Values.Look(ref enableImprovedCalculation, "enableImprovedCalculation", true);
        Scribe_Values.Look(ref enableCalcBleed, "enableCalcBleed", true);
        Scribe_Values.Look(ref enableCalcBrain, "enableCalcBrain", true);
        Scribe_Values.Look(ref enableCalcMovement, "enableCalcMovement", true);
        Scribe_Values.Look(ref enableCalcHeart, "enableCalcHeart", true);
        Scribe_Values.Look(ref enableCalcCore, "enableCalcCore", true);
        Scribe_Values.Look(ref enableCalcMissingParts, "enableCalcMissingParts", true);
        Scribe_Values.Look(ref enableCalcInjury, "enableCalcInjury", true);
        Scribe_Values.Look(ref enableCalcPermanent, "enableCalcPermanent", false);
        Scribe_Values.Look(ref healthBarMinWidth, "healthBarWidth", 40);
        Scribe_Values.Look(ref healthScaleWidthMultiplier, "healthScaleWidthMultiplier", 0);
        Scribe_Values.Look(ref healthBarMaxWidth, "healthBarMaxWidth", 0);
        Scribe_Values.Look(ref easeSpeed, "easeSpeed", 0.1f);
        Scribe_Values.Look(ref healthBarHideDelay, "healthBarHideDelay", 0f);
        Scribe_Values.Look(ref shieldEaseSpeed, "shieldEaseSpeed", 0.3f);
        Scribe_Values.Look(ref shieldBarHideDelay, "shieldBarHideDelay", 5f);
        Scribe_Values.Look(ref noNameHealthBarHeight, "noNameHealthBarHeight", 8);
        Scribe_Values.Look(ref enableBarGradient, "enableBarGradient", true);
        Scribe_Values.Look(ref injuryLethalThreshold, "injuryLethalThreshold", 150);
        Scribe_Values.Look(ref enableVerboseLogging, "enableVerboseLogging", false);
        Scribe_Values.Look(ref enableStableLabel, "enableStableLabel", true);
        Scribe_Values.Look(ref enableCustomBg, "enableCustomBg", true);
        Scribe_Values.Look(ref enableHealthBarOffset, "enableHealthBarOffset", false);
        Scribe_Values.Look(ref enableCompatibilityMode, "enableCompatibilityMode", false);
        Scribe_Values.Look(ref easingStyle, "easingStyle", EasingStyle.Linear);
        Scribe_Values.Look(ref colorActualStr, "colorActualStr", DefaultColorActualStr);
        Scribe_Values.Look(ref colorFriendStr, "colorFriendStr", DefaultColorFriendStr);
        Scribe_Values.Look(ref colorPlayerStr, "colorPlayerStr", DefaultColorPlayerStr);
        Scribe_Values.Look(ref colorNeutralStr, "colorNeutralStr", DefaultColorNeutralStr);
        Scribe_Values.Look(ref colorDamageStr, "colorDamageStr", DefaultColorDamageStr);
        Scribe_Values.Look(ref colorHealStr, "colorHealStr", DefaultColorHealStr);
        Scribe_Values.Look(ref bgColorStr, "bgColorStr", DefaultColorBgStr);
        Scribe_Values.Look(ref gradWidth, "gradWidth", 6);
        Scribe_Values.Look(ref bgGradRatio, "bgGradRatio", 0.15f);
        Scribe_Values.Look(ref healthBarHeight, "healthBarHeight", 8);
        Scribe_Values.Look(ref turretHealthBarHeight, "turretHealthBarHeight", 8);
        Scribe_Values.Look(ref turretHealthBarWidthMultiplier, "turretHealthBarWidthMultiplier", 20f);
        Scribe_Values.Look(ref enableTurretHealthBar, "enableTurretHealthBar", false);
        Scribe_Values.Look(ref enableShieldBar, "enableShieldBar", false);
        Scribe_Values.Look(ref shieldBarHeight, "shieldBarHeight", 6);
        Scribe_Values.Look(ref enableZoomScale, "enableZoomScale", false);
        Scribe_Values.Look(ref enableBatchRendering, "enableBatchRendering", false);
        Scribe_Values.Look(ref enableEaseEffect, "enableEaseEffect", true);
        Scribe_Values.Look(ref bgBorderSize, "bgBorderSize", 1);
        Scribe_Values.Look(ref barSpacing, "barSpacing", 0);
        Scribe_Values.Look(ref enableDeathBar, "enableDeathBar", true);
        Scribe_Values.Look(ref deathBarEaseSpeedMultiplier, "deathBarEaseSpeedMultiplier", 5f);
        Scribe_Values.Look(ref deathBarFadeTime, "deathBarFadeTime", 0.5f);
        Scribe_Values.Look(ref deathBarDisappearStyle, "deathBarDisappearStyle", DeathDisappearStyle.Fade);
        Scribe_Values.Look(ref enableHideFadeEffect, "enableHideFadeEffect", true);
        Scribe_Values.Look(ref hideFadeTime, "hideFadeTime", 0.5f);
        Scribe_Collections.Look(ref subBarOrder, "subBarOrder", LookMode.Value);
        Scribe_Values.Look(ref noNameYOffset, "noNameYOffset", 0);
        Scribe_Values.Look(ref noNamePositionAbove, "noNamePositionAbove", false);
        Scribe_Values.Look(ref noNameMaxZoom, "noNameMaxZoom", CameraZoomRange.Middle);
        Scribe_Values.Look(ref colorShieldStr, "colorShieldStr", DefaultColorShieldStr);
        Scribe_Values.Look(ref colorShieldDamageStr, "colorShieldDamageStr", DefaultColorShieldDamageStr);
        Scribe_Values.Look(ref colorShieldHealStr, "colorShieldHealStr", DefaultColorShieldHealStr);
        Scribe_Values.Look(ref enableBloodBar, "enableBloodBar", false);
        Scribe_Values.Look(ref bloodBarAttachedMode, "bloodBarAttachedMode", true);
        Scribe_Values.Look(ref bloodBarHeight, "bloodBarHeight", 3);
        Scribe_Values.Look(ref bloodBleedFactor, "bloodBleedFactor", 1f);
        Scribe_Values.Look(ref bloodBarHideDelay, "bloodBarHideDelay", 0f);
        Scribe_Values.Look(ref thinBloodMarker, "thinBloodMarker", false);
        Scribe_Values.Look(ref colorBloodCurrentStr, "colorBloodCurrentStr", DefaultColorBloodCurrentStr);
        Scribe_Values.Look(ref colorBloodBleedStr, "colorBloodBleedStr", DefaultColorBloodBleedStr);
        Scribe_Values.Look(ref subBarWidth, "subBarWidth", 40);
        Scribe_Values.Look(ref subBarSyncWithLabel, "subBarSyncWithLabel", true);
        Scribe_Values.Look(ref enableExplosiveThreshold, "enableExplosiveThreshold", true);
        Scribe_Values.Look(ref thinExplosiveMarker, "thinExplosiveMarker", false);
        Scribe_Values.Look(ref colorExplosiveThresholdStr, "colorExplosiveThresholdStr", DefaultColorExplosiveThresholdStr);
        Scribe_Values.Look(ref enablePainBar, "enablePainBar", false);
        Scribe_Values.Look(ref painBarHeight, "painBarHeight", 3);
        Scribe_Values.Look(ref invertPainBar, "invertPainBar", true);
        Scribe_Values.Look(ref painEaseSpeed, "painEaseSpeed", 0.1f);
        Scribe_Values.Look(ref painBarHideDelay, "painBarHideDelay", 5f);
        Scribe_Values.Look(ref colorPainPlusStr, "colorPainPlusStr", DefaultColorPainPlusStr);
        Scribe_Values.Look(ref colorPainMinusStr, "colorPainMinusStr", DefaultColorPainMinusStr);
        Scribe_Values.Look(ref colorPainStr, "colorPainStr", DefaultColorPainStr);
        Scribe_Values.Look(ref showAnimalPlayer, "showAnimalPlayer", true);
        Scribe_Values.Look(ref showAnimalAlly, "showAnimalAlly", true);
        Scribe_Values.Look(ref showAnimalEnemy, "showAnimalEnemy", true);
        Scribe_Values.Look(ref showAnimalNeutral, "showAnimalNeutral", true);
        Scribe_Values.Look(ref showMechPlayer, "showMechPlayer", true);
        Scribe_Values.Look(ref showMechAlly, "showMechAlly", true);
        Scribe_Values.Look(ref showMechEnemy, "showMechEnemy", true);
        Scribe_Values.Look(ref showMechNeutral, "showMechNeutral", true);
        Scribe_Values.Look(ref showOtherPlayer, "showOtherPlayer", true);
        Scribe_Values.Look(ref showOtherAlly, "showOtherAlly", true);
        Scribe_Values.Look(ref showOtherEnemy, "showOtherEnemy", true);
        Scribe_Values.Look(ref showOtherNeutral, "showOtherNeutral", true);
        Scribe_Values.Look(ref showTurretPlayer, "showTurretPlayer", true);
        Scribe_Values.Look(ref showTurretAlly, "showTurretAlly", true);
        Scribe_Values.Look(ref showTurretEnemy, "showTurretEnemy", true);
        Scribe_Values.Look(ref showTurretNeutral, "showTurretNeutral", true);
        Scribe_Values.Look(ref hideHealthBarForInvisible, "hideHealthBarForInvisible", true);
    }

    public void DoSettingsWindowContents(Rect inRect)
    {
        List<TabRecord> tabs = new List<TabRecord>
        {
            new TabRecord("ASQHPBar_TabGeneral".Translate(), delegate { currentPage = SettingsPage.General; }, currentPage == SettingsPage.General),
            new TabRecord("ASQHPBar_TabAppearanceColors".Translate(), delegate { currentPage = SettingsPage.AppearanceColors; }, currentPage == SettingsPage.AppearanceColors),
            new TabRecord("ASQHPBar_TabSubBars".Translate(), delegate { currentPage = SettingsPage.SubBars; }, currentPage == SettingsPage.SubBars),
            new TabRecord("ASQHPBar_TabFilters".Translate(), delegate { currentPage = SettingsPage.Filters; }, currentPage == SettingsPage.Filters),
        };
        TabDrawer.DrawTabs(new Rect(inRect.x, inRect.y + TabDrawer.TabHeight, inRect.width, inRect.height - TabDrawer.TabHeight), tabs);

        Rect contentRect = new Rect(inRect.x, inRect.y + TabDrawer.TabHeight, inRect.width, inRect.height - TabDrawer.TabHeight);
        int pageIndex = (int)currentPage;
        float totalContentHeight = pageHeight[pageIndex];
        Rect viewRect = new Rect(0f, 0f, contentRect.width - 30f, totalContentHeight);
        Widgets.BeginScrollView(contentRect, ref pageScroll[pageIndex], viewRect);

        Listing_Standard list = new Listing_Standard();
        list.Begin(viewRect);

        switch (currentPage)
        {
            case SettingsPage.General:
                DrawGeneralPage(list);
                break;
            case SettingsPage.AppearanceColors:
                DrawAppearanceColorsPage(list);
                break;
            case SettingsPage.SubBars:
                DrawSubBarsPage(list);
                break;
            case SettingsPage.Filters:
                DrawFiltersPage(list);
                break;
        }

        list.End();
        Widgets.EndScrollView();
    }

    /// <summary>
    /// 通用页：改进健康值计算、稳定标签、主开关、调试日志。
    /// </summary>
    private void DrawGeneralPage(Listing_Standard list)
    {
        list.GapLine();
        // --- 改进健康值计算 ---
        list.CheckboxLabeled("ASQHPBar_EnableImprovedCalc".Translate(), ref enableImprovedCalculation);

        if (enableImprovedCalculation)
        {
            ToggleFactor(list, "ASQHPBar_EnableCalcInjury".Translate(), ref enableCalcInjury);
            if (enableCalcInjury)
            {
                injuryLethalThreshold = (int)list.SliderLabeled(
                    "ASQHPBar_InjuryLethalThreshold".Translate() + ": " + injuryLethalThreshold,
                    injuryLethalThreshold, 50f, 500f);
            }
            if (enableBloodBar)
            {
                GUI.color = Color.gray;
                bool dummy = false;
                list.CheckboxLabeled("ASQHPBar_EnableCalcBleed".Translate(), ref dummy);
                GUI.color = Color.white;
            }
            else
            {
                ToggleFactor(list, "ASQHPBar_EnableCalcBleed".Translate(), ref enableCalcBleed);
            }
            ToggleFactor(list, "ASQHPBar_EnableCalcBrain".Translate(), ref enableCalcBrain);
            ToggleFactor(list, "ASQHPBar_EnableCalcHeart".Translate(), ref enableCalcHeart);
            ToggleFactor(list, "ASQHPBar_EnableCalcMovement".Translate(), ref enableCalcMovement);
            ToggleFactor(list, "ASQHPBar_EnableCalcCore".Translate(), ref enableCalcCore);
            list.CheckboxLabeled("ASQHPBar_EnableCalcMissingParts".Translate(), ref enableCalcMissingParts);
            list.CheckboxLabeled("ASQHPBar_EnableCalcPermanent".Translate(), ref enableCalcPermanent);
        }
        list.GapLine();
        list.CheckboxLabeled("ASQHPBar_EnableStableLabel".Translate(), ref enableStableLabel);
        list.CheckboxLabeled("ASQHPBar_EnableImprovedHB".Translate(), ref enableImprovedHealthBar);

        list.GapLine();
        list.CheckboxLabeled("ASQHPBar_EnableDeathBar".Translate(), ref enableDeathBar);
        if (enableDeathBar)
        {
            deathBarEaseSpeedMultiplier = Mathf.Round(list.SliderLabeled(
                "ASQHPBar_DeathBarEaseSpeedMultiplier".Translate() + ": " + deathBarEaseSpeedMultiplier.ToStringPercent(),
                deathBarEaseSpeedMultiplier, 0.1f, 10f) * 10f) / 10f;
            deathBarFadeTime = Mathf.Round(list.SliderLabeled(
                "ASQHPBar_DeathBarFadeTime".Translate() + ": " + deathBarFadeTime.ToString("0.##") + "s",
                deathBarFadeTime, 0f, 2f) * 20f) / 20f;

            list.Label("ASQHPBar_DeathBarDisappearStyle".Translate());
            list.Gap(4f);
            foreach (DeathDisappearStyle style in System.Enum.GetValues(typeof(DeathDisappearStyle)))
            {
                if (list.RadioButton(
                    ("ASQHPBar_DeathBarDisappearStyle_" + style.ToString()).Translate(),
                    deathBarDisappearStyle == style,
                    18f))
                {
                    deathBarDisappearStyle = style;
                }
            }
            list.Gap(4f);
        }

        // --- 数值不变自动隐藏时的淡出效果 ---
        list.GapLine();
        list.CheckboxLabeled("ASQHPBar_EnableHideFade".Translate(), ref enableHideFadeEffect);
        if (enableHideFadeEffect)
        {
            hideFadeTime = Mathf.Round(list.SliderLabeled(
                "ASQHPBar_HideFadeTime".Translate() + ": " + hideFadeTime.ToString("0.##") + "s",
                hideFadeTime, 0f, 2f) * 20f) / 20f;
        }

        list.GapLine();
        list.CheckboxLabeled("ASQHPBar_EnableVerboseLogging".Translate(), ref enableVerboseLogging);

        // — Ammo Readout 兼容提示（兼容成功时显示）
        if (AmmoReadoutCompat.Enabled)
        {
            list.GapLine();
            GUI.color = new Color(0.6f, 1f, 0.6f);
            list.Label("ASQHPBar_AmmoReadoutCompatActive".Translate());
            GUI.color = Color.white;
        }
    }

    /// <summary>
    /// 健康条与颜色页：健康条外观（宽度/缓动/无标签/缩放/批量/兼容/下移/附属条宽度）与全部颜色设置。
    /// </summary>
    private void DrawAppearanceColorsPage(Listing_Standard list)
    {
        if (!enableImprovedHealthBar)
            return;

        list.GapLine();

        healthBarMinWidth = (int)list.SliderLabeled(
            "ASQHPBar_BarWidth".Translate() + ": " + healthBarMinWidth,
            healthBarMinWidth, 20f, 120f);

        healthBarMaxWidth = (int)list.SliderLabeled(
            "ASQHPBar_MaxBarWidth".Translate() + ": " + (EffectiveMaxBarWidth <= 0 ? "0" : EffectiveMaxBarWidth.ToString()),
            healthBarMaxWidth, 0f, 400f);

        healthScaleWidthMultiplier = (int)list.SliderLabeled(
            "ASQHPBar_HealthScaleWidth".Translate() + ": " + healthScaleWidthMultiplier,
            healthScaleWidthMultiplier, 0f, 30f);

        list.CheckboxLabeled("ASQHPBar_EnableEaseEffect".Translate(), ref enableEaseEffect);

        if (enableEaseEffect)
        {
            easeSpeed = list.SliderLabeled(
                "ASQHPBar_EaseSpeed".Translate() + ": " + easeSpeed.ToStringPercent(),
                easeSpeed, 0.01f, 1f);
        }
        else
        {
            easeSpeed = 0.1f;
        }

        list.Label("ASQHPBar_EasingStyle".Translate());
        list.Gap(4f);
        foreach (EasingStyle style in System.Enum.GetValues(typeof(EasingStyle)))
        {
            if (list.RadioButton(
                ("ASQHPBar_EasingStyle_" + style.ToString()).Translate(),
                easingStyle == style,
                18f))
            {
                easingStyle = style;
            }
        }
        list.Gap(4f);

        healthBarHideDelay = list.SliderLabeled(
            "ASQHPBar_HealthBarHideDelay".Translate() + ": " + healthBarHideDelay.ToString("0.#") + "s",
            healthBarHideDelay, 0f, 30f);

        noNameHealthBarHeight = (int)list.SliderLabeled(
            "ASQHPBar_NoNameHealthBarHeight".Translate() + ": " + noNameHealthBarHeight,
            noNameHealthBarHeight, 4f, 40f);

        noNameYOffset = (int)list.SliderLabeled(
            "ASQHPBar_NoNameYOffset".Translate() + ": " + noNameYOffset,
            noNameYOffset, -50f, 50f);

        list.Label("ASQHPBar_NoNameMaxZoom".Translate());
        list.Gap(4f);
        foreach (CameraZoomRange zoomVal in System.Enum.GetValues(typeof(CameraZoomRange)))
        {
            if (list.RadioButton(
                ("ASQHPBar_CameraZoomRange_" + zoomVal.ToString()).Translate(),
                noNameMaxZoom == zoomVal,
                18f))
            {
                noNameMaxZoom = zoomVal;
            }
        }
        list.Gap(4f);

        list.CheckboxLabeled("ASQHPBar_EnableZoomScale".Translate(), ref enableZoomScale);

        // 批量渲染
        list.GapLine();
        list.CheckboxLabeled("ASQHPBar_EnableBatchRendering".Translate(), ref enableBatchRendering);
        // 兼容模式
        list.CheckboxLabeled("ASQHPBar_EnableCompatibilityMode".Translate(), ref enableCompatibilityMode);
        if (enableCompatibilityMode)
        {
            enableHealthBarOffset = true;
        }

        if (enableCompatibilityMode)
        {
            GUI.color = Color.gray;
            bool dummy = true;
            list.CheckboxLabeled("ASQHPBar_EnableHealthBarOffset".Translate(), ref dummy);
            GUI.color = Color.white;
        }
        else
        {
            list.CheckboxLabeled("ASQHPBar_EnableHealthBarOffset".Translate(), ref enableHealthBarOffset);
        }

        if (enableHealthBarOffset)
        {

            healthBarHeight = (int)list.SliderLabeled(
                "ASQHPBar_HealthBarHeight".Translate() + ": " + healthBarHeight,
                healthBarHeight, 4f, 40f);
        }

        list.CheckboxLabeled("ASQHPBar_NoNamePositionAbove".Translate(), ref noNamePositionAbove);
        // --- 标签附属条宽度设置 ---
        list.GapLine();
        list.CheckboxLabeled("ASQHPBar_SubBarSyncWithLabel".Translate(), ref subBarSyncWithLabel);
        if (!subBarSyncWithLabel)
        {
            subBarWidth = (int)list.SliderLabeled(
                "ASQHPBar_SubBarWidth".Translate() + ": " + subBarWidth,
                subBarWidth, 20f, 120f);
        }

        // --- 颜色设置 ---
        list.GapLine();
        list.Label("ASQHPBar_ColorSettings".Translate());

        DrawColorRow(list, "ASQHPBar_ColorPlayer".Translate(),
            () => ColorPlayer,
            c => { colorPlayerStr = ColorToString(c); HealthBarEaseHelper.RebuildTextures(); });
        DrawColorRow(list, "ASQHPBar_ColorFriend".Translate(),
            () => ColorFriend,
            c => { colorFriendStr = ColorToString(c); HealthBarEaseHelper.RebuildTextures(); });
        DrawColorRow(list, "ASQHPBar_ColorNeutral".Translate(),
            () => ColorNeutral,
            c => { colorNeutralStr = ColorToString(c); HealthBarEaseHelper.RebuildTextures(); });
        DrawColorRow(list, "ASQHPBar_ColorActual".Translate(),
            () => ColorActual,
            c => { colorActualStr = ColorToString(c); HealthBarEaseHelper.RebuildTextures(); });
        DrawColorRow(list, "ASQHPBar_ColorDamage".Translate(),
            () => ColorDamage,
            c => { colorDamageStr = ColorToString(c); HealthBarEaseHelper.RebuildTextures(); });
        DrawColorRow(list, "ASQHPBar_ColorHeal".Translate(),
            () => ColorHeal,
            c => { colorHealStr = ColorToString(c); HealthBarEaseHelper.RebuildTextures(); });

        bool prevCustomBg = enableCustomBg;
        list.CheckboxLabeled("ASQHPBar_EnableCustomBg".Translate(), ref enableCustomBg);
        if (enableCustomBg != prevCustomBg)
            HealthBarEaseHelper.RebuildBgTexture();

        if (enableCustomBg)
        {
            DrawColorRow(list, "ASQHPBar_ColorBg".Translate(),
                () => ColorBg,
                c => { bgColorStr = ColorToString(c); HealthBarEaseHelper.RebuildTextures(); });

            list.Gap(6f);

            float prevGradRatio = bgGradRatio;
            bgGradRatio = list.SliderLabeled(
                "ASQHPBar_BgGradRatio".Translate() + ": " + (bgGradRatio * 100f).ToString("F0") + "%",
                bgGradRatio, 0f, 0.3f);
            if (Mathf.Abs(bgGradRatio - prevGradRatio) > 0.001f)
                HealthBarEaseHelper.RebuildBgTexture();
            float prevbgBorderSize = bgBorderSize;
            bgBorderSize = (int)list.SliderLabeled(
                "ASQHPBar_BgBorderSize".Translate() + ": " + bgBorderSize,
                bgBorderSize, 0f, 6f);
            if (Mathf.Abs(bgBorderSize - prevbgBorderSize) > 0.001f)
                HealthBarEaseHelper.RebuildBgTexture();
        }

        list.CheckboxLabeled("ASQHPBar_EnableBarGradient".Translate(), ref enableBarGradient);
        if (enableBarGradient)
        {
            int prevGradWidth = gradWidth;
            gradWidth = (int)list.SliderLabeled(
                "ASQHPBar_GradWidth".Translate() + ": " + gradWidth,
                gradWidth, 0f, 20f);
            if (gradWidth != prevGradWidth)
                HealthBarEaseHelper.RebuildTextures();
        }

        list.Gap(6f);
        if (Widgets.ButtonText(list.GetRect(30f), "ASQHPBar_ResetColors".Translate()))
        {
            enableCustomBg = true;
            enableBarGradient = true;
            colorActualStr = DefaultColorActualStr;
            colorFriendStr = DefaultColorFriendStr;
            colorPlayerStr = DefaultColorPlayerStr;
            colorNeutralStr = DefaultColorNeutralStr;
            colorDamageStr = DefaultColorDamageStr;
            colorHealStr = DefaultColorHealStr;
            bgColorStr = DefaultColorBgStr;
            bgGradRatio = 0.15f;
            gradWidth = 6;
            colorShieldStr = DefaultColorShieldStr;
            colorShieldDamageStr = DefaultColorShieldDamageStr;
            colorShieldHealStr = DefaultColorShieldHealStr;
            colorBloodCurrentStr = DefaultColorBloodCurrentStr;
            colorBloodBleedStr = DefaultColorBloodBleedStr;
            bloodBleedFactor = 1f;
            colorExplosiveThresholdStr = DefaultColorExplosiveThresholdStr;
            colorPainStr = DefaultColorPainStr;
            colorPainPlusStr = DefaultColorPainPlusStr;
            colorPainMinusStr = DefaultColorPainMinusStr;
            shieldBarHeight = 6;
            HealthBarEaseHelper.RebuildTextures();
        }
    }

    /// <summary>
    /// 附属条页：炮塔条、条间隔、附属条顺序、耐痛条、血液条、护盾条。
    /// </summary>
    private void DrawSubBarsPage(Listing_Standard list)
    {
        if (!enableImprovedHealthBar)
            return;

        // --- 炮塔条设置
        list.GapLine();
        list.CheckboxLabeled("ASQHPBar_EnableTurretHealthBar".Translate(), ref enableTurretHealthBar);
        if (enableTurretHealthBar)
        {
            turretHealthBarHeight = (int)list.SliderLabeled(
                "ASQHPBar_TurretHealthBarHeight".Translate() + ": " + turretHealthBarHeight,
                turretHealthBarHeight, 4f, 40f);

            turretHealthBarWidthMultiplier = (int)list.SliderLabeled(
                "ASQHPBar_TurretHealthBarWidthMultiplier".Translate() + ": " + (int)turretHealthBarWidthMultiplier,
                turretHealthBarWidthMultiplier, 12f, 100f);

            list.CheckboxLabeled("ASQHPBar_EnableExplosiveThreshold".Translate(), ref enableExplosiveThreshold);
            if (enableExplosiveThreshold)
            {
                list.CheckboxLabeled("ASQHPBar_ThinExplosiveMarker".Translate(), ref thinExplosiveMarker);
                DrawColorRow(list, "ASQHPBar_ColorExplosiveThreshold".Translate(),
                    () => ColorExplosiveThreshold,
                    c => { colorExplosiveThresholdStr = ColorToString(c); HealthBarEaseHelper.RebuildTextures(); });
            }
        }
        /// --- 额外条设置
        list.GapLine();
        list.GapLine();
        barSpacing = (int)list.SliderLabeled(
            "ASQHPBar_BarSpacing".Translate() + ": " + barSpacing,
            barSpacing, 0f, 6f);

        // --- 附属条显示顺序设置 ---
        list.Label("ASQHPBar_SubBarOrder".Translate());
        list.Gap(4f);
        SubBarKind[] orderArr = GetSubBarOrder();
        for (int i = 0; i < orderArr.Length; i++)
        {
            SubBarKind kind = orderArr[i];
            Rect rowRect = list.GetRect(24f);
            Widgets.Label(new Rect(rowRect.x, rowRect.y, rowRect.width - 96f, 24f),
                ("ASQHPBar_SubBarKind_" + kind.ToString()).Translate());

            float btnW = 44f;
            if (Widgets.ButtonText(new Rect(rowRect.x + rowRect.width - 92f, rowRect.y, btnW, 24f), "▲")
                && i > 0)
            {
                MoveSubBar(kind, -1);
            }
            if (Widgets.ButtonText(new Rect(rowRect.x + rowRect.width - 46f, rowRect.y, btnW, 24f), "▼")
                && i < orderArr.Length - 1)
            {
                MoveSubBar(kind, +1);
            }
        }
        list.Gap(4f);

        // --- 耐痛条设置 ---
        list.GapLine();
        list.CheckboxLabeled("ASQHPBar_EnablePainBar".Translate(), ref enablePainBar);
        if (enablePainBar)
        {
            list.CheckboxLabeled("ASQHPBar_InvertPainBar".Translate(), ref invertPainBar);
            painBarHeight = (int)list.SliderLabeled(
                "ASQHPBar_PainBarHeight".Translate() + ": " + painBarHeight,
                painBarHeight, 3f, 20f);
            if (enableEaseEffect)
            {
                painEaseSpeed = list.SliderLabeled(
                    "ASQHPBar_PainEaseSpeed".Translate() + ": " + painEaseSpeed.ToStringPercent(),
                    painEaseSpeed, 0.01f, 1f);
            }
            painBarHideDelay = list.SliderLabeled(
                "ASQHPBar_PainHideDelay".Translate() + ": " + painBarHideDelay.ToString("0.#") + "s",
                painBarHideDelay, 0f, 30f);
            DrawColorRow(list, "ASQHPBar_ColorPain".Translate(),
                () => ColorPain,
                c => { colorPainStr = ColorToString(c); HealthBarEaseHelper.RebuildTextures(); });
            DrawColorRow(list, "ASQHPBar_ColorPainPlus".Translate(),
                () => ColorPainPlus,
                c => { colorPainPlusStr = ColorToString(c); HealthBarEaseHelper.RebuildTextures(); });
            DrawColorRow(list, "ASQHPBar_ColorPainMinus".Translate(),
                () => ColorPainMinus,
                c => { colorPainMinusStr = ColorToString(c); HealthBarEaseHelper.RebuildTextures(); });
        }

        // --- 血液条设置 ---
        list.GapLine();
        list.CheckboxLabeled("ASQHPBar_EnableBloodBar".Translate(), ref enableBloodBar);
        if (enableBloodBar)
        {
            enableCalcBleed = false;
            if (!HasActiveCalcFactor())
            {
                // 血液条会强制关闭流血因素，需保证至少一个健康因素开启
                enableCalcInjury = true;
            }
            list.CheckboxLabeled("ASQHPBar_BloodBarAttachedMode".Translate(), ref bloodBarAttachedMode);
            if (!bloodBarAttachedMode)
            {
                bloodBarHeight = (int)list.SliderLabeled(
                    "ASQHPBar_BloodBarHeight".Translate() + ": " + bloodBarHeight,
                    bloodBarHeight, 3f, 20f);
            }
            else
            {
                list.CheckboxLabeled("ASQHPBar_ThinBloodMarker".Translate(), ref thinBloodMarker);
            }
            bloodBleedFactor = list.SliderLabeled(
                "ASQHPBar_BloodBleedFactor".Translate() + ": " + bloodBleedFactor.ToStringPercent("F0"),
                bloodBleedFactor, 0.1f, 2f);
            bloodBarHideDelay = list.SliderLabeled(
                "ASQHPBar_BloodHideDelay".Translate() + ": " + bloodBarHideDelay.ToString("0.#") + "s",
                bloodBarHideDelay, 0f, 30f);
            DrawColorRow(list, "ASQHPBar_ColorBloodCurrent".Translate(),
                () => ColorBloodCurrent,
                c => { colorBloodCurrentStr = ColorToString(c); HealthBarEaseHelper.RebuildTextures(); });
            DrawColorRow(list, "ASQHPBar_ColorBloodBleed".Translate(),
                () => ColorBloodBleed,
                c => { colorBloodBleedStr = ColorToString(c); HealthBarEaseHelper.RebuildTextures(); });
        }

        // --- 护盾条设置 ---

        list.GapLine();
        list.CheckboxLabeled("ASQHPBar_EnableShieldBar".Translate(), ref enableShieldBar);
        if (enableShieldBar)
        {
            shieldBarHeight = (int)list.SliderLabeled(
                "ASQHPBar_ShieldBarHeight".Translate() + ": " + shieldBarHeight,
                shieldBarHeight, 4f, 20f);

            if (enableEaseEffect)
            {
                shieldEaseSpeed = list.SliderLabeled(
                    "ASQHPBar_ShieldEaseSpeed".Translate() + ": " + shieldEaseSpeed.ToStringPercent(),
                    shieldEaseSpeed, 0.01f, 1f);
            }
            shieldBarHideDelay = list.SliderLabeled(
                "ASQHPBar_ShieldHideDelay".Translate() + ": " + shieldBarHideDelay.ToString("0.#") + "s",
                shieldBarHideDelay, 0f, 30f);
            DrawColorRow(list, "ASQHPBar_ColorShield".Translate(),
                () => ColorShield,
                c => { colorShieldStr = ColorToString(c); HealthBarEaseHelper.RebuildTextures(); });
            DrawColorRow(list, "ASQHPBar_ColorShieldDamage".Translate(),
                () => ColorShieldDamage,
                c => { colorShieldDamageStr = ColorToString(c); HealthBarEaseHelper.RebuildTextures(); });
            DrawColorRow(list, "ASQHPBar_ColorShieldHeal".Translate(),
                () => ColorShieldHeal,
                c => { colorShieldHealStr = ColorToString(c); HealthBarEaseHelper.RebuildTextures(); });
        }
    }

    /// <summary>
    /// 显示过滤页：单位类型 × 势力过滤网格、对隐形单位隐藏。
    /// </summary>
    private void DrawFiltersPage(Listing_Standard list)
    {
        if (enableImprovedHealthBar)
        {
            list.GapLine();
            DrawFilterGrid(list);
            list.CheckboxLabeled("ASQHPBar_HideHealthBarForInvisible".Translate(), ref hideHealthBarForInvisible);
        }
    }

    private static SubBarKind[]? cachedSubBarOrder;

    /// <summary>
    /// 获取附属条（耐痛/血液/护盾）的绘制顺序，数组按"紧邻健康条 → 远离健康条"排列。
    /// 设置列表中缺失的类型会自动追加到末尾，新增条类型后无需调整旧存档。
    /// </summary>
    public static SubBarKind[] GetSubBarOrder()
    {
        List<SubBarKind> order = subBarOrder;
        if (order == null || order.Count == 0)
        {
            order = new List<SubBarKind>();
            subBarOrder = order;
        }

        // 列表内容未变化时复用缓存，避免每帧分配
        bool upToDate = cachedSubBarOrder != null && cachedSubBarOrder.Length == order.Count;
        if (upToDate)
        {
            for (int i = 0; i < order.Count; i++)
            {
                if (cachedSubBarOrder![i] != order[i])
                {
                    upToDate = false;
                    break;
                }
            }
        }

        if (upToDate)
            return cachedSubBarOrder!;

        // 设置列表未包含的类型追加到末尾（新增条类型自动出现）
        foreach (SubBarKind kind in System.Enum.GetValues(typeof(SubBarKind)))
        {
            if (!order.Contains(kind))
                order.Add(kind);
        }

        cachedSubBarOrder = order.ToArray();
        return cachedSubBarOrder;
    }

    /// <summary>
    /// 在设置界面中上移/下移附属条，并刷新绘制顺序缓存。
    /// </summary>
    private static void MoveSubBar(SubBarKind kind, int dir)
    {
        List<SubBarKind> order = subBarOrder ?? new List<SubBarKind>();
        subBarOrder = order;
        int idx = order.IndexOf(kind);
        int target = idx + dir;
        if (idx < 0 || target < 0 || target >= order.Count)
            return;
        (order[idx], order[target]) = (order[target], order[idx]);
        cachedSubBarOrder = null;
    }

    /// <summary>
    /// 当前是否有至少一个健康因素处于开启状态（流血因素在血液条开启时视为关闭）。
    /// </summary>
    private static bool HasActiveCalcFactor()
    {
        if (enableCalcInjury) return true;
        if (!enableBloodBar && enableCalcBleed) return true;
        if (enableCalcBrain) return true;
        if (enableCalcHeart) return true;
        if (enableCalcMovement) return true;
        if (enableCalcCore) return true;
        return false;
    }

    /// <summary>
    /// 绘制健康因素复选框，并保证至少保留一个因素开启，避免计算退化。
    /// </summary>
    private static void ToggleFactor(Listing_Standard list, string label, ref bool value)
    {
        bool old = value;
        list.CheckboxLabeled(label, ref value);
        if (old && !value && !HasActiveCalcFactor())
        {
            value = true;
        }
    }

    private void DrawColorRow(Listing_Standard list, string label, System.Func<Color> getColor, System.Action<Color> saveColor)
    {
        Color color = getColor();

        // 第一行：标签 + 色块 + 更改按钮
        float row1Height = 28f;
        Rect rect1 = list.GetRect(row1Height);

        float swatchSize = row1Height - 4f;
        float rightWidth = 140f;
        float labelWidth = rect1.width - rightWidth;

        Widgets.Label(new Rect(rect1.x, rect1.y, labelWidth, row1Height), label);

        float btnX = rect1.x + labelWidth;
        Rect swatchRect = new Rect(btnX, rect1.y + (row1Height - swatchSize) / 2f, swatchSize, swatchSize);
        Widgets.DrawBoxSolid(swatchRect, color);
        Widgets.DrawBox(swatchRect, 1);

        float buttonX = swatchRect.xMax + 4f;
        float buttonW = rect1.x + rect1.width - buttonX;
        Rect btnRect = new Rect(buttonX, rect1.y, buttonW, row1Height);
        if (Widgets.ButtonText(btnRect, "ASQHPBar_ChangeColor".Translate()))
        {
            Find.WindowStack.Add(new Dialog_HealthBarColorPicker(color, c =>
            {
                saveColor(new Color(c.r, c.g, c.b, color.a));
            }));
        }

        // 第二行：透明度滑块（使用全宽，仅左侧留小间距）
        float row2Height = 24f;
        float margin = 4f;
        Rect rect2 = list.GetRect(row2Height);

        float alphaLabelW = Text.CalcSize("ASQHPBar_AlphaLabel".Translate()).x;
        float pctW = 46f;
        float sliderW = rect2.width - margin - alphaLabelW - 4f - pctW;

        Widgets.Label(new Rect(rect2.x + margin, rect2.y, alphaLabelW, row2Height),
            "ASQHPBar_AlphaLabel".Translate());

        float newAlpha = Widgets.HorizontalSlider(
            new Rect(rect2.x + margin + alphaLabelW + 4f, rect2.y, sliderW, row2Height),
            color.a, 0f, 1f, middleAlignment: true, roundTo: 0.01f);

        Widgets.Label(new Rect(rect2.x + margin + alphaLabelW + 4f + sliderW, rect2.y, pctW, row2Height),
            newAlpha.ToStringPercent("F0"));

        if (Mathf.Abs(newAlpha - color.a) > 0.001f)
        {
            saveColor(new Color(color.r, color.g, color.b, newAlpha));
        }
    }

    private void DrawFilterGrid(Listing_Standard list)
    {
        list.Label("ASQHPBar_ShowHealthBarFor".Translate());
        list.Gap(4f);

        float colLabelWidth = 64f;
        float colWidth = (list.ColumnWidth - colLabelWidth) / 4f;
        float checkSize = 24f;
        float rowHeight = 28f;

        // 列标题
        string[] colHeaders = { "ASQHPBar_Player", "ASQHPBar_Ally", "ASQHPBar_Enemy", "ASQHPBar_Neutral" };
        Rect headerRect = list.GetRect(rowHeight);
        for (int c = 0; c < 4; c++)
        {
            float cx = headerRect.x + colLabelWidth + c * colWidth;
            Rect cr = new Rect(cx, headerRect.y, colWidth, rowHeight);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(cr, colHeaders[c].Translate());
            Text.Anchor = TextAnchor.UpperLeft;
        }

        // 4 行 × 4 列
        string[] rowLabels = { "ASQHPBar_Animal", "ASQHPBar_Mech", "ASQHPBar_Turret", "ASQHPBar_Other" };
        for (int r = 0; r < 4; r++)
        {
            Rect rowRect = list.GetRect(rowHeight);
            Widgets.Label(new Rect(rowRect.x, rowRect.y, colLabelWidth, rowHeight), rowLabels[r].Translate());

            for (int c = 0; c < 4; c++)
            {
                float cbX = rowRect.x + colLabelWidth + c * colWidth + (colWidth - checkSize) / 2f;
                float cbY = rowRect.y + (rowHeight - checkSize) / 2f;
                bool val = GetFilterValue(r, c);
                Widgets.Checkbox(cbX, cbY, ref val, checkSize);
                SetFilterValue(r, c, val);
            }
        }
    }

    private bool GetFilterValue(int row, int col)
    {
        return (row, col) switch
        {
            (0, 0) => showAnimalPlayer,
            (0, 1) => showAnimalAlly,
            (0, 2) => showAnimalEnemy,
            (0, 3) => showAnimalNeutral,
            (1, 0) => showMechPlayer,
            (1, 1) => showMechAlly,
            (1, 2) => showMechEnemy,
            (1, 3) => showMechNeutral,
            (2, 0) => showTurretPlayer,
            (2, 1) => showTurretAlly,
            (2, 2) => showTurretEnemy,
            (2, 3) => showTurretNeutral,
            (3, 0) => showOtherPlayer,
            (3, 1) => showOtherAlly,
            (3, 2) => showOtherEnemy,
            (3, 3) => showOtherNeutral,
            _ => true
        };
    }

    private void SetFilterValue(int row, int col, bool value)
    {
        switch (row, col)
        {
            case (0, 0): showAnimalPlayer = value; break;
            case (0, 1): showAnimalAlly = value; break;
            case (0, 2): showAnimalEnemy = value; break;
            case (0, 3): showAnimalNeutral = value; break;
            case (1, 0): showMechPlayer = value; break;
            case (1, 1): showMechAlly = value; break;
            case (1, 2): showMechEnemy = value; break;
            case (1, 3): showMechNeutral = value; break;
            case (2, 0): showTurretPlayer = value; break;
            case (2, 1): showTurretAlly = value; break;
            case (2, 2): showTurretEnemy = value; break;
            case (2, 3): showTurretNeutral = value; break;
            case (3, 0): showOtherPlayer = value; break;
            case (3, 1): showOtherAlly = value; break;
            case (3, 2): showOtherEnemy = value; break;
            case (3, 3): showOtherNeutral = value; break;
        }
    }
}
