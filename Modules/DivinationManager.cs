using System;
using System.Collections.Generic;
using TownOfHost.Roles.Core;

namespace TownOfHost.Modules
{
    /// <summary>
    /// 「対象へ投票して仮確保 → 次の通報のタイミングで結果確定 → 以後役職が見える」という
    /// 占い系役職に共通する管理コンポーネント。
    /// 役職側は占い結果を決めるメソッド(GetDivinationResult)だけ用意すればよく、
    /// 仮確保・矢印表示・確定タイミングの管理は全てこちらに任せられる。
    /// </summary>
    public class DivinationManager
    {
        private readonly RoleBase owner;
        private readonly Func<PlayerControl, CustomRoles> getDivinationResult;
        private readonly Dictionary<byte, CustomRoles> revealedRoles = new();

        /// <summary>現在占い中(結果待ち)の対象。誰もいなければbyte.MaxValue</summary>
        public byte PendingTarget { get; private set; } = byte.MaxValue;

        /// <summary>占い中かどうか</summary>
        public bool IsPending => PendingTarget != byte.MaxValue;

        /// <param name="owner">この管理コンポーネントを使う役職自身</param>
        /// <param name="getDivinationResult">占いの結果として見せる役職を決定する関数(役職ごとに差し込む)</param>
        public DivinationManager(RoleBase owner, Func<PlayerControl, CustomRoles> getDivinationResult)
        {
            this.owner = owner;
            this.getDivinationResult = getDivinationResult;
        }

        /// <summary>占いを開始する。対象を仮確保し、矢印をつける。</summary>
        public void StartDivination(PlayerControl target)
        {
            PendingTarget = target.PlayerId;
            TargetArrow.Add(target.PlayerId, owner.Player.PlayerId);
        }

        /// <summary>占い中の対象がいれば結果を確定し、可視化対象に登録する。矢印は外す。</summary>
        public void CompleteDivination()
        {
            if (!IsPending) return;
            var target = PlayerCatch.GetPlayerById(PendingTarget);
            revealedRoles[PendingTarget] = getDivinationResult(target);
            TargetArrow.Remove(PendingTarget, owner.Player.PlayerId);
            PendingTarget = byte.MaxValue;
        }

        /// <summary>対象の役職が既に開示済みなら、その役職を返す。</summary>
        public bool TryGetRevealedRole(byte targetId, out CustomRoles role) => revealedRoles.TryGetValue(targetId, out role);

        /// <summary>状態を全て初期化する。</summary>
        public void Clear()
        {
            PendingTarget = byte.MaxValue;
            revealedRoles.Clear();
        }
    }
}
