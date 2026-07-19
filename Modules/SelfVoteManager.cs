using System.Collections.Generic;
using TownOfHost.Roles.Impostor;
using TownOfHost.Roles.Madmate;
using static TownOfHost.Modules.MeetingVoteManager;
using static TownOfHost.Translator;

namespace TownOfHost.Modules
{
    public static class SelfVoteManager
    {
        /// <summary>
        /// HandleAbilityVoteで発動する能力のシグネチャ。votedForIdを受けて能力を発動する。
        /// </summary>
        public delegate void VoteAbility(byte votedForId);

        ///<summary>
        ///MeetingVoteManagerのSkip
        ///</summary>
        public static byte SkipId = Skip;
        public static Dictionary<byte, bool> CheckVote = new();
        [Attributes.GameModuleInitializer]
        public static void Init()
        {
            CheckVote.Clear();
            RoomTaskAssign.AllRoomTasker.Clear();
        }
        public static void AddSelfVotes(PlayerControl player)
        {
            CheckVote.TryAdd(player.PlayerId, false);
        }

        public enum VoteStatus
        {
            Skip,
            Self,
            Vote,
        }
        ///<summary>
        /// 自投票モードのチェック
        ///</summary>
        ///<returns>自投票モードならtrueを返す</returns>
        /// <param name="status">投票のステータスを返す</param>
        public static bool CheckSelfVoteMode(PlayerControl player, byte id, out VoteStatus status)
        {
            Check(player);
            var mode = CheckVote[player.PlayerId];
            if (player.PlayerId == id)
            {
                status = VoteStatus.Self;
                CheckVote[player.PlayerId] = !mode;
                mode = !mode;
            }
            else if (Skip == id)
                status = VoteStatus.Skip;
            else
                status = VoteStatus.Vote;
            Logger.Info($"player: {Main.AllPlayerNames[player.PlayerId]} mode: {mode} status: {status}", "SelfVoteManager");
            return mode;
        }

        private static void Check(PlayerControl player)
        {
            if (!CheckVote.ContainsKey(player.PlayerId))
            {
                AddSelfVotes(player);
                Logger.Info($"×チェックに失敗 {player.PlayerId}を追加しました", "SelfVoteManager");
            }
        }

        public static void SetMode(PlayerControl player, bool mode)
            => CheckVote[player.PlayerId] = mode;

        public static bool Canuseability()
        {
            if (MadAvenger.Skill) return false;
            if (Options.firstturnmeeting && Options.FirstTurnMeetingCantability.GetBool() && MeetingStates.FirstMeeting) return false;
            if (Assassin.NowUse) return false;

            return true;
        }

        public enum AbilityVoteMode
        {
            NomalVote,
            SelfVote,
        }

        /// <summary>
        /// 「自投票することで能力を発動する」役職のCheckVoteAsVoter内で使う共通処理。
        /// 自投票モードのON/OFF・スキップ判定を行い、次の投票で能力を発動する。
        /// ゲージ(count/max等)や覚醒などの使用可否判定は呼び出し側で行ってから渡すこと。
        /// </summary>
        /// <param name="modeMessageKey">セルフ投票モードON時に表示する役職名相当のGetStringキー(例: "Mode.Divied")</param>
        /// <param name="voteMessageKey">セルフ投票モードON時に表示する動作名相当のGetStringキー(例: "Vote.Divied")</param>
        /// <param name="useAbility">投票確定時に呼ぶ、能力発動処理</param>
        /// <returns>useAbilityが実行されたら投票キャンセル(false)、実行されなければ投票を反映する(true)</returns>
        public static bool HandleAbilityVote(PlayerControl player, byte votedForId, string modeMessageKey, string voteMessageKey, VoteAbility useAbility)
        {
            if (CheckSelfVoteMode(player, votedForId, out var status))
            {
                if (status is VoteStatus.Self)
                    Utils.SendMessage(string.Format(GetString("SkillMode"), GetString(modeMessageKey), GetString(voteMessageKey)) + GetString("VoteSkillMode"), player.PlayerId);
                if (status is VoteStatus.Skip)
                    Utils.SendMessage(GetString("VoteSkillFin"), player.PlayerId);
                if (status is VoteStatus.Vote)
                    useAbility(votedForId);
                SetMode(player, status is VoteStatus.Self);
                return false;
            }
            return true;
        }
    }
}