using TownOfHost.Modules;
using AmongUs.GameOptions;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Impostor;

/// <summary>SyncKillCooldownの検証用。透明化ボタンでキルクールを1秒にする。</summary>
public sealed class KillCoolSyncTest : RoleBase, IImpostor, IUsePhantomButton
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

    static OptionItem OptionSyncValue;

    enum OptionName
    {
        KillCoolSyncTestSyncValue
    }

    private static void SetupOptionItem()
        => OptionSyncValue = FloatOptionItem.Create(RoleInfo, 10, OptionName.KillCoolSyncTestSyncValue, new(0f, 180f, 0.5f), 1f, false).SetValueFormat(OptionFormat.Seconds);

    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
        => Player.SyncKillCooldown(OptionSyncValue.GetFloat());
}
