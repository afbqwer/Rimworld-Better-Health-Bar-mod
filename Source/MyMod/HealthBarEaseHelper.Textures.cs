using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ASQHPBar;

/// <summary>
/// 材质生成逻辑：初始化纹理、重建纹理、渐变纹理、背景纹理。
/// </summary>
public static partial class HealthBarEaseHelper
{
    // — 批量渲染材质
    private static Material? batchMaterial = null;

    // — 批量渲染取色表
    // 纯色 / 渐变纹理在批量路径里不再采样，改为顶点色；这张表把纹理映射回它的基础色。
    // 未登记的纹理（例如 TexUI.GrayTextBG）仍走纹理批次，行为与之前一致。
    private static readonly Dictionary<Texture2D, Color> solidColors = new Dictionary<Texture2D, Color>();

    /// <summary>
    /// 建一张 1×1 纯色纹理并登记颜色。批量路径据此用顶点色代替纹理采样。
    /// </summary>
    private static Texture2D Solid(Color color)
    {
        Texture2D tex = SolidColorMaterials.NewSolidColorTexture(color);
        solidColors[tex] = color;
        return tex;
    }

    /// <summary>
    /// 取该纹理登记的基础色；false 表示未登记（非本模块生成的贴图），调用方应回退到纹理批次。
    /// </summary>
    private static bool TryGetSolidColor(Texture2D? tex, out Color color)
    {
        if (tex != null && solidColors.TryGetValue(tex, out color))
        {
            return true;
        }
        color = white;
        return false;
    }

    /// <summary>
    /// 是否启用批量渲染。
    /// 1.7 的 GL 不再是 OpenGL 立即模式：<c>GL.End</c> 把整段顶点交给 ImGui 绘制列表，
    /// 着色器不参与，材质只作为 mainTexture 的载体，因此不再需要 UI/Default，
    /// 可与其他版本走同一条批处理路径。
    /// 任一版本下若材质创建失败（batchMaterial 为空）都会回退到逐条绘制。
    /// </summary>
    internal static bool BatchRenderingEnabled =>
        SimpleHealthBarSettings.enableBatchRendering && batchMaterial != null;

    static HealthBarEaseHelper()
    {
        RebuildTextures();

        // Populate TurretDefs HashSet for turret building health bar support
        foreach (var def in DefDatabase<ThingDef>.AllDefs)
        {
            if (def.useHitPoints &&
            (typeof(Building_Turret).IsAssignableFrom(def.thingClass)
            || def.HasComp<CompProjectileInterceptor>()))
            {
                TurretDefs.Add(def.defNameHash);
                if (SimpleHealthBarSettings.enableVerboseLogging)
                {
                    Log.Message(def.defName);
                }
            }
        }
    }

