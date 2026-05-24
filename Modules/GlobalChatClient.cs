using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TownOfHost;

namespace TownOfHost.Modules;

public static class GlobalChatManager
{
    private static ClientWebSocket _socket;
    private static CancellationTokenSource _cts;

    public static List<byte> IgnoreList = new();

    public static void Initialize(string serverUrl)
    {
        _cts = new CancellationTokenSource();
        Task.Run(async () => await ConnectAsync(serverUrl, _cts.Token));
    }

    private static async Task ConnectAsync(string url, CancellationToken ct)
    {
        _socket = new ClientWebSocket();
        await _socket.ConnectAsync(new Uri(url), ct);

        byte[] buffer = new byte[1024];
        while (_socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
            string msg = Encoding.UTF8.GetString(buffer, 0, result.Count);

            _ = new LateTask(() =>
            {
                if (!AmongUsClient.Instance.AmHost) return;

                string text = $"<color=#00FFFF>[Global]</color> {msg}";

                // ★ Aiserverと同じパターン: Main.MessagesToSend.Add で送信
                //    Utils.SendMessage はホストのローカル表示のみ
                //    MessagesToSend キューが RPC 経由で全クライアントに届ける

                if (IgnoreList.Count == 0)
                {
                    // 全員に一括送信
                    Main.MessagesToSend.Add((text, byte.MaxValue, "GlobalChat"));
                }
                else
                {
                    foreach (var pc in PlayerCatch.AllPlayerControls)
                    {
                        if (pc == null || pc.Data == null || pc.Data.Disconnected) continue;
                        if (IgnoreList.Contains(pc.PlayerId)) continue;
                        Main.MessagesToSend.Add((text, pc.PlayerId, "GlobalChat"));
                    }
                }

            }, 0.2f, "GlobalChat_Receive_Task", true);
        }
    }

    public static void SendMessage(string message)
    {
        if (_socket != null && _socket.State == WebSocketState.Open)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            _socket.SendAsync(
                new ArraySegment<byte>(buffer),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
        }
    }
}