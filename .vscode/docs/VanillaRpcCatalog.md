# バニラクライアント操作カタログ(RPC基盤)

このModはホスト権限だけで動作し、対戦相手のクライアントはバニラ(無改造)のままでよい設計になっている。そのため、**Modが実現できることは全て「バニラのAmong Us本体が元から認識できるRPC(`RpcCalls`)を、適切なタイミング・適切なデータで送りつけること」に限られる**。新しい役職の挙動を考えるときは、最終的にこのカタログにある操作の組み合わせに落とし込めるかどうかが実装可能性の判断基準になる。

役職側の共通化パターンは[`RoleCommonHooksCatalog.md`](RoleCommonHooksCatalog.md)、名前表示マーク専用の話は[`RoleTextApiStandard.md`](RoleTextApiStandard.md)を参照。本ドキュメントは「そもそも何ができるか」という技術基盤側の一覧。

## 使われているバニラRPC(`RpcCalls`)一覧

`RpcCalls`はAmong Us本体(`Assembly-CSharp.dll`)が定義する列挙型で、このプロジェクトのソース内には定義がない(参照DLL由来)。実際に使用されている値は以下の28種類(件数は概算、多い順):

| RpcCalls | 件数 | 用途 |
|---|---|---|
| `SetName` | 26 | 名前変更(役職名タグ付け、名前表示マーク全般) |
| `SnapTo` | 19 | 瞬間移動(テレポート系役職) |
| `SetVisorStr` / `SetSkinStr` / `SetHatStr` | 各17 | コスメ(バイザー/スキン/帽子)変更 |
| `SetRole` | 15 | バニラ役職(Crewmate/Impostor等)の割り当て |
| `MurderPlayer` | 15 | キル演出 |
| `SendChat` | 14 | チャット送信(システムメッセージ・広報等) |
| `SetColor` | 8 | プレイヤーカラー変更 |
| `BootFromVent` | 7 | ベント強制排出 |
| `UpdateSystem` | 6 | サボタージュ/システム状態更新 |
| `Shapeshift` | 6 | シェイプシフト(変身系役職) |
| `SetPetStr` | 6 | ペットコスメ変更 |
| `ProtectPlayer` | 4 | シールド(ボディガード演出) |
| `StartMeeting` | 3 | 緊急会議開始 |
| `SendQuickChat` | 3 | クイックチャット送信 |
| `StartVanish` | 2 | 透明化開始演出 |
| `SendChatNote` | 2 | 死亡通知等のチャットノート |
| `Pet` | 2 | ペット操作 |
| `EnterVent` | 2 | ベント侵入演出 |
| `VotingComplete` | 1 | 投票結果確定 |
| `StartAppear` | 1 | 再出現演出 |
| `SetNamePlateStr` | 1 | ネームプレート変更 |
| `RejectShapeshift` | 1 | シェイプシフト拒否 |
| `Exiled` | 1 | 追放演出 |
| `CloseMeeting` | 1 | 会議終了 |
| `CheckVanish` | 1 | 透明化状態確認 |
| `CancelPet` | 1 | ペット解除 |

**新しい役職の見た目・挙動を設計するときは、まずこの表の中に使えそうなRPCがないか確認する。** 例えば「対象の位置を変える」なら`SnapTo`、「対象を一時的に別役職に見せる」なら`SetName`+テキスト側の偽装ロジック、「特定条件で強制排出」なら`BootFromVent`、という具合に既存のRPCの組み合わせで表現できないかをまず検討する。

`RpcCalls`列挙型自体の完全なメンバー一覧はソースコードからは確認できない(バニラ本体のDLL内定義のため)。未使用のRPC種別を正確に洗い出すには`Assembly-CSharp.dll`をデコンパイルする必要があり、今回は行っていない。

## Mod専用の内部同期RPC(`CustomRPC`) — バニラには送らない

定義: `Modules/RPC.cs`(19〜51行目)

```
VersionCheck=80, RequestRetryVersionCheck=81,
SyncCustomSettings=100, SyncAssignOption, SetDeathReason, EndGame, PlaySound, SetCustomRole,
ReplaceSubRole, SetNameColorData, SetRealKiller, SetLoversPlayers, SetMadonnaLovers, SetCupidLovers,
ModUnload=240, SetInvisible=241, SendAIMessage=242, SendAIReply=243, GetAchievement=244,
PublicRoleSync=245, SyncYomiage, MeetingInfo, CustomRoleSync, CustomSubRoleSync,
ShowMeetingKill, ClientSendHideMessage, SyncModSystem, SyncAssassinState
```

送受信の仕組み自体はバニラのRPCチャネル(`AmongUsClient.Instance.StartRpcImmediately`)を借りているが、`callId`の値を意図的にバニラの`RpcCalls`と衝突しない範囲(80番台/100番台/240番台)にずらしている。`Modules/RPC.cs`の`RPCHandlerPatch`が、`callId`がこの範囲ならバニラRPCとして処理せず、Mod専用の同期処理(役職同期・恋人同期・死因同期・実績・AIチャット等)にディスパッチする。

**つまりこれは「バニラクライアントへの見た目の操作」ではなく、Mod導入済みホスト内部(またはMod検知)専用のチャンネル**。新しい役職の「対戦相手にどう見えるか」を設計するときは、この`CustomRPC`ではなく上記の`RpcCalls`一覧を使う。

## RPC送信の共通基盤(3層)

役職側が生の`AmongUsClient.Instance.StartRpcImmediately`を直接書く必要がないよう、以下の3層のヘルパーが用意されている。

1. **`Modules/CustomRpcSender.cs`** — `CustomRpcSender`クラス。1つの`GameData`パケットに複数RPCをバッチで詰め込み、`StartMessage`→複数RPC書き込み→`SendMessage`で送信する。パケット分割(サイズ超過時の分割送信)の制御もここが担う。`UtilsNotifyRoles.NotifyRoles`のような「全プレイヤー分の名前RPCを1回でまとめて送る」処理で使われている
2. **`Modules/RPC.cs`** — 個別RPC送信のstatic関数群。`AmongUsClient.Instance.StartRpcImmediately(...)`→`writer.Write(...)`→`FinishRpcImmediately(writer)`という定型パターンをラップする。`CustomRPC`の定義・ディスパッチもここに同居
3. **`Modules/ExtendedPlayerControl/ExtendedRpc.cs`** — `PlayerControl`への拡張メソッドとして個々のバニラRPC(`RpcSetName`, `RpcSnapTo`等)を提供し、呼び出し側からは`player.RpcXxx(...)`のように書けるようにする最も薄い層

新しい操作を実装するときは、まずこの3層に相当する処理が既にないか確認し、無ければ`ExtendedRpc.cs`に薄いラッパーを追加する形にする(役職側に生のRPC送信コードを直書きしない)。

## 未解決の検討事項

- `RpcCalls`の完全なメンバー一覧と、未使用のRPC種別の正確な洗い出しは、`Assembly-CSharp.dll`のデコンパイルが必要(未実施)
- 「共通化できるはずなのに個別実装のまま」の候補探しは、この技術基盤カタログとは別に、役職ファイル間の重複コード調査が必要([`RoleCommonHooksCatalog.md`](RoleCommonHooksCatalog.md)の未解決事項と同じ)
