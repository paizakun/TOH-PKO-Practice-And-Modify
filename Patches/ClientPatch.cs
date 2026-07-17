using System.Globalization;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using BepInEx.Unity.IL2CPP.Utils.Collections;

using TownOfHost.Modules;
using static TownOfHost.Translator;

namespace TownOfHost
{
    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.MakePublic))]
    class MakePublicPatch
    {
        public static bool Prefix(GameStartManager __instance)
        {
            // 定数設定による公開ルームブロック
            if (!AmongUsClient.Instance.AmHost) return false;
            if (!Main.AllowPublicRoom)
            {
                var message = GetString("DisabledByProgram");
                Logger.Info(message, "MakePublicPatch");
                Logger.seeingame(message);
                return false;
            }
            if (!Main.IsPublicRoomAllowed())
            {
                var message = "";
                if (!Main.IsPublicAvailableOnThisVersion) message = GetString("PublicNotAvailableOnThisVersion");
                if (!VersionChecker.IsSupported) message = GetString("UnsupportedVersion");
                if (ModUpdater.isBroken) message = GetString("ModBrokenMessage");
                if (ModUpdater.hasUpdate) message = GetString("CanNotJoinPublicRoomNoLatest");
                Logger.Info(message, "MakePublicPatch");
                Logger.seeingame(message);
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(CreateGameOptions), nameof(CreateGameOptions.Show)), HarmonyPriority(Priority.VeryLow)]
    class CreateGameOptionsShowPatch
    {
        public static void Prefix(CreateGameOptions __instance)
        {
            //マップIdが無効な時画面の操作ができなくなってしまうのを修正
            var mapId = GameOptionsManager.Instance.CurrentGameOptions.MapId;

            if (__instance.mapTooltips.Length <= mapId)
            {
                GameOptionsManager.Instance.CurrentGameOptions.SetByte(AmongUs.GameOptions.ByteOptionNames.MapId, 0);
                GameOptionsManager.Instance.currentHostOptions.SetByte(AmongUs.GameOptions.ByteOptionNames.MapId, 0);
            }
        }
        public static void Postfix(CreateGameOptions __instance)
        {
            //ノーマルモードを選択させる
            __instance.SelectMode(0, false);
            __instance.SetCurrentServer();
            //__instance.UpdateServerText(ServerManager.Instance.CurrentRegion.Name);
        }
    }

    [HarmonyPatch(typeof(CreateGameOptions), nameof(CreateGameOptions.OpenServerDropdown))]
    class CreateGameOptionsOpenServerDropdown
    {
        public static void Prefix(CreateGameOptions __instance)
        {
            __instance.serverDropdown.transform.localPosition = new Vector3(2.08f, -1.63f, -15f);
        }
    }

    [HarmonyPatch(typeof(CreateGameOptions), nameof(CreateGameOptions.Start))]
    class CreateGameOptionsStartPatch
    {
        public static void Prefix() => CreateGameOptionsUpdateServerText.Prefix();
    }
    [HarmonyPatch(typeof(CreateGameOptions), nameof(CreateGameOptions.UpdateServerText))]
    class CreateGameOptionsUpdateServerText
    {
        public static void Prefix()
        {
            var obj = GameObject.Find("MainMenuManager/MainUI/AspectScaler/CreateGameScreen/ParentContent/Content/CreateGame");
            if (obj == null) return;
            if (ServerManager.Instance?.CurrentRegion?.Name == null) return;

            var nowserver = ServerManager.Instance.CurrentRegion.Name;
            if ((Main.IsAndroid() && !Main.IsCs()) || nowserver is "ExROfficialTokyo" || nowserver.Contains("Nebula on the Ship JP") || nowserver.Contains("<color=#ffa500>Super</color>")
            || (VersionInfoManager.BlockVanillaSaver && !Main.IsCs()))
            {
                obj.transform.localPosition = new Vector3(100f, 100f, 100f);
            }
            else
            {
                obj.transform.localPosition = new Vector3(2.2664f, -4.71f, -12f);
            }
        }
    }

    [HarmonyPatch(typeof(SplashManager), nameof(SplashManager.Update))]
    class SplashLogoAnimatorPatch
    {
        public static void Prefix(SplashManager __instance)
        {
            if (DebugModeManager.AmDebugger)
            {
                __instance.sceneChanger.AllowFinishLoadingScene();
                __instance.startedSceneLoad = true;
            }
        }
    }
    [HarmonyPatch(typeof(EOSManager), nameof(EOSManager.IsAllowedOnline))]
    class RunLoginPatch
    {
        public static void Prefix(ref bool canOnline)
        {
#if DEBUG
            if (CultureInfo.CurrentCulture.Name != "ja-JP") canOnline = false;
#endif
        }
    }
    [HarmonyPatch(typeof(BanMenu), nameof(BanMenu.SetVisible))]
    class BanMenuSetVisiblePatch
    {
        public static bool Prefix(BanMenu __instance, bool show)
        {
            if (!AmongUsClient.Instance.AmHost) return true;
            show &= PlayerControl.LocalPlayer && PlayerControl.LocalPlayer.Data != null;
            __instance.BanButton.gameObject.SetActive(AmongUsClient.Instance.CanBan());
            __instance.KickButton.gameObject.SetActive(AmongUsClient.Instance.CanKick());
            __instance.MenuButton.gameObject.SetActive(show);
            return false;
        }
    }
    [HarmonyPatch(typeof(InnerNet.InnerNetClient), nameof(InnerNet.InnerNetClient.CanBan))]
    class InnerNetClientCanBanPatch
    {
        public static bool Prefix(InnerNet.InnerNetClient __instance, ref bool __result)
        {
            __result = __instance.AmHost;
            return false;
        }
    }
    [HarmonyPatch(typeof(InnerNet.InnerNetClient), nameof(InnerNet.InnerNetClient.KickPlayer))]
    class KickPlayerPatch
    {
        public static void Prefix(InnerNet.InnerNetClient __instance, int clientId, bool ban)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (ban) BanManager.AddBanPlayer(AmongUsClient.Instance.GetRecentClient(clientId));
        }
    }
    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.SendAllStreamedObjects))]
    class InnerNetObjectSerializePatch
    {
        public static void Prefix(InnerNetClient __instance)
        {
            if (AmongUsClient.Instance.AmHost)
                GameOptionsSender.SendAllGameOptions();
        }
    }
    [HarmonyPatch]
    class InnerNetClientPatch
    {
        [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.HandleMessage)), HarmonyPrefix]
        public static bool HandleMessagePatch(InnerNetClient __instance, MessageReader reader, SendOption sendOption)
        {
            /*
            if (DebugModeManager.IsDebugMode)
            {
                Logger.Info($"HandleMessagePatch:Packet({reader.Length}) ,SendOption:{sendOption}", "InnerNetClient");
            }
            else if (reader.Length > 1000)
            {
                Logger.Info($"HandleMessagePatch:Large Packet({reader.Length})", "InnerNetClient");
            }*/
            return true;
        }

        [HarmonyPatch(typeof(InnerNetServer), "Broadcast"), HarmonyPrefix]
        public static bool BroadcastPatch(MessageWriter msg, InnerNetServer.Player source)
        {
            // タグ(0x02=Rpc)+netId(packed varint)+callId(0x0D=SendChat)+文字列長+文字列、という
            // 構造として解釈できて、かつ文字列が"/cmd"で始まる場合だけ、
            // 中継をブロックする代わりに自分でOnReceiveChatを呼んでコマンドを実行する。
            // (Broadcastを丸ごとスキップするとホスト自身の処理も止まってしまうと判明したため、
            // 「処理は自分で肩代わりしてから中継だけ止める」形にする)
            if (!Options.ExHideChatCommand.GetBool()) return true;
            // 移動同期など高頻度なBroadcastの大半はSendOption.None、チャットは常にReliableなので、
            // ここで安価に弾いて、無関係な呼び出しでは配列コピー・走査を行わないようにする
            if (msg.SendOption != SendOption.Reliable) return true;
            try
            {
                if (TryFindSendChatCommand(msg.Buffer, msg.Length, out var commandText))
                {
                    // source.IdはPlayerControl.PlayerIdではなくネットワークのクライアントIdなので、
                    // GetClientId()で一致するプレイヤーを探す必要がある
                    var sender = source == null ? null : PlayerCatch.AllPlayerControls.FirstOrDefault(p => p.GetClientId() == source.Id);
                    if (sender != null)
                    {
                        Logger.Info($"BroadcastPatch: /cmdコマンドを検出、自前で処理して中継をブロックします Source={source?.Id} Text={commandText}", "InnerNetServer.Broadcast");
                        ChatCommands.OnReceiveChat(sender, commandText, out _);
                        return false;
                    }
                }
            }
            catch (System.Exception e)
            {
                Logger.Warn($"BroadcastPatch: 例外: {e}", "InnerNetServer.Broadcast");
            }
            return true;
        }

        private const byte RpcTag = 0x02;
        private static readonly byte SendChatCallId = (byte)RpcCalls.SendChat;

        // msg.Bufferの先頭には(パケットレベルのヘッダーなど)可変長の前置きバイトがあるため、
        // バッファ全体からタグ(0x02=Rpc)+netId(packed varint)+callId(0x0D=SendChat)+文字列長+文字列、
        // という並びを走査して探す。長さフィールドの一致検証で偶然の一致による誤検知を減らす
        private static bool TryFindSendChatCommand(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte> buf, int msgLength, out string commandText)
        {
            commandText = null;
            var effectiveLength = System.Math.Min(buf.Length, msgLength);
            var scanLen = System.Math.Min(effectiveLength, 128);
            for (int i = 0; i < scanLen; i++)
            {
                if (buf[i] != RpcTag) continue;
                if (i < 2) continue; // 直前2バイトのチャンク長フィールドを見るため

                // netId(packed varint)を読み飛ばす
                int j = i + 1;
                bool varintOk = false;
                for (int step = 0; step < 5 && j < effectiveLength; step++)
                {
                    byte b = buf[j];
                    j++;
                    if ((b & 0x80) == 0) { varintOk = true; break; }
                }
                if (!varintOk || j >= effectiveLength) continue;

                byte callId = buf[j];
                if (callId != SendChatCallId) continue;

                int strLenPos = j + 1;
                if (strLenPos >= effectiveLength) continue;
                byte strLen = buf[strLenPos];
                if (strLenPos + 1 + strLen > effectiveLength) continue;

                // タグの直前2バイトにある「チャンク長」(リトルエンディアン)が、
                // netId+callId+文字列長+文字列の実際のバイト数と一致するかを検証し、
                // 偶然のバイト一致による誤検知を減らす
                int expectedLen = (j - (i + 1)) + 1 + 1 + strLen;
                int declaredLen = buf[i - 2] | (buf[i - 1] << 8);
                if (declaredLen != expectedLen) continue;

                var allBytes = new byte[strLen];
                for (int k = 0; k < strLen; k++) allBytes[k] = buf[strLenPos + 1 + k];
                var fullText = System.Text.Encoding.UTF8.GetString(allBytes);

                bool isCommand = strLen >= 4 && fullText.StartsWith("/cmd");
                Logger.Info($"TryFindSendChatCommand: SendChatを検出 Text=\"{fullText}\" IsCommand={isCommand}", "InnerNetServer.Broadcast");

                if (!isCommand) continue;

                commandText = fullText;
                return true;
            }
            return false;
        }
        public static bool DontTouch = false;
        static Dictionary<int, int> messageCount = new(10);
        const int warningThreshold = 100;
        static int peak = warningThreshold;
        static float timer = 0f;
        [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.FixedUpdate)), HarmonyPrefix]
        public static void FixedUpdatePatch(InnerNetClient __instance)
        {
            int last = (int)timer % 10;
            timer += Time.fixedDeltaTime;
            int current = (int)timer % 10;
            if (last != current)
            {
                messageCount[current] = 0;
            }
        }

        [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.SendOrDisconnect)), HarmonyPrefix]
        public static bool SendOrDisconnectPatch(InnerNetClient __instance, MessageWriter msg)
        {
            //分割するサイズ。大きすぎるとリトライ時不利、小さすぎると受信パケット取りこぼしが発生しうる。
            var limitSize = 1000;

            /*if (DebugModeManager.IsDebugMode)
            {
                Logger.Info($"SendOrDisconnectPatch:Packet({msg.Length}) ,SendOption:{msg.SendOption}", "InnerNetClient");
            }
            else*/
            if (msg.Length > limitSize)
            {
                Logger.Info($"SendOrDisconnectPatch:Large Packet({msg.Length}) ,SendOption:{msg.SendOption}", "InnerNetClient");
            }
            //メッセージピークのログ出力
            if (msg.SendOption == SendOption.Reliable)
            {
                int last = (int)timer % 10;
                messageCount[last]++;
                int totalMessages = 0;
                foreach (var count in messageCount.Values)
                {
                    totalMessages += count;
                }
                if (totalMessages > warningThreshold)
                {
                    if (peak > totalMessages)
                    {
                        Logger.Warn($"SendOrDisconnectPatch:Packet Spam Detected ({peak})", "InnerNetClient");
                        peak = warningThreshold;
                    }
                    else
                    {
                        peak = totalMessages;
                    }
                }
                /*
                else
                {
                    Logger.Info($"{totalMessages}", "InnerNetClient");
                }*/
            }
            if (!Options.FixSpawnPacketSize.GetBool() && !Utils.IsRestriction()) return true;
            if (DontTouch || AntiBlackout.IsCached) return true;

            //ラージパケットを分割(9人以上部屋で落ちる現象の対策コード)

            //メッセージが大きすぎる場合は分割して送信を試みる
            if (msg.Length > limitSize)
            {
                var writer = MessageWriter.Get(msg.SendOption);
                var reader = MessageReader.Get(msg.ToByteArray(false));

                //Tagレベルの処理
                while (reader.Position < reader.Length)
                {
                    //Logger.Info($"SendOrDisconnectPatch:reader {reader.Position} / {reader.Length}", "InnerNetClient");

                    var partMsg = reader.ReadMessage();
                    var tag = partMsg.Tag;

                    //Logger.Info($"SendOrDisconnectPatch:partMsg Tag={tag} Length={partMsg.Length}", "InnerNetClient");

                    //TagがGameData,GameDataToの場合のみ分割処理
                    //それ以外では多分分割しなくても問題ない
                    if (tag is 5 or 6 && partMsg.Length > limitSize)
                    {
                        //分割を試みる
                        DivideLargeMessage(__instance, writer, partMsg);
                    }
                    else
                    {
                        //そのまま追加
                        WriteMessage(writer, partMsg);
                    }

                    //送信サイズが制限を超えた場合は送信
                    if (writer.Length > limitSize)
                    {
                        Send(__instance, writer);
                        writer.Clear(writer.SendOption);
                    }
                }

                //残りの送信
                if (writer.HasBytes(7))
                {
                    Send(__instance, writer);
                }

                writer.Recycle();
                reader.Recycle();
                return false;
            }
            return true;
        }
        private static void DivideLargeMessage(InnerNetClient __instance, MessageWriter writer, MessageReader partMsg)
        {
            var tag = partMsg.Tag;
            var GameId = partMsg.ReadInt32();
            var ClientId = -1;

            //元と同じTagを開く
            writer.StartMessage(tag);
            writer.Write(GameId);
            if (tag == 6)
            {
                ClientId = partMsg.ReadPackedInt32();
                writer.WritePacked(ClientId);
            }

            //Flag単位の処理
            while (partMsg.Position < partMsg.Length)
            {
                var subMsg = partMsg.ReadMessage();
                var subLength = subMsg.Length;

                //加算すると制限を超える場合は先に送信
                if (writer.Length + subLength > 800)
                {
                    writer.EndMessage();
                    Send(__instance, writer);
                    //再度Tagを開く
                    writer.Clear(writer.SendOption);
                    writer.StartMessage(tag);
                    writer.Write(GameId);
                    if (tag == 6)
                    {
                        writer.WritePacked(ClientId);
                    }
                }
                //メッセージの出力
                WriteMessage(writer, subMsg);
            }
            writer.EndMessage();
        }

        private static void WriteMessage(MessageWriter writer, MessageReader reader)
        {
            writer.Write((ushort)reader.Length);
            writer.Write(reader.Tag);
            writer.Write(reader.ReadBytes(reader.Length));
        }

        private static void Send(InnerNetClient __instance, MessageWriter writer)
        {
            //Logger.Info($"SendOrDisconnectPatch: SendMessage Length={writer.Length}", "InnerNetClient");
            var err = __instance.connection.Send(writer);
            if (err != SendErrors.None)
            {
                Logger.Info($"SendOrDisconnectPatch: SendMessage Error={err}", "InnerNetClient");
            }
        }
    }

    [HarmonyPatch(typeof(AmongUsClient))]
    class PreloadMapPatch
    {
        public static bool lastToggle = false;
        public static List<AsyncOperationHandle> handles = new();
        public static Dictionary<byte, ShipStatus> ships = new();

        [HarmonyPostfix]
        [HarmonyPatch(nameof(AmongUsClient.Awake))]
        [HarmonyPatch(nameof(AmongUsClient.OnGameJoined))]
        public static void Preload()
        {
            if (lastToggle == Main.PreloadMapAssets.Value) return;
            lastToggle = Main.PreloadMapAssets.Value;

            if (lastToggle)
            {
                AmongUsClient.Instance.StartCoroutine(CoLoadAssets().WrapToIl2Cpp());
            }
            else
            {
                foreach (var handle in handles)
                {
                    Logger.Warn($"Start Unload {(handle.Result.TryCast<GameObject>(out var obj) ? obj.name : "???")}", nameof(PreloadMapPatch));
                    if (handle.IsValid())
                        handle.Release();
                }
                ships.Clear();
                handles.Clear();
            }
        }

        public static IEnumerator CoLoadAssets()
        {
            var maps = EnumHelper.GetAllValues<MapNames>();
            var shipPrefabs = AmongUsClient.Instance.ShipPrefabs;
            for (var i = 0; i < maps.Length; ++i)
            {
                var mapId = maps[i];
                var ship = shipPrefabs[i];
                if (lastToggle == false) break; //途中でOFFになった場合は中断
                yield return CoLoad(ship, mapId);
            }
        }

        public static IEnumerator CoLoad(UnityEngine.AddressableAssets.AssetReference ship, MapNames mapId)
        {
            AsyncOperationHandle handle;

            if (ship.OperationHandle.IsValid() && ship.OperationHandle.Status == AsyncOperationStatus.Succeeded)
            {
                Logger.Warn($"Skip Load {mapId}", nameof(PreloadMapPatch));
                handle = ship.OperationHandle;
            }
            else
            {
                Logger.Warn($"Start Load {mapId}", nameof(PreloadMapPatch));
                handle = ship.LoadAssetAsync<GameObject>();
                handles.Add(handle);

                yield return handle;
            }

            Logger.Warn($"End Load {mapId}", nameof(PreloadMapPatch));

            if (handle.IsValid() && handle.Status == AsyncOperationStatus.Succeeded)
            {
                var obj = handle.Result.Cast<GameObject>();
                ships[(byte)mapId] = obj.GetComponent<ShipStatus>();
            }
            else
            {
                Logger.Error($"Failed to load {mapId}", nameof(PreloadMapPatch));
            }
        }
    }
}