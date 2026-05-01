using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Impostor;

public sealed class Rocket : RoleBase, IImpostor, IKiller, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Rocket),
            player => new Rocket(player),
            CustomRoles.Rocket,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            86350,
            SetupOptionItem,
            "rkt",
            OptionSort: (3, 14),
            from: From.SuperNewRoles
        );

    public Rocket(PlayerControl player)
        : base(RoleInfo, player)
    {
        InitialGrabCooldown = OptionInitialGrabCooldown.GetFloat();
        SubsequentGrabCooldown = OptionSubsequentGrabCooldown.GetFloat();
        LaunchCooldown = OptionLaunchCooldown.GetFloat();
        LaunchAfterMeeting = OptionLaunchAfterMeeting.GetBool();

        GrabbedPlayers = new();
        launchPending = false;
        killCDOverride = InitialGrabCooldown;
        phantomCDTimer = LaunchCooldown;
    }

    static OptionItem OptionInitialGrabCooldown;
    static float InitialGrabCooldown;
    static OptionItem OptionSubsequentGrabCooldown;
    static float SubsequentGrabCooldown;
    static OptionItem OptionLaunchCooldown;
    static float LaunchCooldown;
    static OptionItem OptionLaunchAfterMeeting;
    static bool LaunchAfterMeeting;

    enum OptionName
    {
        RocketInitialGrabCooldown,
        RocketSubsequentGrabCooldown,
        RocketLaunchCooldown,
        RocketLaunchAfterMeeting,
    }

    public readonly List<PlayerControl> GrabbedPlayers;
    bool launchPending;
    float killCDOverride;
    float phantomCDTimer;
    int snapFrame = 0;

    static void SetupOptionItem()
    {
        OptionInitialGrabCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.RocketInitialGrabCooldown,
            new(2.5f, 60f, 2.5f), 15f, false).SetValueFormat(OptionFormat.Seconds);
        OptionSubsequentGrabCooldown = FloatOptionItem.Create(RoleInfo, 11, OptionName.RocketSubsequentGrabCooldown,
            new(0f, 60f, 2.5f), 5f, false).SetValueFormat(OptionFormat.Seconds);
        OptionLaunchCooldown = FloatOptionItem.Create(RoleInfo, 12, OptionName.RocketLaunchCooldown,
            new(0f, 60f, 2.5f), 10f, false).SetValueFormat(OptionFormat.Seconds);
        OptionLaunchAfterMeeting = BooleanOptionItem.Create(RoleInfo, 13, OptionName.RocketLaunchAfterMeeting,
            false, false);
    }

    public float CalculateKillCooldown() => killCDOverride;
    public bool CanUseSabotageButton() => true;
    public bool CanUseImpostorVentButton() => true;
    public bool CanUseKillButton() => Player.IsAlive();
    public override bool CanClickUseVentButton => true;

    bool IUsePhantomButton.IsPhantomRole => true;
    bool IUsePhantomButton.IsresetAfterKill => false;

    public override void Add()
    {
        killCDOverride = InitialGrabCooldown;
        Main.AllPlayerKillCooldown[Player.PlayerId] = killCDOverride;
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = LaunchCooldown;
    }

    // ★ IKiller インターフェース経由（override ではない）
    public bool OnCheckMurderAsKiller(MurderInfo info)
    {
        if (!AmongUsClient.Instance.AmHost) return true;

        var target = info.AttemptTarget;
        if (target == null) return true;

        // ★ すでに掴んでいる人はキルしない
        if (GrabbedPlayers.Contains(target)) return false;

        // ★ キルせずに掴む
        GrabPlayer(target);
        info.DoKill = false;
        return false;
    }

    void GrabPlayer(PlayerControl target)
    {
        GrabbedPlayers.Add(target);

        var targetState = PlayerState.GetByPlayerId(target.PlayerId);
        targetState.CanMove = false;
        target.MarkDirtySettings();

        killCDOverride = SubsequentGrabCooldown;
        Main.AllPlayerKillCooldown[Player.PlayerId] = killCDOverride;
        Player.SetKillCooldown(killCDOverride);

        Player.KillFlash();

        SendRpc();
        UtilsNotifyRoles.NotifyRoles();

        Logger.Info($"{Player.Data.GetLogPlayerName()} が {target.Data.GetLogPlayerName()} を掴んだ", "Rocket");
    }

    void ReleasePlayer(PlayerControl target)
    {
        if (!GrabbedPlayers.Remove(target)) return;
        var targetState = PlayerState.GetByPlayerId(target.PlayerId);
        targetState.CanMove = true;
        target.MarkDirtySettings();
    }

    void ReleaseAll()
    {
        foreach (var p in GrabbedPlayers.ToArray())
        {
            var s = PlayerState.GetByPlayerId(p.PlayerId);
            s.CanMove = true;
            p.MarkDirtySettings();
        }
        GrabbedPlayers.Clear();
    }

    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = false;
        ResetCooldown = true;

        if (!Player.IsAlive()) return;
        if (GrabbedPlayers.Count == 0) return;

        LaunchAll();

        killCDOverride = InitialGrabCooldown;
        Main.AllPlayerKillCooldown[Player.PlayerId] = killCDOverride;
        Player.SetKillCooldown(killCDOverride);

        SendRpc();
    }

    void LaunchAll()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        var launchPos = Player.GetTruePosition();

        foreach (var target in GrabbedPlayers.ToArray())
        {
            if (target == null || !target.IsAlive()) continue;

            Vector2 upPos = launchPos + new Vector2(0f, 50f);
            target.NetTransform.SnapTo(upPos);
            ushort sid = (ushort)(target.NetTransform.lastSequenceId + 2U);
            var snapWriter = AmongUsClient.Instance.StartRpcImmediately(
                target.NetTransform.NetId, (byte)RpcCalls.SnapTo, SendOption.Reliable);
            NetHelpers.WriteVector2(upPos, snapWriter);
            snapWriter.Write(sid);
            AmongUsClient.Instance.FinishRpcImmediately(snapWriter);

            PlayerState.GetByPlayerId(target.PlayerId).DeathReason = CustomDeathReason.etc;
            target.RpcExileV3();
            PlayerState.GetByPlayerId(target.PlayerId).SetDead();

            UtilsGameLog.AddGameLog("Rocket",
                $"{UtilsName.GetPlayerColor(Player)}が{UtilsName.GetPlayerColor(target)}を打ち上げた");

            NotifyLaunchToNearby(target, launchPos);
        }

        ReleaseAll();
        UtilsNotifyRoles.NotifyRoles();
    }

    void NotifyLaunchToNearby(PlayerControl launched, Vector2 launchPos)
    {
        const float notifyRange = 5f;
        foreach (var pc in PlayerCatch.AllAlivePlayerControls)
        {
            if (pc.PlayerId == Player.PlayerId) continue;
            if (Vector2.Distance(pc.GetTruePosition(), launchPos) > notifyRange) continue;

            pc.KillFlash();

            _ = new LateTask(() =>
            {
                Utils.SendMessage(
                    $"<color=#ff6600>🚀 {UtilsName.GetPlayerColor(launched, true)} が打ち上げられた！</color>",
                    pc.PlayerId);
            }, 0.3f, $"Rocket.NotifyLaunch.{pc.PlayerId}", true);
        }
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!GameStates.IsInTask) return;
        if (GrabbedPlayers.Count == 0) return;
        if (!Player.IsAlive())
        {
            ReleaseAll();
            SendRpc();
            return;
        }

        foreach (var p in GrabbedPlayers.ToArray())
        {
            if (p == null || !p.IsAlive())
                ReleasePlayer(p);
        }

        snapFrame++;
        if (snapFrame % 3 != 0) return;

        var myPos = Player.GetTruePosition();
        foreach (var grabbed in GrabbedPlayers.ToArray())
        {
            if (grabbed == null || !grabbed.IsAlive()) continue;
            if (grabbed.MyPhysics.Animations.IsPlayingAnyLadderAnimation()) continue;

            grabbed.NetTransform.SnapTo(myPos);
            ushort sid = (ushort)(grabbed.NetTransform.lastSequenceId + 2U);
            var writer = AmongUsClient.Instance.StartRpcImmediately(
                grabbed.NetTransform.NetId, (byte)RpcCalls.SnapTo, SendOption.None);
            NetHelpers.WriteVector2(myPos, writer);
            writer.Write(sid);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
    }

    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (GrabbedPlayers.Count > 0)
            launchPending = LaunchAfterMeeting;
        foreach (var p in GrabbedPlayers.ToArray())
        {
            var s = PlayerState.GetByPlayerId(p.PlayerId);
            s.CanMove = true;
            p.MarkDirtySettings();
        }
    }

    public override void AfterMeetingTasks()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive())
        {
            ReleaseAll();
            SendRpc();
            return;
        }

        if (launchPending && GrabbedPlayers.Count > 0)
        {
            _ = new LateTask(() =>
            {
                LaunchAll();
                killCDOverride = InitialGrabCooldown;
                Main.AllPlayerKillCooldown[Player.PlayerId] = killCDOverride;
                Player.SetKillCooldown(killCDOverride);
                SendRpc();
            }, 1.5f, "Rocket.LaunchAfterMeeting", true);
        }
        else
        {
            foreach (var p in GrabbedPlayers.ToArray())
            {
                var s = PlayerState.GetByPlayerId(p.PlayerId);
                s.CanMove = false;
                p.MarkDirtySettings();
            }
        }
        launchPending = false;

        AURoleOptions.PhantomCooldown = LaunchCooldown;
        Player.RpcResetAbilityCooldown();
    }

    public override void OnStartMeeting()
    {
        launchPending = false;
    }

    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (!Is(seer)) return "";
        if (GrabbedPlayers.Contains(seen))
            return "<color=#ff6600>🚀</color>";
        return "";
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seer.PlayerId != seen.PlayerId || !Player.IsAlive()) return "";
        if (isForMeeting) return "";

        string size = isForHud ? "" : "<size=60%>";
        string color = RoleInfo.RoleColorCode;
        int count = GrabbedPlayers.Count;

        if (count == 0)
            return $"{size}<color={color}>キルボタン → 掴む | ファントム → 打ち上げ</color>";
        return $"{size}<color={color}>掴み中: {count}人 | ファントムボタン → 打ち上げ！</color>";
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (!Player.IsAlive()) return "";
        int count = GrabbedPlayers.Count;
        if (count == 0) return "";
        return $"<color=#ff6600>({count}人)</color>";
    }

    void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(GrabbedPlayers.Count);
        foreach (var p in GrabbedPlayers)
            sender.Writer.Write(p?.PlayerId ?? byte.MaxValue);
        sender.Writer.Write(launchPending);
        sender.Writer.Write(killCDOverride);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        int count = reader.ReadInt32();
        GrabbedPlayers.Clear();
        for (int i = 0; i < count; i++)
        {
            var id = reader.ReadByte();
            var pc = PlayerCatch.GetPlayerById(id);
            if (pc != null) GrabbedPlayers.Add(pc);
        }
        launchPending = reader.ReadBoolean();
        killCDOverride = reader.ReadSingle();
    }

    public override string GetAbilityButtonText() =>
        GrabbedPlayers.Count > 0 ? "打ち上げ" : "掴む";

    public override bool OverrideAbilityButton(out string text)
    {
        text = GrabbedPlayers.Count > 0 ? "Rocket_Launch" : "Rocket_Grab";
        return true;
    }
}