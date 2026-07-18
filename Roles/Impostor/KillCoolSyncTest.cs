using TownOfHost.Modules;
using AmongUs.GameOptions;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Impostor;

/// <summary>SyncKillCooldownの検証用。透明化ボタンでキルクールを1秒にする。</summary>
public sealed class KillCoolSyncTest : RoleBase, IImpostor, IUsePhantomButton, IKiller
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(KillCoolSyncTest),
            player => new KillCoolSyncTest(player),
            CustomRoles.KillCoolSyncTest,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            73232,
            SetupOptionItem,
            "kcst",
            "#ff1919"
        );

    public KillCoolSyncTest(PlayerControl player) : base(RoleInfo, player) { }

    static OptionItem OptionMaxSyncValue;
    static OptionItem OptionCurrentSyncValue;

    enum OptionName
    {
        KillCoolSyncTestMaxSyncValue,
        KillCoolSyncTestCurrentSyncValue
    }

    private static void SetupOptionItem()
    {
        OptionMaxSyncValue = FloatOptionItem.Create(RoleInfo, 10, OptionName.KillCoolSyncTestMaxSyncValue, new(0f, 180f, 0.5f), 1f, false).SetValueFormat(OptionFormat.Seconds);
        OptionCurrentSyncValue = FloatOptionItem.Create(RoleInfo, 11, OptionName.KillCoolSyncTestCurrentSyncValue, new(0f, 180f, 0.5f), 1f, false).SetValueFormat(OptionFormat.Seconds);
    }

    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        // AdjustKillCooldownをtrueのままにすると、この後ActionButtonPatch側が
        // 独自計算した値でMain.AllPlayerKillCooldownを上書きしてしまうため、falseにして防ぐ。
        // ただしfalseにするとActionButtonPatch側のIPPlayerKillCooldownリセットもスキップされる
        // (前回クリックからの経過時間が残り続け、2回目以降のクールが正しく計算されない)ため、
        // Eater/DoubleKillerなど自前管理の役職と同様に自分でリセットする
        AdjustKillCooldown = false;
        Player.SetMaxKillCooldown(OptionMaxSyncValue.GetFloat());
        Player.SetCurrentKillCooldown(OptionCurrentSyncValue.GetFloat());
        IUsePhantomButton.IPPlayerKillCooldown[Player.PlayerId] = 0f;
    }

    // ActionButtonPatch.CheckVanish内で無条件に呼ばれるResetKillCooldown()が
    // Options.DefaultKillCooldownで上書きしてしまうのを防ぐため、常に設定した最大値を返す。
    float IKiller.CalculateKillCooldown() => OptionMaxSyncValue.GetFloat();
}
