using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ASQHPBar;

/// <summary>
/// Prefix for SummaryHealthHandler.get_SummaryHealthPercent
/// 当 enableImprovedCalculation 开启时使用最小值模型计算健康值。
/// </summary>
[HarmonyPatch(typeof(SummaryHealthHandler), "get_SummaryHealthPercent")]
public static class Patch_SummaryHealthHandler_get_SummaryHealthPercent
{
    public static bool Prefix(
        Pawn ___pawn,
        ref bool ___dirty,
        ref float ___cachedSummaryHealthPercent,
        ref float __result)
    {
        if (!SimpleHealthBarSettings.enableImprovedCalculation)
        {
            return true;
        }

        if (___pawn.Dead)
        {
            __result = 0f;
            return false;
        }

        if (!___dirty)
        {
            __result = ___cachedSummaryHealthPercent;
            return false;
        }

        var hediffSet = ___pawn.health.hediffSet;
        float healthScale = ___pawn.HealthScale;
        if (___pawn.RaceProps.IsMechanoid)
        {
            healthScale *= 1.2f;
        }
        List<Hediff> hediffs = hediffSet.hediffs;

        // === 单次遍历 hediffs：构建部位→损伤Severity字典 + 累计总损伤 ===
        var partInjury = new Dictionary<BodyPartRecord, float>();
        float totalInjurySev = 0f;

        for (int i = 0; i < hediffs.Count; i++)
        {
            // enableCalcPermanent 关闭时无视永久伤口（IsPermanent）；开启时计入永久伤口
            if (hediffs[i] is Hediff_Injury injury && injury.Visible && (SimpleHealthBarSettings.enableCalcPermanent || !injury.IsPermanent()))
            {
                float sev = injury.Severity;
                totalInjurySev += sev;
                BodyPartRecord part = injury.Part;
                if (part != null)
                {
                    if (partInjury.TryGetValue(part, out float existing))
                        partInjury[part] = existing + sev;
                    else
                        partInjury[part] = sev;
                }
            }
        }

        // === 缺失部位 HashSet ===
        List<Hediff_MissingPart>? missingPartsList = null;
        var missingPartSet = _emptyBPRSet;
        if (SimpleHealthBarSettings.enableCalcMissingParts)
        {
            missingPartsList = hediffSet.GetMissingPartsCommonAncestors();
            missingPartSet = new HashSet<BodyPartRecord>(missingPartsList.Count);
            for (int i = 0; i < missingPartsList.Count; i++)
            {
                missingPartSet.Add(missingPartsList[i].Part);
                totalInjurySev += missingPartsList[i].Part.def.hitPoints * healthScale;
            }
        }

        // === 获取缓存的身体部位 tag 分组 ===
        var tagCache = GetOrBuildTagCache(___pawn.def.race.body);

        // 1. 伤害健康分
        float injuryHealth = 1f;
        if (SimpleHealthBarSettings.enableCalcInjury)
        {
            injuryHealth = 1f - Mathf.Min(totalInjurySev / (SimpleHealthBarSettings.injuryLethalThreshold * healthScale), 0.8f);
        }

        // 2. 流血健康分
        float bleedHealth = 1f;
        if (SimpleHealthBarSettings.enableCalcBleed)
        {
            Hediff bloodLossHediff = hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
            bleedHealth = 1f - Mathf.Min(bloodLossHediff?.Severity ?? 0f, 0.8f);
        }

        // 3. 意识部位健康分
        // 同时检查大脑（ConsciousnessSource）本身损伤和父部位损伤
        float brainHealth = 1f;
        if (SimpleHealthBarSettings.enableCalcBrain)
        {
            float maxConsciousnessDamageRatio = 0f;

            // 3a. 大脑（ConsciousnessSource）本身损伤
            var sources = tagCache.ConsciousnessSources;
            for (int i = 0; i < sources.Count; i++)
            {
                var source = sources[i];
                if (missingPartSet.Contains(source))
                {
                    maxConsciousnessDamageRatio = 1f;
                    break;
                }
                if (partInjury.TryGetValue(source, out float sev))
                {
                    float ratio = sev / source.def.hitPoints / healthScale;
                    if (ratio > maxConsciousnessDamageRatio)
                        maxConsciousnessDamageRatio = ratio;
                }
            }
            // 3b. 父部位损伤——按总数权重累计（等同于 CalcPresentTagDamage 逻辑）
            if (maxConsciousnessDamageRatio < 1f)
            {
                float parentTotalRatio = CalcPresentTagDamage(
                    tagCache.ConsciousnessSourceParents,
                    missingPartSet,
                    partInjury,
                    healthScale,
                    tagCache.ConsciousnessSourceParentWeight);
                if (parentTotalRatio > maxConsciousnessDamageRatio)
                    maxConsciousnessDamageRatio = parentTotalRatio;
            }
            brainHealth = 1f - Mathf.Min(maxConsciousnessDamageRatio, 0.8f);
        }

        // 4. 移动部位——存留部位损伤
        float moveHealth = 1f;
        if (SimpleHealthBarSettings.enableCalcMovement)
        {
            float moveDamageRatio = CalcPresentTagDamage(tagCache.MovingCores, missingPartSet, partInjury, healthScale, tagCache.MovingCoreWeight);
            if (missingPartsList != null)
            {
                for (int i = 0; i < missingPartsList.Count; i++)
                {
                    var part = missingPartsList[i].Part;
                    if (part.def.tags.Contains(BodyPartTagDefOf.MovingLimbCore))
                        moveDamageRatio += tagCache.MovingCoreWeight;
                }
            }
            //var player = Faction.OfPlayerSilentFail;
            //if (___pawn.Faction != player && Find.Storyteller.difficulty.enemyDeathOnDownedChanceFactor > 0.001f)
            //{
            //    moveHealth = 1f - Mathf.Min(moveDamageRatio * 1.0f, 0.8f);
            //}
            moveHealth = 1f - Mathf.Min(moveDamageRatio * 0.8f, 0.75f);
        }

        // 5. 心脏因子——BloodPumpingSource 部位损伤
        float heartHealth = 1f;
        if (SimpleHealthBarSettings.enableCalcHeart)
        {
            float heartDamageRatio = CalcPresentTagDamage(tagCache.BloodPumpingSources, missingPartSet, partInjury, healthScale, tagCache.BloodPumpingWeight);
            if (missingPartsList != null)
            {
                for (int i = 0; i < missingPartsList.Count; i++)
                {
                    var part = missingPartsList[i].Part;
                    if (part.def.tags.Contains(BodyPartTagDefOf.BloodPumpingSource))
                        heartDamageRatio += tagCache.BloodPumpingWeight;
                }
            }
            heartHealth = 1f - Mathf.Min(heartDamageRatio * 0.7f, 0.7f);
        }

        // 6. 核心部位健康分
        float coreHealth = 1f;
        if (SimpleHealthBarSettings.enableCalcCore)
        {
            var corePart = tagCache.CorePart;
            if (missingPartSet.Contains(corePart))
            {
                coreHealth = 0f;
            }
            else if (partInjury.TryGetValue(corePart, out float sev))
            {
                float ratio = sev / corePart.def.hitPoints / healthScale;
                coreHealth = 1f - Mathf.Min(ratio * 0.7f, 0.8f);
            }
        }

        float dInjury = 1f - injuryHealth;
        float dBleed = 1f - bleedHealth;
        float dBrain = 1f - brainHealth;
        float dMove = 1f - moveHealth;
        float dHeart = 1f - heartHealth;
        float dCore = 1f - coreHealth;

        // 级联组合：将各维度伤害从大到小排序，第 i 大的伤害乘以衰减系数 decay^(i-1)
        float[] damages = [dInjury, dBleed, dBrain, dMove, dHeart, dCore];
        Array.Sort(damages); // 升序排列，最后一个最大
        float cascade = 0f;
        float remainingHealth = 1f;
        float decay = 1.0f; // 起始权重 1.0（最大因子100%），之后每轮 ×0.45
        for (int i = damages.Length - 1; i >= 0; i--)
        {
            float d = damages[i];
            if (d <= 0f) break;
            cascade += remainingHealth * d * decay;
            remainingHealth *= 1f - d * decay;
            decay *= 0.45f;
        }
        float combinedDamage = Mathf.Min(cascade, 1f);
        float result = 1f - combinedDamage;
        if (SimpleHealthBarSettings.enableVerboseLogging)
        {
            // === 日志：汇总各维度健康分 ===
            Log.Message(
                $"[HealthBar] {___pawn.Name?.ToStringShort} — " +
                $"injury={injuryHealth:F3}, bleed={bleedHealth:F3}, " +
                $"brain={brainHealth:F3}, move={moveHealth:F3}, heart={heartHealth:F3}, core={coreHealth:F3}, " +
                $"combinedDmg={combinedDamage:F3}, result={result:F3}, " +
                $"clamped={Mathf.Clamp(result, 0.01f, 1f):F3}"
            );
        }
        ___cachedSummaryHealthPercent = Mathf.Clamp(result, 0.05f, 1f);
        ___dirty = false;
        __result = ___cachedSummaryHealthPercent;
        return false;
    }

