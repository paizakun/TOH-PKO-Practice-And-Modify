using System;
using System.Collections.Generic;

namespace TownOfHost.Modules;

/// <summary>
/// 毎フレーム(FixedUpdate)処理を登録制で一元管理するディスパッチャ。
/// 各サブシステムはRegisterで自分のTick処理を登録するだけでよく、
/// 呼び出し元(FixedUpatePatch.cs等)はRunAllを1回呼ぶだけで済む。
/// </summary>
public static class TickManager
{
    private static readonly List<Action<PlayerControl>> Handlers = new();

    public static void Register(Action<PlayerControl> handler) => Handlers.Add(handler);

    public static void RunAll(PlayerControl player)
    {
        foreach (var handler in Handlers) handler(player);
    }
}
