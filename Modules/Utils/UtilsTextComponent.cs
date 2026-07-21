using System.Linq;
using System.Text;
using System.Collections.Generic;

using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Impostor;
using TownOfHost.Roles.Neutral;
using TownOfHost.Roles.AddOns.Common;
using TownOfHost.Roles.AddOns.Impostor;
using TownOfHost.Roles.AddOns.Neutral;

using static TownOfHost.Utils;
using static TownOfHost.UtilsRoleText;

namespace TownOfHost
{
    /// <summary>
    /// 名前表示テキスト(役職名・Mark・Suffix・Subroleリスト)の共通構築ロジック。
    /// UtilsRoleText / UtilsNotifyRoles に分散していた組み立て処理をここに集約する。
    /// </summary>
    public static class UtilsTextComponent
    {
        /// <summary>
        /// seer自身の名前に付けるMark/Suffixを構築する。
        /// UtilsNotifyRoles.NotifyRoles/NotifyMeetingRolesで重複していたロジックを共通化。
        /// </summary>
        public static (string mark, string suffix) BuildSelfDecoration(PlayerControl seer, bool isForMeeting)
        {
            var role = seer.GetCustomRole();
            var seerRole = seer.GetRoleClass();
            var amnesiaCheck = Amnesia.CheckAbility(seer);
            var isMisidentify = seer.GetMisidentify(out _);
            var seerConnecting = seer.Is(CustomRoles.Connecting);
            var seerIsAlive = seer.IsAlive();

            var mark = new StringBuilder(20);
            if (amnesiaCheck && !isMisidentify)
                mark.Append(seerRole?.GetMark(seer, isForMeeting: isForMeeting) ?? "");

            mark.Append(CustomRoleManager.GetMarkOthers(seer, isForMeeting: isForMeeting));

            var lover = seer.GetLoverRole();
            if (lover is not CustomRoles.NotAssigned and not CustomRoles.OneLove) mark.Append(ColorString(GetRoleColor(lover), "♥"));

            if ((seerConnecting && role is not CustomRoles.WolfBoy)
            || (seerConnecting && !seerIsAlive)) mark.Append(ColorString(GetRoleColor(CustomRoles.Connecting), "Ψ"));

            if (!isForMeeting && Options.CurrentGameMode == CustomGameMode.TaskBattle)
            {
                TaskBattle.GetMark(seer, null, ref mark);
            }

            var suffix = new StringBuilder(20);
            if (amnesiaCheck && !isMisidentify)
                suffix.Append(seerRole?.GetLowerText(seer, isForMeeting: isForMeeting) ?? "");
            suffix.Append(CustomRoleManager.GetLowerTextOthers(seer, isForMeeting: isForMeeting));

            if (!isForMeeting && Options.CanseeVoteresult.GetBool() && MeetingVoteManager.Voteresult != "")
            {
                if (suffix.ToString() != "") suffix.Append('\n');
                suffix.Append("<#ffffff><size=75%>" + MeetingVoteManager.Voteresult + "</color></size>");
            }

            if (amnesiaCheck)
                suffix.Append(seerRole?.GetSuffix(seer, isForMeeting: isForMeeting) ?? "");
            suffix.Append(CustomRoleManager.GetSuffixOthers(seer, isForMeeting: isForMeeting));

            return (mark.ToString(), suffix.ToString());
        }