    /// <summary>
    /// 计算缓存 tag 分组中实际存留部位的损伤比例
    /// （使用 HashSet 做 O(1) 缺失判定）
    /// </summary>
    private static float CalcPresentTagDamage(
        List<BodyPartRecord> tagParts,
        HashSet<BodyPartRecord> missingPartSet,
        Dictionary<BodyPartRecord, float> partInjury,
        float healthScale,
        float weight)
    {
        float total = 0f;
        for (int i = 0; i < tagParts.Count; i++)
        {
            var part = tagParts[i];
            if (!missingPartSet.Contains(part))
            {
                if (partInjury.TryGetValue(part, out float sev))
                    total += sev / part.def.hitPoints / healthScale * weight;
            }
        }
        return total;
    }

    /// <summary>
    /// 静态缓存：按 BodyDef 缓存预分组的身体部位列表
    /// BodyDef + BodyPartRecord.tags 在运行时完全静态，只需构建一次
    /// </summary> 
    private static readonly Dictionary<BodyDef, CachedTagParts> _partTagCache = new();
    private static readonly HashSet<BodyPartRecord> _emptyBPRSet = new();

    private sealed class CachedTagParts
    {
        public readonly BodyPartRecord CorePart;
        public readonly List<BodyPartRecord> ConsciousnessSources;
        public readonly List<BodyPartRecord> MovingCores;
        public readonly List<BodyPartRecord> BloodPumpingSources;
        public readonly List<BodyPartRecord> ConsciousnessSourceParents;

