using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using System.Reflection;
using static TownOfHost.Translator;

namespace TownOfHost.Roles.Crewmate;

public sealed class Santa : RoleBase, IKiller
{
    bool IKiller.IsKiller => true;
    bool IKiller.CanKill => true;

    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Santa),
            player => new Santa(player),
            CustomRoles.Santa,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            66100,
            SetupOptionItem,
            "st",
            "#f29c9f",
            (6, 0),
            from: From.SuperNewRoles,
            isDesyncImpostor: true
        );

    public Santa(PlayerControl player)
        : base(RoleInfo, player)
    {
        KillCooldown = OptKillCooldown.GetFloat();
        taskCompleted = false;
        giftCount = 0;
    }

    static OptionItem OptKillCooldown;
    static float KillCooldown;

    static OptionItem OptBalancerRate;
    static OptionItem OptSheriffRate;
    static OptionItem OptLighterRate;
    static OptionItem OptUltraStarRate;
    static OptionItem OptExpressRate;
    static OptionItem OptNiceGuesserRate;
    static OptionItem OptGiftLimit;
    static OptionItem OptCanGiftLovers;
    static OptionItem OptCanGiftMadmate;

    bool taskCompleted;
    int giftCount;

    private enum OptionName
    {
        SantaGiftRateBalancer,
        SantaGiftRateSheriff,
        SantaGiftRateLighter,
        SantaGiftRateUltraStar,
        SantaGiftRateExpress,
        SantaGiftRateNiceGuesser,
        SantaGiftLimit,
        SantaCanGiftLovers,
        SantaCanGiftMadmate,
    }

    private static readonly Dictionary<byte, int> RememberedColorByPlayerId = new();

    private static void SetupOptionItem()
    {
        OptBalancerRate = IntegerOptionItem.Create(
            RoleInfo, 11, OptionName.SantaGiftRateBalancer,
            new(0, 100, 5), 20, false
        ).SetValueFormat(OptionFormat.Percent);

        OptSheriffRate = IntegerOptionItem.Create(
            RoleInfo, 12, OptionName.SantaGiftRateSheriff,
            new(0, 100, 5), 20, false
        ).SetValueFormat(OptionFormat.Percent);

        OptLighterRate = IntegerOptionItem.Create(
            RoleInfo, 13, OptionName.SantaGiftRateLighter,
            new(0, 100, 5), 20, false
        ).SetValueFormat(OptionFormat.Percent);

        OptUltraStarRate = IntegerOptionItem.Create(
            RoleInfo, 14, OptionName.SantaGiftRateUltraStar,
            new(0, 100, 5), 20, false
        ).SetValueFormat(OptionFormat.Percent);

        OptExpressRate = IntegerOptionItem.Create(
            RoleInfo, 15, OptionName.SantaGiftRateExpress,
            new(0, 100, 5), 20, false
        ).SetValueFormat(OptionFormat.Percent);

        OptNiceGuesserRate = IntegerOptionItem.Create(
            RoleInfo, 16, OptionName.SantaGiftRateNiceGuesser,
            new(0, 100, 5), 20, false
        ).SetValueFormat(OptionFormat.Percent);

        OptKillCooldown = FloatOptionItem.Create(
            RoleInfo, 10, "SantaKillCooldown",
            new(0.5f, 60f, 0.5f), 25f, false
        ).SetValueFormat(OptionFormat.Seconds);
        OptGiftLimit = IntegerOptionItem.Create(
            RoleInfo, 17, OptionName.SantaGiftLimit,
            new(1, 100, 1), 3, false
        ).SetValueFormat(OptionFormat.Times);

        OptCanGiftLovers = BooleanOptionItem.Create(
            RoleInfo, 18, OptionName.SantaCanGiftLovers,
            false, false
        );

        OptCanGiftMadmate = BooleanOptionItem.Create(
            RoleInfo, 19, OptionName.SantaCanGiftMadmate,
            false, false
        );

        OverrideTasksData.Create(RoleInfo, 200);
    }

    public float CalculateKillCooldown() => KillCooldown;

    private static int GetGiftRate(CustomRoles role) => role switch
    {
        CustomRoles.Balancer => OptBalancerRate?.GetInt() ?? 0,
        CustomRoles.Sheriff => OptSheriffRate?.GetInt() ?? 0,
        CustomRoles.Lighter => OptLighterRate?.GetInt() ?? 0,
        CustomRoles.UltraStar => OptUltraStarRate?.GetInt() ?? 0,
        CustomRoles.Express => OptExpressRate?.GetInt() ?? 0,
        CustomRoles.NiceGuesser => OptNiceGuesserRate?.GetInt() ?? 0,
        _ => 0
    };

    private static CustomRoles RollGiftRole(CustomRoles[] giftRoles)
    {
        var weightedRoles = giftRoles
            .Select(role =>
            {
                var weight = GetGiftRate(role);
                if (weight < 0) weight = 0;
                if (weight > 100) weight = 100;
                return (Role: role, Weight: weight);
            })
            .Where(x => x.Weight > 0)
            .ToArray();

        if (weightedRoles.Length == 0)
            return giftRoles[IRandom.Instance.Next(giftRoles.Length)];

        var totalWeight = weightedRoles.Sum(x => x.Weight);
        var roll = IRandom.Instance.Next(totalWeight);
        var acc = 0;

        foreach (var entry in weightedRoles)
        {
            acc += entry.Weight;
            if (roll < acc) return entry.Role;
        }

        return weightedRoles[weightedRoles.Length - 1].Role;
    }

    // ★ タスク完了後・配布上限未達成のみキルボタンを使える
    public bool CanUseKillButton()
    {
        if (!Player.IsAlive() || !taskCompleted) return false;
        var limit = OptGiftLimit?.GetInt() ?? 3;
        if (limit == 0) return true; // 0 = 無制限
        return giftCount < limit;
    }
    public bool CanUseSabotageButton() => false;
    public bool CanUseImpostorVentButton() => false;

    public override RoleTypes? AfterMeetingRole => taskCompleted ? RoleTypes.Impostor : RoleTypes.Crewmate;

    public override bool OnCompleteTask(uint taskid)
    {
        if (!Player.IsAlive()) return true;

        if (IsTaskFinished && !taskCompleted)
        {
            taskCompleted = true;

            if (!AmongUsClient.Instance.AmHost) return true;

            Player.RpcSetRoleDesync(RoleTypes.Impostor, Player.GetClientId());
            Player.ResetKillCooldown();
            Player.SetKillCooldown();
            UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
        }
        return true;
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(false);
    }

    public override void ChengeRoleAdd()
    {
        base.ChengeRoleAdd();
        if (taskCompleted && Player.IsAlive() && AmongUsClient.Instance.AmHost)
        {
            Player.RpcSetRoleDesync(RoleTypes.Impostor, Player.GetClientId());
        }
    }

    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        var (killer, target) = info.AttemptTuple;
        info.DoKill = false;

        if (target.PlayerId == killer.PlayerId) return;

        var targetRoleType = target.GetCustomRole().GetCustomRoleTypes();
        bool isLovers = target.Is(CustomRoles.Lovers) || target.Is(CustomRoles.MadonnaLovers) || target.Is(CustomRoles.OneLove);
        bool isMadmate = targetRoleType == CustomRoleTypes.Madmate;
        bool isCrew = targetRoleType == CustomRoleTypes.Crewmate;
        bool canGift = isCrew;
        if (!canGift && isLovers && (OptCanGiftLovers?.GetBool() ?? false)) canGift = true;
        if (!canGift && isMadmate && (OptCanGiftMadmate?.GetBool() ?? false)) canGift = true;

        if (!canGift)
        {
            PlayerState.GetByPlayerId(Player.PlayerId).DeathReason = CustomDeathReason.Suicide;
            killer.RpcMurderPlayerV2(killer);
            return;
        }
        var limit = OptGiftLimit?.GetInt() ?? 3;
        if (limit > 0 && giftCount >= limit) return;

        CustomRoles[] giftRoles =
        {
            CustomRoles.Balancer,
            CustomRoles.Sheriff,
            CustomRoles.Lighter,
            CustomRoles.UltraStar,
            CustomRoles.Express,
            CustomRoles.NiceGuesser,
        };

        var role = RollGiftRole(giftRoles);
        var beforeRole = target.GetCustomRole();

        if (role == CustomRoles.UltraStar && beforeRole != CustomRoles.UltraStar)
            RememberedColorByPlayerId[target.PlayerId] = target.Data.DefaultOutfit.ColorId;

        bool resetExpressSpeed = beforeRole == CustomRoles.Express && role != CustomRoles.Express;
        if (resetExpressSpeed)
            Main.AllPlayerSpeed[target.PlayerId] = Main.NormalOptions.PlayerSpeedMod;

        if (!Utils.RoleSendList.Contains(target.PlayerId))
            Utils.RoleSendList.Add(target.PlayerId);

        target.RpcSetCustomRole(role, log: null);

        if (beforeRole == CustomRoles.UltraStar &&
            role != CustomRoles.UltraStar &&
            RememberedColorByPlayerId.TryGetValue(target.PlayerId, out var originalColorId))
        {
            target.RpcSetColor((byte)originalColorId);
            RememberedColorByPlayerId.Remove(target.PlayerId);
        }

        if (role == CustomRoles.UltraStar)
        {
            var field = typeof(UltraStar).GetField("CanseeAllplayer", BindingFlags.NonPublic | BindingFlags.Static);
            field?.SetValue(null, true);
        }

        if (resetExpressSpeed)
            UtilsOption.MarkEveryoneDirtySettings();

        giftCount++;

        killer.ResetKillCooldown();
        killer.SetKillCooldown();
        killer.RpcResetAbilityCooldown();

        _ = new LateTask(() => UtilsNotifyRoles.NotifyRoles(ForceLoop: true), 0.2f, "Santa Gift");
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (!taskCompleted) return "";
        var limit = OptGiftLimit?.GetInt() ?? 3;
        if (limit == 0) return $"<color=#f29c9f>({giftCount})</color>";
        return $"<color=#f29c9f>({giftCount}/{limit})</color>";
    }

    public bool OverrideKillButtonText(out string text)
    {
        text = "プレゼント";
        return true;
    }

    public bool OverrideKillButton(out string text)
    {
        text = "Santa_Gift";
        return true;
    }
}