using TownOfHost.Modules;
using AmongUs.GameOptions;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Impostor;

/// <summary>
/// OperatePlayerSpeedModifierの動作検証用テスト役職。
/// 透明化ボタン(バニラのPhantomアビリティボタン。TownOfHostによりキルクール同期
/// トリガーに転用されている)を使用すると、自分自身に10秒間BaseSpeed+100%(1.0、2倍速)を付与する。
/// </summary>
public sealed class SpeedAdderTest : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(SpeedAdderTest),
            player => new SpeedAdderTest(player),
            CustomRoles.SpeedAdderTest,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            73231,
            SetupOptionItem,
            "sat",
            "#ff1919"
        );

    public SpeedAdderTest(PlayerControl player) : base(RoleInfo, player) { }

    private static void SetupOptionItem() { }

    public float CalculateKillCooldown() => Options.DefaultKillCooldown;
    public bool CanUseSabotageButton() => false;
    public bool CanUseImpostorVentButton() => true;

    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        OperatePlayerSpeedModifier.Add(Player, this, 1.0f, 10f);
        Logger.Info(OperatePlayerSpeedModifier.DumpEntries(Player), "SpeedAdderTest");
    }
}