        /// <summary>每个 MovingCores 部位的权重 = 1f / 总数</summary>
        public readonly float MovingCoreWeight;

        /// <summary>每个 BloodPumpingSource 部位的权重 = 1f / 总数</summary>
        public readonly float BloodPumpingWeight;

        /// <summary>每个父部位的权重 = 1f / 总数</summary>
        public readonly float ConsciousnessSourceParentWeight;

        public CachedTagParts(BodyDef bodyDef)
        {
            var allParts = bodyDef.AllParts;
            ConsciousnessSources = new List<BodyPartRecord>(2);
            MovingCores = new List<BodyPartRecord>(4);
            BloodPumpingSources = new List<BodyPartRecord>(2);

            for (int i = 0; i < allParts.Count; i++)
            {
                var part = allParts[i];
                var tags = part.def.tags;
                if (tags.Contains(BodyPartTagDefOf.ConsciousnessSource))
                    ConsciousnessSources.Add(part);
                if (tags.Contains(BodyPartTagDefOf.MovingLimbCore))
                    MovingCores.Add(part);
                if (tags.Contains(BodyPartTagDefOf.BloodPumpingSource))
                    BloodPumpingSources.Add(part);
            }

            // 缓存核心部位（每个 BodyDef 有且只有一个）
            CorePart = bodyDef.corePart;
            // 构建 ConsciousnessSource 的所有父部位列表（迭代向上，直到核心部位，不包括核心部位，去重）
            var parentSet = new HashSet<BodyPartRecord>();
            for (int i = 0; i < ConsciousnessSources.Count; i++)
            {
                var current = ConsciousnessSources[i].parent;
                while (current != null && !current.IsCorePart)
                {
                    parentSet.Add(current);
                    current = current.parent;
                }
            }
            ConsciousnessSourceParents = [.. parentSet];
            // 按总数计算权重（游戏原版逻辑：移动效率 = 各 Core 部位的平均效率）
            MovingCoreWeight = MovingCores.Count > 0 ? 1f / MovingCores.Count : 0f;
            BloodPumpingWeight = BloodPumpingSources.Count > 0 ? 1f / BloodPumpingSources.Count : 0f;
            // 父部位损伤权重 = 1f / 父部位总数
            ConsciousnessSourceParentWeight = ConsciousnessSourceParents.Count > 0 ? 1f / ConsciousnessSourceParents.Count : 0f;
        }
    }

    private static CachedTagParts GetOrBuildTagCache(BodyDef bodyDef)
    {
        if (!_partTagCache.TryGetValue(bodyDef, out var cached))
        {
            cached = new CachedTagParts(bodyDef);
            _partTagCache[bodyDef] = cached;
        }
        return cached;
    }
}