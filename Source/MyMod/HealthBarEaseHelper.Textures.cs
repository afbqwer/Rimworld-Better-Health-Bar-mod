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

        Color actualColor = SimpleHealthBarSettings.ColorActual;
        Color friendColor = SimpleHealthBarSettings.ColorFriend;
        Color playerColor = SimpleHealthBarSettings.ColorPlayer;
        Color neutralColor = SimpleHealthBarSettings.ColorNeutral;
        Color damageColor = SimpleHealthBarSettings.ColorDamage;
        Color enemyDamageColor = SimpleHealthBarSettings.ColorEnemyDamage;
        Color healColor = SimpleHealthBarSettings.ColorHeal;

        EasedDamageTex = SolidColorMaterials.NewSolidColorTexture(damageColor);
        EasedEnemyDamageTex = SolidColorMaterials.NewSolidColorTexture(enemyDamageColor);
        EasedHealTex = SolidColorMaterials.NewSolidColorTexture(healColor);
        ActualBarTex = SolidColorMaterials.NewSolidColorTexture(actualColor);
        FriendActualBarTex = SolidColorMaterials.NewSolidColorTexture(friendColor);
        PlayerActualBarTex = SolidColorMaterials.NewSolidColorTexture(playerColor);
        NeutralActualBarTex = SolidColorMaterials.NewSolidColorTexture(neutralColor);

        ShieldBarTex = SolidColorMaterials.NewSolidColorTexture(SimpleHealthBarSettings.ColorShield);
        ShieldDamageTex = SolidColorMaterials.NewSolidColorTexture(SimpleHealthBarSettings.ColorShieldDamage);
        ShieldHealTex = SolidColorMaterials.NewSolidColorTexture(SimpleHealthBarSettings.ColorShieldHeal);

        BloodCurrentTex = SolidColorMaterials.NewSolidColorTexture(SimpleHealthBarSettings.ColorBloodCurrent);
        BloodBleedTex = SolidColorMaterials.NewSolidColorTexture(SimpleHealthBarSettings.ColorBloodBleed);

        PainBarTex = SolidColorMaterials.NewSolidColorTexture(SimpleHealthBarSettings.ColorPain);
        PainPlusTex = SolidColorMaterials.NewSolidColorTexture(SimpleHealthBarSettings.ColorPainPlus);
        PainMinusTex = SolidColorMaterials.NewSolidColorTexture(SimpleHealthBarSettings.ColorPainMinus);

        ExplosiveThresholdTex = SolidColorMaterials.NewSolidColorTexture(SimpleHealthBarSettings.ColorExplosiveThreshold);

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
            LeftGradPain = null;
            RightGradPain = null;
            LeftGradDamage = null;
            LeftGradEnemyDamage = null;
        }

        RebuildBgTexture();

        // 初始化批量渲染材质（使用 uGUI Default 着色器，unlit 且支持透明纹理）
        if (batchMaterial == null)
        {
            var shader = Shader.Find("UI/Default") ?? Shader.Find("GUI/Text Shader");
            if (shader == null)
            {
                Log.Error("[Simple Health Bar] 找不到 UI/Default 或 GUI/Text Shader，批量模式将不可用");
                return;
            }
            batchMaterial = new Material(shader)
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
        return tex;
    }

    /// <summary>
    /// 根据当前 ColorBg 和 bgGradRatio 重建背景纹理（两端渐变，中间实心）。
    /// </summary>
    public static void RebuildBgTexture()
    {
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
    /// 刷新批量渲染缓冲区：将当前轮次收集到的纹理矩形按纹理分组，用 GL 批处理绘制。
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

        // 按纹理分组，最小化材质状态切换
        var groups = new Dictionary<Texture2D, List<BatchTexRect>>();
        for (int i = 0; i < batchTexRects.Count; i++)
        {
            var r = batchTexRects[i];
            if (r.tex == null) continue;
            if (!groups.TryGetValue(r.tex, out var list))
            {
                list = new List<BatchTexRect>();
                groups[r.tex] = list;
            }
            list.Add(r);
        }

        GL.PushMatrix();
        GL.LoadPixelMatrix(0f, Screen.width / _cachedUIScale, Screen.height / _cachedUIScale, 0f);

        foreach (var kvp in groups)
        {
            if (kvp.Value.Count == 0) continue;
            batchMaterial.mainTexture = kvp.Key;
            batchMaterial.SetPass(0);

            GL.Begin(GL.QUADS);
            for (int i = 0; i < kvp.Value.Count; i++)
            {
                var r = kvp.Value[i];
                // 逐矩形顶点色（淡出用，alpha=1 时与原先一致）
                GL.Color(new Color(1f, 1f, 1f, r.alpha));
                GL.TexCoord3(0f, 0f, 0f);
                GL.Vertex3(r.x, r.y, 0f);
                GL.TexCoord3(1f, 0f, 0f);
                GL.Vertex3(r.x + r.w, r.y, 0f);
                GL.TexCoord3(1f, 1f, 0f);
                GL.Vertex3(r.x + r.w, r.y + r.h, 0f);
                GL.TexCoord3(0f, 1f, 0f);
                GL.Vertex3(r.x, r.y + r.h, 0f);
            }
            GL.End();
        }

        // 复位当前顶点色，避免影响同帧其它立即模式绘制
        GL.Color(Color.white);

        GL.PopMatrix();

        batchTexRects.Clear();
    }

}