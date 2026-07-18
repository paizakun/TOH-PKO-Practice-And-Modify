using System.Collections.Generic;
using AmongUs.GameOptions;
using UnityEngine;
using TownOfHost.Roles.Core;
using TownOfHost.Modules;
using static TownOfHost.Modules.SelfVoteManager;
using Hazel;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Crewmate;

public sealed class AmateurTeller : RoleBase, ISelfVoter
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(AmateurTeller),
            player => new AmateurTeller(player),
            CustomRoles.AmateurTeller,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            30100,
            SetupOptionItem,
            "AT",
            "#6b3ec3",
            (3, 2),
            from: From.TownOfHost_K
        );
    public AmateurTeller(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        AssignOptions();

        divination = new DivinationManager(this, GetDivinationResult);
        UsedAbilityCount = 0;
        CustomRoleManager.MarkOthers.Add(OtherArrow);
        this.RegisterAbilityMethod(nameof(UseTellAbility));
    }
    /// <summary>オプション値をフィールドへ反映する。役職本体の初期化とは分離している。</summary>
    private void AssignOptions()
    {
        Awakened = !OptAwakening.GetBool() || OptionCanTaskcount.GetInt() < 1;
        Votemode = (AbilityVoteMode)OptionVoteMode.GetValue();
        AbilityMaxUse = OptionMaximum.GetInt();
        requiredTaskCount = OptionCanTaskcount.GetInt();
        canSeeArrowAsTarget = TargetCanseeArrow.GetBool();
        canSeePlayerAsTarget = TargetCanseePlayer.GetBool();
        canSeeRole = OptionRole.GetBool();
        canUseEmergencyButton = AbilityUseTurnCanButton.GetBool();
    }

    static OptionItem OptionMaximum;
    static OptionItem OptionVoteMode;
    static OptionItem OptionRole;
    static OptionItem OptionCanTaskcount;
    static OptionItem OptAwakening;
    static OptionItem TargetCanseeArrow;
    static OptionItem TargetCanseePlayer;
    static OptionItem AbilityUseTurnCanButton;
    public AbilityVoteMode Votemode;
    static bool canUseEmergencyButton;
    static bool canSeeRole;
    static int AbilityMaxUse;
    static int requiredTaskCount;
    static bool canSeeArrowAsTarget;
    static bool canSeePlayerAsTarget;
    int UsedAbilityCount;
    bool Awakened;
    readonly DivinationManager divination;
    static HashSet<AmateurTeller> tellers = new();

    enum Option
    {
        AbilityMaxUse,
        AbilityVotemode,
        TellRole,
        AmateurTellerTargetCanseeArrow,
        AmateurTellerCanUseAbilityTurnButton,
        AmateurTellerTargetCanseePlayer
    }

    public override void Add()
    {
        tellers.Add(this);
    }
    public override void OnDestroy()
    {
        tellers.Clear();
    }
    private static void SetupOptionItem()
    {
        OptionMaximum = IntegerOptionItem.CreateWithRolePrefixedKey(RoleInfo, 10, Option.AbilityMaxUse, new(1, 99, 1), 1, false)
            .SetValueFormat(OptionFormat.Times);
        OptionVoteMode = StringOptionItem.Create(RoleInfo, 11, Option.AbilityVotemode, EnumHelper.GetAllNames<AbilityVoteMode>(), 1, false);
        OptionRole = BooleanOptionItem.Create(RoleInfo, 12, Option.TellRole, true, false);
        TargetCanseePlayer = BooleanOptionItem.Create(RoleInfo, 13, Option.AmateurTellerTargetCanseePlayer, true, false);
        TargetCanseeArrow = BooleanOptionItem.Create(RoleInfo, 14, Option.AmateurTellerTargetCanseeArrow, true, false, TargetCanseePlayer);
        AbilityUseTurnCanButton = BooleanOptionItem.Create(RoleInfo, 15, Option.AmateurTellerCanUseAbilityTurnButton, true, false);
        OptionCanTaskcount = IntegerOptionItem.Create(RoleInfo, 16, GeneralOption.requiredTaskCount, new(0, 99, 1), 5, false);
        OptAwakening = BooleanOptionItem.Create(RoleInfo, 17, GeneralOption.AbilityAwakening, false, false);
    }
    public override bool NotifyRolesCheckOtherName => true;
    public override string GetRoleStatusText(bool comms = false, bool gamelog = false)
    {
        var hasEnoughTasks = MyTaskState.HasCompletedEnoughCountOfTasks(requiredTaskCount);
        var hasUsesLeft = AbilityMaxUse > UsedAbilityCount;
        var textColor = hasEnoughTasks && hasUsesLeft ? Color.cyan : Color.gray;
        return Utils.ColorString(textColor, $"({AbilityMaxUse - UsedAbilityCount})");
    }
    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        divination.CompleteDivination();
        SendRPC();
    }
    public override bool CancelReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target, ref DontReportreson reportreson)
    {
        var isEmergencyButtonPressAsReporter = this.IsSelfEmergencyButtonPress(reporter, target);
        var isDivining = divination.IsPending;
        var cannotUseEmergencyButton = !canUseEmergencyButton;
        if (isEmergencyButtonPressAsReporter && isDivining && cannotUseEmergencyButton)
        {
            reportreson = DontReportreson.CantUseButton;
            return true;
        }
        return false;
    }
    /// <summary>占いの残り回数・必要タスク数・占い中でないか、を全て満たしているか</summary>
    bool CanUseTellAbility =>
        AbilityMaxUse > UsedAbilityCount
        && MyTaskState.HasCompletedEnoughCountOfTasks(requiredTaskCount)
        && !divination.IsPending;
    bool ISelfVoter.CanUseVoted() => Canuseability() && CanUseTellAbility;
    public override bool CheckVoteAsVoter(byte votedForId, PlayerControl voter)
    {
        if (!Canuseability()) return true;
        if (CanUseTellAbility && Is(voter))
            return HandleAbilityVote(Player, votedForId, Votemode, "Mode.Divied", "Vote.Divied", UseTellAbility);
        return true;
    }
    public void UseTellAbility(byte votedForId)
    {
        var target = PlayerCatch.GetPlayerById(votedForId);
        if (!target.IsAlive()) return;
        UsedAbilityCount++;
        divination.StartDivination(target);
        SendRPC();
        Utils.SendMessage(UtilsName.GetPlayerColor(target.PlayerId) + GetString("AmatruertellerTellMeg"), Player.PlayerId);
    }
    /// <summary>占いの結果として見せる役職を決定する。AmateurTellerは素直に対象の役職を返す。</summary>
    CustomRoles GetDivinationResult(PlayerControl target) => target.GetTellResults(Player);
    public override CustomRoles Misidentify() => Awakened ? CustomRoles.NotAssigned : CustomRoles.Crewmate;
    public override bool OnCompleteTask(uint taskid)
    {
        if (MyTaskState.HasCompletedEnoughCountOfTasks(requiredTaskCount))
        {
            if (Awakened == false)
                if (!Utils.RoleSendList.Contains(Player.PlayerId))
                    Utils.RoleSendList.Add(Player.PlayerId);
            Awakened = true;
        }
        return true;
    }
    public static string OtherArrow(PlayerControl seer, PlayerControl seen, bool isForMeeting = false)
    {
        seen ??= seer;
        if (isForMeeting) return "";
        if (!canSeePlayerAsTarget) return "";

        foreach (var tell in tellers)
        {
            if (seer.PlayerId == tell.divination.PendingTarget && seer == seen)
            {
                var ar = "";
                if (seer.GetCustomRole().GetCustomRoleTypes() is not CustomRoleTypes.Crewmate)
                {
                    if (canSeeArrowAsTarget) ar = $"\n{TargetArrow.GetArrows(seer, tell.Player.PlayerId)}";
                    return $"<color=#6b3ec3>★{ar}</color>";
                }
            }
            else if (seer.PlayerId == tell.divination.PendingTarget && seen == tell.Player)
                return "<color=#6b3ec3>★</color>";
        }
        return "";
    }
    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (isForMeeting
            && Player.IsAlive()
            && Awakened
            && seer.PlayerId == seen.PlayerId
            && Canuseability()
            && AbilityMaxUse > UsedAbilityCount
            && MyTaskState.HasCompletedEnoughCountOfTasks(requiredTaskCount))
        {
            var mes = $"<color={RoleInfo.RoleColorCode}>{(Votemode == AbilityVoteMode.SelfVote ? GetString("SelfVoteRoleInfoMeg") : GetString("NomalVoteRoleInfoMeg"))}</color>";
            return isForHud ? mes : $"<size=40%>{mes}</size>";
        }
        return "";
    }
    public override void OverrideDisplayRoleNameAsSeer(PlayerControl seen, ref bool enabled, ref Color roleColor, ref string roleText, ref bool addon)
    {
        if (!Player.IsAlive()) return;
        if (divination.PendingTarget == seen.PlayerId) return;
        if (divination.TryGetRevealedRole(seen.PlayerId, out var revealedRole))
        {
            addon = false;
            if (revealedRole.IsCrewmate() is false) Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[0]);
            if (!canSeeRole)
            {
                enabled = true;
                (roleColor, roleText) = UtilsRoleText.GetTeamDisplay(revealedRole);
            }
            else
            {
                enabled = true;
            }
        }
    }

    public void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(UsedAbilityCount);
        sender.Writer.Write(divination.PendingTarget);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        UsedAbilityCount = reader.ReadInt32();
        var target = reader.ReadByte();

        //new Target
        if (!divination.IsPending && target != byte.MaxValue)
        {
            divination.StartDivination(PlayerCatch.GetPlayerById(target));
        }
        //reset Target
        if (divination.IsPending && target == byte.MaxValue)
        {
            divination.CompleteDivination();
        }
    }
    public override void OnMurderPlayerAsTarget(MurderInfo info)
    {
        if (info.AppearanceTarget.PlayerId == Player.PlayerId && info.AppearanceKiller.PlayerId == divination.PendingTarget)
            Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[1]);
    }
    public static Dictionary<int, Achievement> achievements = new();
    [Attributes.PluginModuleInitializer]
    public static void Load()
    {
        var n1 = new Achievement(RoleInfo, 0, 1, 0, 0);
        var l1 = new Achievement(RoleInfo, 1, 1, 0, 2, true);
        achievements.Add(0, n1);
        achievements.Add(1, l1);
    }
}