        /// <summary>
        /// seerから見たtargetの名前に付けるMark/Suffixを構築する。
        /// UtilsNotifyRoles.NotifyRoles/NotifyMeetingRolesで重複していたロジックを共通化。
        /// タスクフェーズ/会議フェーズで恋人マーク表示条件がわずかに異なる(OneLoveの扱い)ため、
        /// isForMeetingで分岐して従来の挙動を再現している。
        /// </summary>
        public static (string mark, string suffix) BuildTargetDecoration(PlayerControl seer, PlayerControl target, bool isForMeeting)
        {
            var role = seer.GetCustomRole();
            var seerRole = seer.GetRoleClass();
            var amnesiaCheck = Amnesia.CheckAbility(seer);
            var seerConnecting = seer.Is(CustomRoles.Connecting);
            var seerIsAlive = seer.IsAlive();
            var seerSubrole = seer.GetCustomSubRoles();

            var mark = new StringBuilder(20);
            mark.Append(CustomRoleManager.GetMarkOthers(seer, target, isForMeeting));

            var seerLoverRole = seer.GetLoverRole();
            var targetLoverRole = target.GetLoverRole();
            var seerIsOneLove = seerSubrole.Contains(CustomRoles.OneLove);
            var targetSubrole = target.GetCustomSubRoles();
            var targetIsOneLove = targetSubrole.Contains(CustomRoles.OneLove);
            var targetLoverRoleValid = targetLoverRole != CustomRoles.NotAssigned && (!isForMeeting || targetLoverRole != CustomRoles.OneLove);
            if (seerLoverRole == targetLoverRole && seer.IsLovers() && !seerIsOneLove)
                mark.Append(ColorString(GetRoleColor(seerLoverRole), "♥"));
            else if (seer.Data.IsDead && !seer.Is(targetLoverRole) && targetLoverRoleValid && !seerIsOneLove)
                mark.Append(ColorString(GetRoleColor(targetLoverRole), "♥"));

            if ((seerIsOneLove && targetIsOneLove)
            || ((seer.Data.IsDead || seerIsOneLove) && target.PlayerId == Lovers.OneLovePlayer.BelovedId)
            )
                mark.Append("<#ff7961>♡</color>");

            if (seerConnecting && targetSubrole.Contains(CustomRoles.Connecting) && (role is not CustomRoles.WolfBoy || !seerIsAlive)
            || (seer.Data.IsDead && !seerConnecting && targetSubrole.Contains(CustomRoles.Connecting))
            ) //狼少年じゃないか死亡なら処理
                mark.Append($"<#96514d>Ψ</color>");

            //インサイダーモードタスク表示
            if (Options.InsiderModeCanSeeTask.GetBool())
            {
                if (target.GetPlayerTaskState() != null && target.GetPlayerTaskState().AllTasksCount > 0)
                {
                    if (role.IsImpostor())
                    {
                        mark.Append($"<yellow>({target.GetPlayerTaskState().CompletedTasksCount}/{target.GetPlayerTaskState().GetNeedCountOrAll()})</color>");
                    }
                }
            }

            var suffix = new StringBuilder(20);
            suffix.Append(CustomRoleManager.GetLowerTextOthers(seer, target, isForMeeting: isForMeeting));
            suffix.Append(CustomRoleManager.GetSuffixOthers(seer, target, isForMeeting: isForMeeting));
            // 空でなければ先頭に改行を挿入
            if (suffix.Length > 0)
                suffix.Insert(0, "\r\n");

            if (amnesiaCheck)
            {
                mark.Append(seerRole?.GetMark(seer, target, isForMeeting) ?? "");
                suffix.Append(seerRole?.GetSuffix(seer, target, isForMeeting: isForMeeting) ?? "");

                if (targetSubrole.Contains(CustomRoles.Workhorse))
                {
                    if (((seerRole as Alien)?.mode == Alien.AlienMode.ProgressKiller && Alien.ProgressWorkhorseseen)
                    || ((seerRole as JackalAlien)?.mode == Alien.AlienMode.ProgressKiller == true && JackalAlien.ProgressWorkhorseseen)
                    || (role is CustomRoles.ProgressKiller && ProgressKiller.ProgressWorkhorseseen)
                    || ((seerRole as AlienHijack)?.mode == Alien.AlienMode.ProgressKiller && Alien.ProgressWorkhorseseen))
                    {
                        mark.Append($"<#0000ff>♦</color>");
                    }
                }
            }

            return (mark.ToString(), suffix.ToString());
        }

