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

        // __instanceがRoleBaseで、AbilityEnabledがfalseなら本体の実行自体をスキップする(false=元メソッドを実行しない)
        private static bool Prefix(object __instance, MethodBase __originalMethod)
        {
            if (__instance is RoleBase role && !role.AbilityEnabled)
            {
                Logger.Info($"{role.Player?.Data?.GetLogPlayerName()}: AbilityEnabled=falseのため{__originalMethod.Name}をブロックしました", "AbilityGate");
                return false;
            }
            return true;
        }
    }
}
