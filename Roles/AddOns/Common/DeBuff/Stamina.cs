using System.Collections.Generic;
using AmongUs.GameOptions;
using HarmonyLib;
using UnityEngine;
using TownOfHost.Roles.Core;
using static TownOfHost.Options;

namespace TownOfHost.Roles.AddOns.Common
{
    public static class Stamina
    {
        private static readonly int Id = 50900;
        private static Color RoleColor = UtilsRoleText.GetRoleColor(CustomRoles.Stamina);
        public static string SubRoleMark = Utils.ColorString(RoleColor, "ST");
        public static List<byte> playerIdList = new();

        static OptionItem OptionMaxStamina; static float maxStamina;
        static OptionItem OptionDrainRate; static float drainRate;
        static OptionItem OptionRecoverRate; static float recoverRate;
        static OptionItem OptionMaxSpeed; static float maxSpeed;
        static OptionItem OptionSlowSpeed; static float slowSpeed;
        static OptionItem OptionDrainThreshold; static float drainThreshold;

        // ★ 静止判定はハードコード（オプション不要）
        private const float StopDistancePerSec = 0.15f;

        private static readonly Dictionary<byte, float> CurrentStamina = new();
        private static readonly Dictionary<byte, bool> IsExhausted = new();
        private static readonly Dictionary<byte, Vector2> LastPosition = new();
        private static readonly Dictionary<byte, bool> Initialized = new();
        private static readonly Dictionary<byte, float> NotifyTimer = new();

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.Addons, CustomRoles.Stamina);
            AddOnsAssignData.Create(Id + 10, CustomRoles.Stamina, true, true, true, true);
            ObjectOptionitem.Create(Id + 20, "AddonOption", true, "", TabGroup.Addons)
                .SetOptionName(() => "Role Option").SetSubRoleOptionItem(CustomRoles.Stamina);

            OptionMaxStamina = FloatOptionItem.Create(Id + 21, "StaminaMax",
                new(5f, 60f, 1f), 20f, TabGroup.Addons, false)
                .SetSubRoleOptionItem(CustomRoles.Stamina)
                .SetValueFormat(OptionFormat.Seconds);

            OptionDrainRate = FloatOptionItem.Create(Id + 22, "StaminaDrainRate",
                new(0.1f, 5f, 0.1f), 1f, TabGroup.Addons, false)
                .SetSubRoleOptionItem(CustomRoles.Stamina);

            OptionRecoverRate = FloatOptionItem.Create(Id + 23, "StaminaRecoverRate",
                new(0.1f, 5f, 0.1f), 0.5f, TabGroup.Addons, false)
                .SetSubRoleOptionItem(CustomRoles.Stamina);

            OptionMaxSpeed = FloatOptionItem.Create(Id + 24, "StaminaMaxSpeed",
                new(0.25f, 3f, 0.05f), 1.5f, TabGroup.Addons, false)
                .SetSubRoleOptionItem(CustomRoles.Stamina);

            OptionSlowSpeed = FloatOptionItem.Create(Id + 25, "StaminaSlowSpeed",
                new(0.1f, 2f, 0.05f), 0.5f, TabGroup.Addons, false)
                .SetSubRoleOptionItem(CustomRoles.Stamina);

