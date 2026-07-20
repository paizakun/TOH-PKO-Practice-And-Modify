using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TownOfHost.Roles.Core;

namespace TownOfHost.Modules
{
    /// <summary>
    /// 役職の能力発動メソッドを動的にHarmonyパッチで横取りし、対象の<see cref="RoleBase.AbilityEnabled"/>が
    /// falseの間は本体を実行させない仕組み。
    /// 呼び出し元は通常通りメソッドを呼ぶだけでよく、審査の存在を意識する必要はない。
    /// </summary>
    public static class AbilityGate
    {
        private static readonly Harmony harmony = new("TownOfHost.AbilityGate");
        private static readonly HashSet<MethodInfo> registered = new();
        private static readonly HarmonyMethod prefix = new(typeof(AbilityGate), nameof(Prefix));

        /// <summary>
        /// declaringType上のmethodNameという名前のメソッドを、AbilityEnabled審査の対象として登録する。
        /// 同じメソッドを複数回登録しても安全(2回目以降は無視される)。
        /// </summary>
        public static void Register(Type declaringType, string methodName)
        {
            var method = declaringType.GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (method == null)
            {
                Logger.Error($"{declaringType.Name}.{methodName}が見つからないため、AbilityGateに登録できませんでした。", "AbilityGate");
                return;
            }
            Register(method);
        }
        public static void Register(MethodInfo method)
        {
            if (method == null || !registered.Add(method)) return;
            harmony.Patch(method, prefix: prefix);
        }

        // __instanceがRoleBaseで、AbilityEnabledがfalseまたは全体的に能力使用不可(Canuseability)なら
        // 本体の実行自体をスキップする(false=元メソッドを実行しない)
        private static bool Prefix(object __instance, MethodBase __originalMethod)
        {
            if (__instance is not RoleBase role) return true;
            if (role.AbilityEnabled && SelfVoteManager.CanUseAbility()) return true;

            Logger.Info($"{role.Player?.Data?.GetLogPlayerName()}: 能力使用不可のため{__originalMethod.Name}をブロックしました", "AbilityGate");
            return false;
        }
    }
}