    public static void RebuildTextures()
    {
        GradWidth = SimpleHealthBarSettings.gradWidth;
        solidColors.Clear();

        Color actualColor = SimpleHealthBarSettings.ColorActual;
        Color friendColor = SimpleHealthBarSettings.ColorFriend;
        Color playerColor = SimpleHealthBarSettings.ColorPlayer;
        Color neutralColor = SimpleHealthBarSettings.ColorNeutral;
        Color damageColor = SimpleHealthBarSettings.ColorDamage;
        Color enemyDamageColor = SimpleHealthBarSettings.ColorEnemyDamage;
        Color healColor = SimpleHealthBarSettings.ColorHeal;

        EasedDamageTex = Solid(damageColor);
        EasedEnemyDamageTex = Solid(enemyDamageColor);
        EasedHealTex = Solid(healColor);
        ActualBarTex = Solid(actualColor);
        FriendActualBarTex = Solid(friendColor);
        PlayerActualBarTex = Solid(playerColor);
        NeutralActualBarTex = Solid(neutralColor);

        ShieldBarTex = Solid(SimpleHealthBarSettings.ColorShield);
        ShieldDamageTex = Solid(SimpleHealthBarSettings.ColorShieldDamage);
        ShieldHealTex = Solid(SimpleHealthBarSettings.ColorShieldHeal);

        BloodCurrentTex = Solid(SimpleHealthBarSettings.ColorBloodCurrent);
        BloodBleedTex = Solid(SimpleHealthBarSettings.ColorBloodBleed);

        PainBarTex = Solid(SimpleHealthBarSettings.ColorPain);
        PainPlusTex = Solid(SimpleHealthBarSettings.ColorPainPlus);
        PainMinusTex = Solid(SimpleHealthBarSettings.ColorPainMinus);

        ExplosiveThresholdTex = Solid(SimpleHealthBarSettings.ColorExplosiveThreshold);

        if (GradWidth > 0)
        {
            LeftGradActual = CreateGradient(actualColor, true);
            RightGradActual = CreateGradient(actualColor, false);
            LeftGradFriend = CreateGradient(friendColor, true);
            RightGradFriend = CreateGradient(friendColor, false);
            LeftGradPlayer = CreateGradient(playerColor, true);
            RightGradPlayer = CreateGradient(playerColor, false);
            LeftGradNeutral = CreateGradient(neutralColor, true);
            RightGradNeutral = CreateGradient(neutralColor, false);

            Color shieldColor = SimpleHealthBarSettings.ColorShield;
            LeftGradShield = CreateGradient(shieldColor, true);
            RightGradShield = CreateGradient(shieldColor, false);

            Color bloodColor = SimpleHealthBarSettings.ColorBloodCurrent;
            LeftGradBlood = CreateGradient(bloodColor, true);
            RightGradBlood = CreateGradient(bloodColor, false);
            RightGradBleed = CreateGradient(SimpleHealthBarSettings.ColorBloodBleed, false);

            Color painColor = SimpleHealthBarSettings.ColorPain;
            LeftGradPain = CreateGradient(painColor, true);
            RightGradPain = CreateGradient(painColor, false);

            LeftGradDamage = CreateGradient(damageColor, true);
            LeftGradEnemyDamage = CreateGradient(enemyDamageColor, true);
        }
        else
        {
            LeftGradActual = null;
            RightGradActual = null;
            LeftGradFriend = null;
            RightGradFriend = null;
            LeftGradPlayer = null;
            RightGradPlayer = null;
            LeftGradNeutral = null;
            RightGradNeutral = null;
            LeftGradShield = null;
            RightGradShield = null;
            LeftGradBlood = null;
            RightGradBlood = null;
            RightGradBleed = null;
            LeftGradPain = null;
            RightGradPain = null;
            LeftGradDamage = null;
            LeftGradEnemyDamage = null;
        }

        RebuildBgTexture();

        if (batchMaterial == null)
        {
#if RIMWORLD_1_7
            // 1.7 的 GL 在 End 时只从 Material.CurrentImmediateSource 取一张纹理交给 ImGui，
            // 着色器与 SetPass 的其余语义都不参与绘制；UI/Default 在 1.7 也不存在
            // （Shader.Find 只解析文件型资源）。用引擎自带的 Map/Transparent 即可。
            Shader? batchShader = ShaderDatabase.Transparent;
#else
            Shader? batchShader = Shader.Find("UI/Default") ?? Shader.Find("GUI/Text Shader");
#endif
            if (batchShader == null)
            {
                Log.Error("[Simple Health Bar] 找不到可用的 UI 着色器，批量模式将不可用");
                return;
            }
            batchMaterial = new Material(batchShader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }
    }

    private static Texture2D CreateGradient(Color color, bool isLeftToRight)
    {
        var tex = new Texture2D(GradWidth, 1, TextureFormat.ARGB32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var colors = new Color[GradWidth];
        for (int i = 0; i < GradWidth; i++)
        {
            float t = (float)i / (GradWidth - 1);
            float alpha = isLeftToRight ? t : (1f - t);
            colors[i] = new Color(color.r, color.g, color.b, alpha * color.a);
        }
        tex.SetPixels(colors);
        tex.Apply();
        // 渐变段的颜色与对应纯色段一致，登记后批量路径可用顶点 alpha 斜坡替代这张贴图
        solidColors[tex] = color;
        return tex;
    }

    /// <summary>
    /// 根据当前 ColorBg 和 bgGradRatio 重建背景纹理（两端渐变，中间实心）。
    /// </summary>
    public static void RebuildBgTexture()
    {
        // ColorBg 每次访问都要 Split + 4 次 float.Parse，不能出现在绘制路径上；
        // 所有会改动它的入口（颜色、渐变比例、开关）都会走到这里，缓存在此处更新即可。
        bgColorCached = SimpleHealthBarSettings.ColorBg;
        bgGradRatioCached = SimpleHealthBarSettings.bgGradRatio;

        if (!SimpleHealthBarSettings.enableCustomBg)
        {
            BgTex = TexUI.GrayTextBG;
            return;
        }

        Color bgColorSetting = SimpleHealthBarSettings.ColorBg;
        var baseColor = new Color(bgColorSetting.r, bgColorSetting.g, bgColorSetting.b, 1f);

        const int texWidth = 100;
        float gradRatio = SimpleHealthBarSettings.bgGradRatio;

        var tex = new Texture2D(texWidth, 1, TextureFormat.ARGB32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var colors = new Color[texWidth];

        for (int i = 0; i < texWidth; i++)
        {
            float t = (float)i / (texWidth - 1); // 0 → 1

            float alphaFactor;
            if (t < gradRatio)
            {
                // 左端渐变：0 → 1
                alphaFactor = t / gradRatio;
            }
            else if (t > 1f - gradRatio)
            {
                // 右端渐变：1 → 0
                alphaFactor = (1f - t) / gradRatio;
            }
            else
            {
                // 中间实心
                alphaFactor = 1f;
            }

            colors[i] = new Color(baseColor.r, baseColor.g, baseColor.b, bgColorSetting.a * alphaFactor);
        }

        tex.SetPixels(colors);
        tex.Apply();
        BgTex = tex;
    }

    /// <summary>
    /// 将 UI 空间坐标对齐到屏幕像素网格，防止 UIScale ≠ 1.0 时因缩放产生亚像素间隙。
    /// </summary>
    private static Rect SnapRectToUIScalePixel(Rect rect)
    {
        float scale = _cachedUIScale;
        if (scale <= 1f) return rect;

        float sx = rect.x * scale;
        float sy = rect.y * scale;
        float sr = (rect.x + rect.width) * scale;
        float sb = (rect.y + rect.height) * scale;

        float rx = Mathf.Round(sx);
        float ry = Mathf.Round(sy);
        float rr = Mathf.Round(sr);
        float rb = Mathf.Round(sb);

        return new Rect(rx / scale, ry / scale, (rr - rx) / scale, (rb - ry) / scale);
    }

    /// <summary>
    /// 把浮点 UI 矩形转成像素矩形：四条边分别取整后再求宽高。
    /// 不能用 (int)rect.x / (int)rect.width —— 那是各自截断，右缘会落在 floor(x)+floor(w)，
    /// 比真实右缘 xMax 最多内缩近 2px（frac(x)+frac(w)），表现为条比标签窄一截且整体左偏。
    /// 按边取整的结果与 GUI 的像素中心采样一致：正好覆盖原矩形实际填充的那些列/行，
    /// 因此与按浮点矩形绘制的原版标签保持一致的中心与宽度。
    /// </summary>
    private static Rect PixelRect(Rect rect)
    {
        float x = Mathf.RoundToInt(rect.x);
        float y = Mathf.RoundToInt(rect.y);
        float right = Mathf.RoundToInt(rect.xMax);
        float bottom = Mathf.RoundToInt(rect.yMax);
        return new Rect(x, y, right - x, bottom - y);
    }
    // 背景参数缓存：ColorBg 每次访问都要 Split + 4 次 float.Parse，
    // 绘制路径上每帧每单位取用一次不能这么干（见 RebuildBgTexture）。
    private static Color bgColorCached = new Color(0.08f, 0.08f, 0.08f, 0.5f);
    private static float bgGradRatioCached = 0.15f;
    private static Color white = new Color(1f, 1f, 1f, 1f);
    /// <summary>
    /// 刷新批量渲染缓冲区。
    /// 纯色填充与渐变都已转成顶点色：它们统一用 BaseContent.WhiteTex 作纹理，
    /// 整段合成一次 GL.Begin/End，只产生一个绘制调用（不再按颜色/渐变分纹理）。
    /// 仍带自身纹理的矩形（默认背景 TexUI.GrayTextBG）按纹理再分一组。
    /// 仅在 Repaint 事件中实际绘制；其余事件仅保留缓冲区。
    /// 每次调用都会清空缓冲区，可在同帧多次调用（炮台 vs 单位各自刷出）。
    /// </summary>
    public static void FlushBatch()
    {
        if (batchTexRects.Count == 0)
            return;

        if (batchMaterial == null)
        {
            batchTexRects.Clear();
            return;
        }

        // 仅在 Repaint 事件中绘制；其余事件保留缓冲区供 Repaint 使用
        if (Event.current == null || Event.current.type != EventType.Repaint)
            return;

#if RIMWORLD_1_7
        // 1.7：GL 顶点直接进 ImGui 绘制列表，坐标保持 GUI 空间、由 backend 的 ToScreen
        // 应用 UI 矩阵（GUI.DrawTexture 走同一条路）。再叠加 LoadPixelMatrix 会在
        // UIScale ≠ 1 时二次缩放，所以这里不再设置像素矩阵。
#else
        GL.PushMatrix();
        GL.LoadPixelMatrix(0f, Screen.width / _cachedUIScale, Screen.height / _cachedUIScale, 0f);
#endif

        // 第一遍：纯顶点色。全部血条共用一张 1×1 白纹理，纹理切换为零，
        // 整段顶点进同一次 Begin/End → 一个绘制调用。
        batchMaterial.mainTexture = BaseContent.WhiteTex;
        batchMaterial.SetPass(0);

        GL.Begin(GL.QUADS);
        for (int i = 0; i < batchTexRects.Count; i++)
        {
            if (batchTexRects[i].tex == null)
            {
                var r = batchTexRects[i];
                EmitQuad(in r);
            }
        }
        GL.End();

        // 第二遍：仍带自身纹理的矩形，按纹理分组（通常只有默认背景一张）
        Dictionary<Texture2D, List<BatchTexRect>>? groups = null;
        for (int i = 0; i < batchTexRects.Count; i++)
        {
            var r = batchTexRects[i];
            if (r.tex == null) continue;
            groups ??= new Dictionary<Texture2D, List<BatchTexRect>>();
            if (!groups.TryGetValue(r.tex, out var list))
            {
                list = new List<BatchTexRect>();
                groups[r.tex] = list;
            }
            list.Add(r);
        }

        if (groups != null)
        {
            foreach (var kvp in groups)
            {
                if (kvp.Value.Count == 0) continue;
                batchMaterial.mainTexture = kvp.Key;
                batchMaterial.SetPass(0);

                GL.Begin(GL.QUADS);
                for (int i = 0; i < kvp.Value.Count; i++)
                {
                    var r = kvp.Value[i];
                    EmitQuad(in r);
                }
                GL.End();
            }
        }

        // 复位当前顶点色与纹理引用，避免影响同帧其它立即模式绘制
        GL.Color(white);
        batchMaterial.mainTexture = null;

#if !RIMWORLD_1_7
        GL.PopMatrix();
#endif

        batchTexRects.Clear();

        // 单个四边形：左上/右上/右下/左下。alpha = 颜色自身 alpha × 淡出系数，
        // 左右两端按 ramp 标志降到 0，用来替代原来的 6px 渐变贴图。
        static void EmitQuad(in BatchTexRect r)
        {
            float baseA = r.color.a * r.alpha;
            float aL = r.rampLeft ? 0f : baseA;
            float aR = r.rampRight ? 0f : baseA;
            Color cL = new Color(r.color.r, r.color.g, r.color.b, aL);
            Color cR = new Color(r.color.r, r.color.g, r.color.b, aR);

            // 1.7 新引擎的 UnityEngine.GL 仅提供 TexCoord/TexCoord2，TexCoord3 已移除；
            // TexCoord2 在 1.6 与 1.7 均可用，故直接统一使用。
            GL.Color(cL); GL.TexCoord2(0f, 0f); GL.Vertex3(r.x, r.y, 0f);
            GL.Color(cR); GL.TexCoord2(1f, 0f); GL.Vertex3(r.x + r.w, r.y, 0f);
            GL.Color(cR); GL.TexCoord2(1f, 1f); GL.Vertex3(r.x + r.w, r.y + r.h, 0f);
            GL.Color(cL); GL.TexCoord2(0f, 1f); GL.Vertex3(r.x, r.y + r.h, 0f);
        }
    }

}