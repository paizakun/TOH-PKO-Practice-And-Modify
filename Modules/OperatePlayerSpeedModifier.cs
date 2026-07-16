using System.Collections.Generic;
using AmongUs.GameOptions;
using UnityEngine;

namespace TownOfHost.Modules;

public enum SpeedModifierMode
{
    Add,
    Multiply
}

public class SpeedModifierEntry
{
    public object Source;
    public SpeedModifierMode Mode;
    public float Value;
    public float RemainingDuration;
}

/// <summary>
/// 移動速度の一時効果(バフ/デバフ)を統一管理するAPI。
/// 最終速度 = BaseSpeed × (1 + Add系Valueの合計) × (Multiply系Valueの積)
/// </summary>
public static class OperatePlayerSpeedModifier
{
    private static readonly Dictionary<byte, float> BaseSpeed = new();
    private static readonly Dictionary<byte, List<SpeedModifierEntry>> SpeedModifierEntries = new();

    [Attributes.PluginModuleInitializer]
    public static void RegisterTick() => TickManager.Register(OnFixedUpdate);

    [Attributes.GameModuleInitializer]
    public static void GameReset() => Reset();

    /// <summary>役職固有の恒久的な地の速度を設定する。一時効果(Add/Multiply)には使わない。</summary>
    public static void SetBaseSpeed(PlayerControl player, float speed)
    {
        BaseSpeed[player.PlayerId] = speed;
        Recompute(player);
    }

    /// <summary>BaseSpeedに対する割合(0.1 = +10%)をduration秒間加算する。減算はvalueに負値を渡す。</summary>
    public static void Add(PlayerControl target, object source, float value, float duration)
        => AddEntry(target, source, SpeedModifierMode.Add, value, duration);

    /// <summary>Addの無期限版。RemoveBySourceで明示的に解除するまで有効。</summary>
    public static void AddIndefinite(PlayerControl target, object source, float value)
        => AddEntry(target, source, SpeedModifierMode.Add, value, float.PositiveInfinity);

    /// <summary>倍率(0.8 = 0.8倍)をduration秒間乗算する。</summary>
    public static void Multiply(PlayerControl target, object source, float multiplier, float duration)
        => AddEntry(target, source, SpeedModifierMode.Multiply, multiplier, duration);

    /// <summary>Multiplyの無期限版。RemoveBySourceで明示的に解除するまで有効。</summary>
    public static void MultiplyIndefinite(PlayerControl target, object source, float multiplier)
        => AddEntry(target, source, SpeedModifierMode.Multiply, multiplier, float.PositiveInfinity);

    /// <summary>
    /// sourceが登録した全エントリ(Add/Multiply両方、対象プレイヤー問わず)を削除する。
    /// RoleBase.Dispose()から自動的に呼ばれるため、通常は役職側から明示的に呼ぶ必要はない。
    /// </summary>
    public static void RemoveBySource(object source)
    {
        foreach (var (playerId, list) in SpeedModifierEntries)
        {
            if (list.RemoveAll(e => Equals(e.Source, source)) <= 0) continue;

            var player = playerId.GetPlayerControl();
            if (player != null) Recompute(player);
        }
    }

    private static void AddEntry(PlayerControl target, object source, SpeedModifierMode mode, float value, float duration)
    {
        if (!SpeedModifierEntries.TryGetValue(target.PlayerId, out var list))
        {
            list = new List<SpeedModifierEntry>();
            SpeedModifierEntries[target.PlayerId] = list;
        }
        list.Add(new SpeedModifierEntry
        {
            Source = source,
            Mode = mode,
            Value = value,
            RemainingDuration = duration
        });
        Recompute(target);
    }

    /// <summary>
    /// RemainingDurationの減算・期限切れエントリの除去のみを行う。
    /// FixedUpatePatch.cs等、毎フレーム処理の呼び出し元から呼ぶ想定
    /// </summary>
    public static void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!SpeedModifierEntries.TryGetValue(player.PlayerId, out var list) || list.Count == 0) return;

        var expired = false;
        for (var i = list.Count - 1; i >= 0; i--)
        {
            var entry = list[i];
            if (float.IsPositiveInfinity(entry.RemainingDuration)) continue;

            entry.RemainingDuration -= Time.fixedDeltaTime;
            if (entry.RemainingDuration > 0f) continue;

            list.RemoveAt(i);
            expired = true;
        }

        if (expired) Recompute(player);
    }

    private static void Recompute(PlayerControl player)
    {
        var baseSpeed = BaseSpeed.TryGetValue(player.PlayerId, out var bs)
            ? bs
            : Main.RealOptionsData?.GetFloat(FloatOptionNames.PlayerSpeedMod) ?? 1f;

        var addSum = 0f;
        var multiplyProduct = 1f;

        if (SpeedModifierEntries.TryGetValue(player.PlayerId, out var list))
        {
            foreach (var entry in list)
            {
                if (entry.Mode == SpeedModifierMode.Add) addSum += entry.Value;
                else multiplyProduct *= entry.Value;
            }
        }

        Main.AllPlayerSpeed[player.PlayerId] = baseSpeed * (1f + addSum) * multiplyProduct;
        player.MarkDirtySettings();
    }

    /// <summary>ゲーム終了時などに全状態をクリアする。</summary>
    public static void Reset()
    {
        BaseSpeed.Clear();
        SpeedModifierEntries.Clear();
    }

    /// <summary>デバッグ用。指定プレイヤーの現在のエントリ一覧を文字列化する。</summary>
    public static string DumpEntries(PlayerControl player)
    {
        var baseSpeed = BaseSpeed.TryGetValue(player.PlayerId, out var bs)
            ? bs
            : Main.RealOptionsData?.GetFloat(FloatOptionNames.PlayerSpeedMod) ?? 1f;

        var currentSpeed = Main.AllPlayerSpeed.GetValueOrDefault(player.PlayerId);

        if (!SpeedModifierEntries.TryGetValue(player.PlayerId, out var list) || list.Count == 0)
            return $"entries=0, BaseSpeed={baseSpeed}, CurrentAllPlayerSpeed={currentSpeed}";

        var sb = new System.Text.StringBuilder();
        sb.Append($"entries={list.Count}, BaseSpeed={baseSpeed}, CurrentAllPlayerSpeed={currentSpeed}");
        foreach (var entry in list)
        {
            sb.Append($"\n  Source={entry.Source}, Mode={entry.Mode}, Value={entry.Value}, RemainingDuration={entry.RemainingDuration}");
        }
        return sb.ToString();
    }
}