            OptionDrainThreshold = FloatOptionItem.Create(Id + 26, "StaminaDrainThreshold",
                new(0f, 1f, 0.05f), 0.5f, TabGroup.Addons, false)
                .SetSubRoleOptionItem(CustomRoles.Stamina)
                .SetValueFormat(OptionFormat.Percent);
        }

        public static void Init()
        {
            playerIdList = new();
            maxStamina = OptionMaxStamina.GetFloat();
            drainRate = OptionDrainRate.GetFloat();
            recoverRate = OptionRecoverRate.GetFloat();
            maxSpeed = OptionMaxSpeed.GetFloat();
            slowSpeed = OptionSlowSpeed.GetFloat();
            drainThreshold = OptionDrainThreshold.GetFloat();

            CurrentStamina.Clear();
            IsExhausted.Clear();
            LastPosition.Clear();
            Initialized.Clear();
            NotifyTimer.Clear();
        }

        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            CurrentStamina[playerId] = maxStamina;
            IsExhausted[playerId] = false;
            Initialized[playerId] = false; // ★ 初回フレームで位置初期化する
            NotifyTimer[playerId] = 0f;

            if (AmongUsClient.Instance.AmHost)
                SetSpeed(playerId, maxSpeed);
        }

        public static void OnFixedUpdate(PlayerControl player)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (player == null || !player.IsAlive()) return;
            if (GameStates.IsMeeting) return;

            byte id = player.PlayerId;
            if (!playerIdList.Contains(id)) return;

            var currentPos = player.GetTruePosition();

            if (!Initialized.TryGetValue(id, out bool init) || !init)
            {
                LastPosition[id] = currentPos;
                Initialized[id] = true;
                return;
            }

            float moved = Vector2.Distance(currentPos, LastPosition[id]);
            LastPosition[id] = currentPos;
            float movedPerSec = moved / Time.fixedDeltaTime;
            bool isMoving = movedPerSec > StopDistancePerSec;

            if (isMoving)
            {
                CurrentStamina[id] -= drainRate * Time.fixedDeltaTime;
                CurrentStamina[id] = Mathf.Max(0f, CurrentStamina[id]);
            }
            else
            {
                CurrentStamina[id] += recoverRate * Time.fixedDeltaTime;
                CurrentStamina[id] = Mathf.Min(maxStamina, CurrentStamina[id]);
            }

            // ★ スタミナ割合に応じて常に速度を滑らかに計算
            // 満タン(1.0) → maxSpeed、ゼロ(0.0) → slowSpeed で線形補間
            float ratio = CurrentStamina[id] / maxStamina;
            float newSpeed = Mathf.Lerp(slowSpeed, maxSpeed, ratio);
            SetSpeed(id, newSpeed);

            NotifyTimer[id] += Time.fixedDeltaTime;
            if (NotifyTimer[id] >= 0.2f)
            {
                NotifyTimer[id] = 0f;
                UtilsNotifyRoles.NotifyRoles(OnlyMeName: true);
            }
        }

        public static void OnStartMeeting()
        {
            if (!AmongUsClient.Instance.AmHost) return;
            foreach (var id in playerIdList)
            {
                CurrentStamina[id] = maxStamina;
                IsExhausted[id] = false;
                Initialized[id] = false;
                SetSpeed(id, maxSpeed);
            }
        }

        public static void AfterMeetingTasks()
        {
            if (!AmongUsClient.Instance.AmHost) return;
            foreach (var id in playerIdList)
            {
                var pc = PlayerCatch.GetPlayerById(id);
                if (pc == null || !pc.IsAlive()) continue;
                CurrentStamina[id] = maxStamina;
                IsExhausted[id] = false;
                Initialized[id] = false;
                SetSpeed(id, maxSpeed);
            }
        }

        private static void SetSpeed(byte playerId, float speed)
        {
            if (!Main.AllPlayerSpeed.ContainsKey(playerId)) return;
            if (Mathf.Approximately(Main.AllPlayerSpeed[playerId], speed)) return;
            Main.AllPlayerSpeed[playerId] = speed;
            PlayerCatch.GetPlayerById(playerId)?.MarkDirtySettings();
        }
    }

    // ★ PlayerControl.FixedUpdate にフック
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    public static class StaminaFixedUpdatePatch
    {
        public static void Postfix(PlayerControl __instance)
        {
            if (!GameStates.IsInTask) return;
            if (!__instance.Is(CustomRoles.Stamina)) return;
            Stamina.OnFixedUpdate(__instance);
        }
    }
}