        /// <summary>
        /// RoleAddAddons(GiveXxxフラグ)とLastImpostor/LastNeutralから、対象のSubroleリストを構築する。
        /// UtilsRoleText.GetTrueRoleNameDataとGetSubRolesTextで同一のロジックだったため共通化。
        /// </summary>
        public static List<CustomRoles> BuildSubRolesFromAddon(PlayerState state, PlayerControl player)
        {
            var Subrole = new List<CustomRoles>(state.SubRoles);
            if (Subrole == null) Subrole.Add(CustomRoles.NotAssigned);
            if (state.MainRole != CustomRoles.NotAssigned && state != null && player != null)
                if (RoleAddAddons.GetRoleAddon(state.MainRole, out var data, player, subrole: CustomRoles.NotAssigned))
                {
                    if (data != null)
                    {
                        if (data.GiveGuesser.GetBool()) Subrole.Add(CustomRoles.Guesser);
                        if (data.GiveWatching.GetBool()) Subrole.Add(CustomRoles.Watching);
                        if (data.GivePlusVote.GetBool()) Subrole.Add(CustomRoles.PlusVote);
                        if (data.GiveTiebreaker.GetBool()) Subrole.Add(CustomRoles.Tiebreaker);
                        if (data.GiveAutopsy.GetBool()) Subrole.Add(CustomRoles.Autopsy);
                        if (data.GiveRevenger.GetBool()) Subrole.Add(CustomRoles.Revenger);
                        if (data.GiveSpeeding.GetBool()) Subrole.Add(CustomRoles.Speeding);
                        if (data.GiveGuarding.GetBool()) Subrole.Add(CustomRoles.Guarding);
                        if (data.GiveManagement.GetBool()) Subrole.Add(CustomRoles.Management);
                        if (data.GiveSeeing.GetBool()) Subrole.Add(CustomRoles.Seeing);
                        if (data.GiveOpener.GetBool()) Subrole.Add(CustomRoles.Opener);
                        //if (data.GiveAntiTeleporter.GetBool()) Subrole.Add(CustomRoles.AntiTeleporter);
                        if (!data.IsImpostor)
                        {
                            if (data.GiveLighting.GetBool()) Subrole.Add(CustomRoles.Lighting);
                            if (data.GiveMoon.GetBool()) Subrole.Add(CustomRoles.Moon);
                        }
                        if (data.GiveNotvoter.GetBool()) Subrole.Add(CustomRoles.Notvoter);
                        if (data.GiveElector.GetBool()) Subrole.Add(CustomRoles.Elector);
                        if (data.GiveInfoPoor.GetBool()) Subrole.Add(CustomRoles.InfoPoor);
                        if (data.GiveNonReport.GetBool()) Subrole.Add(CustomRoles.NonReport);
                        if (data.GiveTransparent.GetBool()) Subrole.Add(CustomRoles.Transparent);
                        if (data.GiveWater.GetBool()) Subrole.Add(CustomRoles.Water);
                        if (data.GiveClumsy.GetBool()) Subrole.Add(CustomRoles.Clumsy);
                        if (data.GiveSlacker.GetBool()) Subrole.Add(CustomRoles.Slacker);
                        if (data.GiveStamina.GetBool()) Subrole.Add(CustomRoles.Stamina);
                        if (data.GiveJumbo.GetBool()) Subrole.Add(CustomRoles.Jumbo);
                        if (data.GiveSunglasses.GetBool()) Subrole.Add(CustomRoles.Sunglasses);
                        if (data.GiveSecurer.GetBool() && Securer.CanBeAssigned(player)) Subrole.Add(CustomRoles.Securer);
                        if (data.GiveSealer.GetBool() && Sealer.CanBeAssigned(player)) Subrole.Add(CustomRoles.Sealer);
                    }
                    if (state.SubRoles.Any(x => x is CustomRoles.LastImpostor))
                    {
                        if (LastImpostor.GiveAutopsy.GetBool()) Subrole.Add(CustomRoles.Autopsy);
                        if (LastImpostor.giveguesser) Subrole.Add(CustomRoles.Guesser);
                        if (LastImpostor.GiveManagement.GetBool()) Subrole.Add(CustomRoles.Management);
                        if (LastImpostor.GiveSeeing.GetBool()) Subrole.Add(CustomRoles.Seeing);
                        if (LastImpostor.GiveTiebreaker.GetBool()) Subrole.Add(CustomRoles.Tiebreaker);
                        if (LastImpostor.GiveWatching.GetBool()) Subrole.Add(CustomRoles.Watching);
                    }
                    if (state.SubRoles.Any(x => x is CustomRoles.LastNeutral))
                    {
                        if (LastNeutral.GiveAutopsy.GetBool()) Subrole.Add(CustomRoles.Autopsy);
                        if (LastNeutral.GiveGuesser.GetBool()) Subrole.Add(CustomRoles.Guesser);
                        if (LastNeutral.GiveManagement.GetBool()) Subrole.Add(CustomRoles.Management);
                        if (LastNeutral.GiveSeeing.GetBool()) Subrole.Add(CustomRoles.Seeing);
                        if (LastNeutral.GiveTiebreaker.GetBool()) Subrole.Add(CustomRoles.Tiebreaker);
                        if (LastNeutral.GiveWatching.GetBool()) Subrole.Add(CustomRoles.Watching);
                    }
                }
            return Subrole;
        }
    }
}
