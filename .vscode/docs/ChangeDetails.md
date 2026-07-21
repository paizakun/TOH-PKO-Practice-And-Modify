# フォーク後の変更まとめ（詳細版）

フォーク元(`paizakun/TOH-PKO-Practice-And-Modify`)から現時点までの、自分(Ereshkigal / `paizakun`)によるコミット18件をまとめる。
簡易版は [ChangeSummary.md](ChangeSummary.md)。

## 目次

1. [練習モード(Practice Mode)の新規作成](#1-練習モードpractice-modeの新規作成)
2. [`/cmd cr`によるシェイプシフター系役職変更時の不具合修正](#2-cmd-crによるシェイプシフター系役職変更時の不具合修正)
3. [EvilMovingのキルクール参照方式の修正](#3-evilmovingのキルクール参照方式の修正)
4. [個人環境差分ファイルのgit管理除外](#4-個人環境差分ファイルのgit管理除外)
5. [速度バフ/デバフ統一管理API `OperatePlayerSpeedModifier` の新規実装](#5-速度バフデバフ統一管理api-operateplayerspeedmodifier-の新規実装)
6. [キルクールダウン同期API `SyncKillCooldown` の新規実装（後に分割）と検証用役職](#6-キルクールダウン同期api-synckillcooldown-の新規実装後に分割と検証用役職)
7. [GlobalChat接続エラーログの抑制](#7-globalchat接続エラーログの抑制)
8. [練習モード専用 `/cmd setconfig` コマンドの追加](#8-練習モード専用-cmd-setconfig-コマンドの追加)
9. [`/cmd`コマンドの秘匿情報が他クライアントへ漏れる不具合の修正](#9-cmdコマンドの秘匿情報が他クライアントへ漏れる不具合の修正)
10. [投票発動共通API・役職無効化フック・占いAPI・能力自動審査APIの新設(AmateurTellerの再実装)](#10-投票発動共通api役職無効化フック占いapi能力自動審査apiの新設amateurtellerの再実装)
11. [付録: 今回のセッションでの議論・確認事項](#付録-今回のセッションでの議論確認事項コードに残らない設計判断)

---

## 1. 練習モード(Practice Mode)の新規作成

**コミット**: `695626a4` Practiceモードをテスト作成

- `Modules/GameMode/GameModeManager.cs`, `Modules/OptionHolder.cs`, `Patches/ChatCommandPatch.cs`, `Patches/onGameStartedPatch.cs`, `Resources/string.csv`
- `/cmd cr`（役職変更コマンド）を有効化した専用ゲームモードとして`CustomGameMode.Practice`を追加。
- 終了処理（試合終了時の後始末）は当時未実装だった。

---

## 2. `/cmd cr`によるシェイプシフター系役職変更時の不具合修正

**コミット**: `6b69711a` cmd crで役職が正しく切り替わらない問題を修正

- `Modules/ExtendedPlayerControl/ExtendedRpc.cs`
  - `RpcSetCustomRole`内で、対象がホスト自身の場合`RpcSetRole`だけではホストの`Data.Role.Role`が実際には切り替わらないことがあったため、`RoleManager.Instance.SetRole`を明示的に呼ぶよう修正。
    ```csharp
    if (player.PlayerId == PlayerControl.LocalPlayer.PlayerId)
    {
        RoleManager.Instance.SetRole(PlayerControl.LocalPlayer, role.GetRoleTypes());
    }
    ```
- `Roles/Crewmate/Express.cs`
  - 速度バフの解除処理を`ChengeRoleAdd()`（呼ばれるタイミングが不安定）から`OnDestroy()`に変更。`/cmd cr`でExpressから別役職に変更した際、速度が戻らず速いままになる不具合を修正。

---

## 3. EvilMovingのキルクール参照方式の修正

**コミット**: `efbb969b` EvilMovingのキルクール修正

- `Roles/Impostor/EvilMoving.cs`
  - `CalculateKillCooldown()`が`Main.NormalOptions.KillCooldown`（同期処理のたびに書き換わる共有の可変値）を都度直接参照していたのを、役職生成時(コンストラクタ)にキャッシュした`static float KillCooldown`を返すよう修正。他の`IUsePhantomButton`実装役職と同じ方式に統一。
- `TownOfHost.csproj`の軽微な変更を含む。

---

## 4. 個人環境差分ファイルのgit管理除外

**コミット**: `8c78dde7` Gitignoreに個人によって異なるcsprojを追加 / `18f2881b` TownOfHost.csprojをgit追跡から除外

- `.gitignore`に`TownOfHost.csproj`を追加し、追跡対象から削除。開発環境ごとに異なりうるcsprojの差分がコミットに混ざらないようにする環境設定変更。機能追加ではない。

---

## 5. 速度バフ/デバフ統一管理API `OperatePlayerSpeedModifier` の新規実装

**コミット**: `44200a0f` 速度APIと練習モードのいったんの終息

### 新規ファイル: `Modules/OperatePlayerSpeedModifier.cs`

移動速度の一時効果(バフ/デバフ)を役職側が個別管理するのではなく、統一APIで管理する仕組み。

```
最終速度 = BaseSpeed × (1 + Add系Valueの合計) × (Multiply系Valueの積)
```

公開API:
- `SetBaseSpeed(player, speed)` — 役職固有の恒久的な地の速度を設定（一時効果には使わない）
- `Add(target, source, value, duration)` — `BaseSpeed`に対する割合(0.1 = +10%)をduration秒間加算。負値で減算
- `AddIndefinite(target, source, value)` — `Add`の無期限版。`RemoveBySource`で明示的に解除するまで有効
- `Multiply(target, source, multiplier, duration)` — 倍率(0.8 = 0.8倍)をduration秒間乗算
- `MultiplyIndefinite(target, source, multiplier)` — `Multiply`の無期限版
- `RemoveBySource(source)` — 指定した`source`が登録した全エントリを削除。`RoleBase.Dispose()`から自動的に呼ばれる

内部実装:
- `SpeedModifierEntry`（`Source`, `Mode`(Add/Multiply), `Value`, `RemainingDuration`）のリストをプレイヤーごとに保持
- `OnFixedUpdate`で`RemainingDuration`を減算し、期限切れエントリを除去して再計算(`Recompute`)
- `Recompute`結果は`Main.AllPlayerSpeed[playerId]`へ反映し、`MarkDirtySettings()`で同期をマーク

### 新規ファイル: `Modules/TickManager.cs`

毎フレーム(FixedUpdate)処理を登録制で一元管理するディスパッチャ。各サブシステムは`TickManager.Register(handler)`で自分のTick処理を登録するだけでよく、呼び出し元(`Patches/PlayerContorols/FixedUpatePatch.cs`)は`TickManager.RunAll(player)`を1回呼ぶだけで済む。

### 新規ファイル: `Roles/Impostor/SpeedAdderTest.cs`

`OperatePlayerSpeedModifier`の動作検証用テスト役職。透明化ボタン(バニラのPhantomアビリティボタン)を使用すると、自分自身に10秒間`BaseSpeed+100%`(2倍速)を付与する。

---

## 6. キルクールダウン同期API `SyncKillCooldown` の新規実装（後に分割）と検証用役職

**コミット**: `be9e8a58` キルクールダウンAPIとテスト役職実装 → `5aeb6479` 修正 → `3039c50f` 調整 → `21467bb7` `SetMaxKillCooldown`/`SetCurrentKillCooldown`へ分割

### 背景・動機

Among Usの`killTimer`（実際のキルクールダウンの残り秒数）はネットワーク同期されない。ホストが値を直接書き換えても対象が他プレイヤーの場合は反映されない。この制約下で「任意の秒数にキルクールを設定する」ためのAPIを新規実装した。

### 実装の要点（バニラの仕様に依存する部分）

- 唯一「相手のクライアント上で`killTimer`を書き換えさせる」ことができる手段が、`MurderPlayer`RPC経由でのタイマーリセットのみ。
- `ExtendedRpc.cs`の`RpcProtectedMurderPlayer`は、自分自身を対象に`MurderResultFlags.FailedProtected`（バニラの「守護天使に守られてキル失敗」結果）を使って、**実際にはキルさせずにタイマーリセットの副作用だけ発生させる**トリックを使っている。`Succeeded`を使うと本当に自分を殺してしまうため使えない。
- バニラの`FailedProtected`処理は、リセット後のクールダウンを**現在のKillCooldown設定値の半分**にする仕様がある。そのため、意図した秒数の**2倍の値**を一時的に`KillCooldown`として送ってからRPCを発火する補正が必要（`5aeb6479`で修正）。
- バニラの`killTimer`は毎フレーム`Min(killTimer, KillCooldown)`で切り詰められる仕様がある。このため、送信したKillCooldownの値を下げると、進行中のタイマーがその場で切り詰められる（クランプされる）。

### API分割の経緯

当初1つの`SyncKillCooldown(duration)`だったが、以下の問題が判明し2つに分割した。

- **問題1**: 「最大値」と「現在のタイマー値」を別々に設定したいという要求が出た。
- **問題2**: `current > max`の場合、RPC後にmaxへ復元する処理を入れると、バニラのMin-clampによって即座にmaxまで切り詰められ、見た目上一瞬しか反映されない。
- **問題3**: 復元を遅らせる（保持し続ける）方式は、その待機中に本当のキルが発生すると誤った(一時的に膨らませた)クールダウン値がそのキルに適用されてしまう危険があるため不採用。

**最終的な設計方針**:
- `SetMaxKillCooldown(player, maxCooldown)` — 「今アクティブな上限」を設定する。他のリセット処理（実際のキル成立時など）で参照される基準値。
- `SetCurrentKillCooldown(player, currentCooldown)` — 現在のタイマーを一回限り即座に変更する操作。**maxへの自動復元は行わない**。RPC到達後、格納値を`currentCooldown`そのものに設定し直す（＝一時的にmax相当の値をcurrentへ置き換える）。以後何らかのリセット（実際のキル成立や次の`SetMaxKillCooldown`呼び出し）が入れば、そちらの値が優先されて上書きされるのを正しい仕様とする。

この設計により、「current中は正常動作し、リセットが入ったらmaxに戻る」という一貫した挙動になる。

### 送信順序の保証

`SetCurrentKillCooldown`は「KillCooldownの一時的な値をGameOptions(Reliable)で送信 → RPC(元はSendOption.None)でタイマーリセット」という2段階の送信を行うが、`SendOption.None`と`SendOption.Reliable`は別チャンネルで**互いの送信順序が保証されない**。同一フレームで連続送信すると、リモートクライアント(特にバニラクライアント)側でRPCが先に処理され、意図した値が反映されないことがある。

当初は`Main.LagTime`（リージョンに応じた固定の推測値、実測値ではない）による遅延で誤魔化していたが、この方式は実際のラグが`LagTime`を超えると容易に破綻する。最終的に、`RpcProtectedMurderPlayer`に`SendOption`を明示指定できる新オーバーロードを追加し、`SetCurrentKillCooldown`は`SyncSettings()`と同じ`SendOption.Reliable`で送信することで、タイムベースの当て推量に依存せず順序を保証する方式に変更した。

```csharp
// Modules/ExtendedPlayerControl/ExtendedRpc.cs
public static void RpcProtectedMurderPlayer(this PlayerControl killer, PlayerControl target = null)
    => killer.RpcProtectedMurderPlayer(target, SendOption.None);
public static void RpcProtectedMurderPlayer(this PlayerControl killer, PlayerControl target, SendOption sendOption)
{ /* ... */ }
```

※ 既存の`RpcProtectedMurderPlayer(target)`（`SendOption.None`固定）は他の役職から高頻度に呼ばれているため変更していない。新しいオーバーロードは今回の用途専用。

### `IKiller`実装によるリセット時の値保護

`Patches/PlayerContorols/RoleAbilityPatch.cs`の`PlayerControlPhantomPatch.CheckVanish`は、`AdjustKillCooldown`フラグの値に関わらず**無条件に**`ResetKillCooldown()`を呼ぶ実装になっている（この共通コードは影響範囲が広いため今回は変更していない）。`ResetKillCooldown()`は`(roleClass as IKiller)?.CalculateKillCooldown() ?? Options.DefaultKillCooldown`を参照するため、`IKiller`未実装のロールでは意図しない`Options.DefaultKillCooldown`で上書きされてしまう。

`KillCoolSyncTest`に`IKiller.CalculateKillCooldown() => OptionMaxSyncValue.GetFloat()`を実装することで、この上書きが起きても常に正しいMax値になるようにした。

### 検証用役職: `Roles/Impostor/KillCoolSyncTest.cs`

- `IImpostor`, `IUsePhantomButton`, `IKiller`を実装
- オプション: `OptionMaxSyncValue`（最大キルクール秒数）, `OptionCurrentSyncValue`（現在のキルクール秒数）を個別に設定可能
- 透明化ボタン使用時、`SetMaxKillCooldown`→`SetCurrentKillCooldown`を順に呼ぶ

---

## 7. GlobalChat接続エラーログの抑制

**コミット**: `27daed2c` ログの抑制

- `Modules/GlobalChatClient.cs`
  - WebSocket接続失敗時、5秒ごとの再試行のたびにログが出続けていたのを、**接続に成功するまでは初回のみ**警告ログを出すように変更(`hasLoggedError`フラグを追加)。接続成功時にフラグをリセットするので、再度切断すれば再び通知される。

---

## 8. 練習モード専用 `/cmd setconfig` コマンドの追加

**コミット**: `0a792121` ゲーム内役職設定変更コマンドの追加

- `Patches/ChatCommandPatch.cs`（`/cmd setconfig`ケースを新設、約130行）
- `CustomGameMode.Practice`中のみ使用可能。
- 使い方: `/cmd setconfig <役職名|myrole> [n番目] [値]`
  - 役職名のみ指定 → その役職に紐づく設定項目一覧を番号付きで表示
  - 役職名+番号 → その設定項目の型・範囲・現在値を表示
  - 役職名+番号+値 → その設定項目に値を設定
- 対応する設定型: `FloatOptionItem`, `IntegerOptionItem`, `BooleanOptionItem`
- 設定変更後、その役職を既に持っているプレイヤー全員に対し、一旦`Crewmate`を経由して役職を再付与し直すことで、コンストラクタでキャッシュされた値を含めて新しい設定値を反映させる。

---

## 9. `/cmd`コマンドの秘匿情報が他クライアントへ漏れる不具合の修正

**コミット**: `097d8d7d` コマンドの他クライアントへの通信の抑制

### 問題

従来は`ChatController.SendChat`をHarmonyパッチしてアプリケーション層で`/cmd`コマンドかどうかを判定・キャンセルしていたが、`SendChat`のRPC自体は`InnerNetServer.Broadcast`によって**全クライアントへの配信が先に(判定処理より前に)実行されてしまう**構造だった。これはタイミング(遅延)に依存する競合ではなく、呼び出し順そのものに起因する構造的な問題であり、レイテンシの大小やローカル/実サーバーの違いに関わらず再現する。結果として、本来秘匿されるべき`/cmd`コマンドの内容（役職固有の秘匿チャットなど）が他クライアントに一時的に漏れることがあった。

### 修正内容

- `Patches/ClientPatch.cs`に`[HarmonyPatch(typeof(InnerNetServer), "Broadcast")]`のPrefixパッチ`BroadcastPatch`を追加。
- Broadcastされるバイト列を直接走査し、「タグ(0x02=Rpc) + netId(packed varint) + callId(0x0D=SendChat) + 文字列長 + 文字列」という構造かつ文字列が`"/cmd"`で始まる場合にのみ、**中継そのものをブロック**し、代わりに自前で`ChatCommands.OnReceiveChat`を呼んでコマンドを処理する。
  - Broadcast全体を丸ごとスキップするとホスト自身の処理も止まってしまうことが判明したため、「該当メッセージだけを選別してブロックし、処理は自分で肩代わりする」という設計。
- `TryFindSendChatCommand`でバイト列を解析する際、タグ直前2バイトの「チャンク長」フィールドと実際のバイト数を比較検証し、偶然のバイト一致による誤検知を減らしている。
- `Options.ExHideChatCommand`が無効な場合、および`msg.SendOption != SendOption.Reliable`の場合は即座に処理をスキップし、高頻度なBroadcast(移動同期など、通常`SendOption.None`)への性能影響を避けている。
- `ExHideChatCommand`が有効な場合、既存の`ChatCommandPatch.cs`側にも「バニラ鯖以外のチャット秘匿の処理」として、会議中に生存者全員へ発言者を一時的に生存扱いにしたRPCを個別送信する処理が別途存在する（Mod非対応クライアント向けの秘匿チャット偽装）。

---

## 10. 投票発動共通API・役職無効化フック・占いAPI・能力自動審査APIの新設(AmateurTellerの再実装)

**コミット**: `c5c0d777` 投票の共通API実装 → `63862347` 役職無効フックの追加 → `f4420530` 見習い占い師の置換と占いAPI、能力APIの追加 → `ec2d33a2` API強化と可読性向上

`AmateurTeller`（見習い占い師）を対象に、複数の占い系役職(`FortuneTeller`, `PonkotuTeller`等)で重複していた処理を段階的に共通APIへ切り出したシリーズ。

### 10-1. 投票発動の共通API: `SelfVoteManager.HandleAbilityVote`

**追加先**: `Modules/SelfVoteManager.cs`（既存の`SelfVoteManager`クラス内に追加）

```csharp
public static bool HandleAbilityVote(PlayerControl player, byte votedForId, AbilityVoteMode voteMode, string modeMessageKey, string voteMessageKey, Action<byte> useAbility)
```

「対象に投票することで能力を発動する」役職の`CheckVoteAsVoter`内で使う共通処理。`AbilityVoteMode.NomalVote`なら対象へ直接発動、`SelfVote`なら自投票でのトグル・スキップ判定込みの分岐を行う。`AmateurTeller`, `FortuneTeller`, `PonkotuTeller`, `Inspector`, `MeetingSheriff`等がほぼ同一のコードを個別に持っていたうちの1つ(`AmateurTeller`)をこれに移行した。

### 10-2. 役職無効化フック: `RoleBase.AbilityEnabled`

**追加先**: `Roles/Core/RoleBase.cs`（フィールドとして追加。のちに`RegisterAbilityMethod`は`ec2d33a2`で`Modules/Utils/Utils.cs`へ移動）

```csharp
public bool AbilityEnabled = true;
```

能力(Vent/変身/ファントムボタン等を含む広義のアビリティ)を使用できる状態かを表す。`Amnesia`側(`Roles/AddOns/Common/DeBuff/Amnesia.cs`の`Add`/`RemoveAmnesia`)が直接この値を書き換える設計にし、`RoleBase`側はAmnesiaの存在を一切知らない（依存の方向を一方向に保った）。

### 10-3. 能力メソッドの自動審査API: `AbilityGate`

**追加先**: `Modules/AbilityGate.cs`（新規ファイル）

```csharp
public static void Register(Type declaringType, string methodName)
public static void Register(MethodInfo method)
```

指定したメソッドに動的にHarmony Prefixパッチを当て、呼び出しの瞬間に対象インスタンス(`RoleBase`)の`AbilityEnabled`と`SelfVoteManager.Canuseability()`(`ec2d33a2`で追加)を審査する仕組み。falseならメソッド本体を実行せずログのみ出す。呼び出し元は登録済みメソッドを普段通り呼ぶだけでよく、審査の存在を意識しなくてよい。

役職側の登録用ヘルパー(**追加先**: `Modules/Utils/Utils.cs`の拡張メソッド。当初`RoleBase.cs`に`protected`メソッドとして実装したが、`RoleBase`本体の肥大化を避けるため`ec2d33a2`で拡張メソッドへ移設):
```csharp
public static void RegisterAbilityMethod(this RoleBase role, string methodName)
```

### 10-4. 占い系役職共通コンポーネント: `DivinationManager`

**追加先**: `Modules/DivinationManager.cs`（新規ファイル）

```csharp
public DivinationManager(RoleBase owner, Func<PlayerControl, CustomRoles> getDivinationResult)
public void StartDivination(PlayerControl target)
public void CompleteDivination()
public bool TryGetRevealedRole(byte targetId, out CustomRoles role)
public byte PendingTarget { get; }
public bool IsPending { get; }
```

「対象へ投票して仮確保→次の通報のタイミングで結果確定→以後役職が見える」という占い系役職共通の管理コンポーネント。役職側は結果決定ロジック(`getDivinationResult`、例:`AmateurTeller`は`target.GetTellResults(Player)`を素直に返すだけ)だけ用意すればよく、仮確保・矢印表示・確定タイミングの管理は全てこちらに委譲できる。`PonkotuTeller`のような確率で結果が変わる役職は、この`getDivinationResult`を差し替えるだけで対応できる想定（未移行）。

### 10-5. その他新設API

- **追加先**: `Modules/OptionItem/IntegerOptionItem.cs`
  ```csharp
  public static IntegerOptionItem CreateWithRolePrefixedKey(SimpleRoleInfo roleInfo, int idOffset, Enum name, IntegerValueRule rule, int defaultValue, bool isSingleValue, OptionItem parent = null)
  ```
  複数役職で共有したいenum名(例:`AbilityMaxUse`)はそのままに、実際のCSV/翻訳キーだけ`"{役職名}{enum名}"`という役職ごとに一意な文字列にする版。通常の`Create(roleInfo, idOffset, enum, ...)`はenum名をそのままキーにするため、複数役職でenum名を共有すると翻訳キーも共有されてしまう問題への対応。

- **追加先**: `Modules/Utils/UtilsRoletext.cs`
  ```csharp
  public static (Color color, string text) GetTeamDisplay(CustomRoles role)
  ```
  実際の役職名を明かさず、陣営(Crewmate/Impostor/Neutral)だけを色とテキストに変換する。占い系役職の「役職までは見せず陣営だけ見せる」設定で使う。

- **追加先**: `Modules/Utils/Utils.cs`（拡張メソッド）
  ```csharp
  public static bool IsEmergencyButtonPress(this NetworkedPlayerInfo target)
  public static bool IsSelfEmergencyButtonPress(this RoleBase role, PlayerControl reporter, NetworkedPlayerInfo target)
  ```
  `OnReportDeadBody`/`CancelReportDeadBody`のtargetが緊急ボタン(自己通報)によるものかどうかの判定。`RoleBase`本体を肥大化させないため、最初から拡張メソッドとして実装。

- **リネーム**: `Roles/Core/RoleBase.cs`の`protected enum GeneralOption`内、`cantaskcount` → `requiredTaskCount`（可読性のため。参照していた14ファイル全てを追従）

### 10-6. `AmateurTeller`側の変化

`Roles/Crewmate/AmateurTeller.cs`は上記のAPI群を使う形に全面的に書き換えられた。主な変更:
- `UseTarget`/`Targets`(占い対象の仮確保・確定リスト)を廃止し、`DivinationManager`のインスタンスに委譲
- `Use`フィールド(`UseTarget != byte.MaxValue`と常に等価だった冗長な状態)を削除
- 未使用だった`Divination`辞書(占いテラー系のコピペ残骸)を削除
- `CheckVoteAsVoter`/`CancelReportDeadBody`内の長い`&&`連結条件を、名前付きの真偽値変数やプロパティ(`CanUseTellAbility`, `IsSelfEmergencyButtonPress`等)に分解
- ネストした三項演算子(`GetRoleStatusText`)を、名前付き変数を使った1段の三項演算子に分解
- コンストラクタを`AssignOptions()`(オプション値の反映)と初期化処理に分離

---

## 付録: 今回のセッションでの議論・確認事項（コードに残らない設計判断）

- `SendOption.None`と`SendOption.Reliable`は別チャンネルであり、互いの送信順序は保証されない。同種同士(Reliable-Reliable)なら順序が保証される。
- `Main.LagTime`はリージョンに応じて起動時に一度だけ決まる固定の推測値（0.23秒 or 0.43秒）であり、実測pingではない。実際のラグがこれを超えると、時間待ちに依存した順序保証は崩壊する。
- Hazelネットワークは、ping応答が6回連続で欠落すると切断される。Reliableメッセージ自体がロスしても、接続が生きている(ping欠落6回未満)限り再送により最終的に正しい順序で届く。これが`SendOption.Reliable`が「遅延はあっても結果は必ず正しい」という保証を持つ理由。
- `RpcProtectedMurderPlayer`が`SendOption.None`を使っているのは、過去の不具合修正の結果ではなく、演出目的の高頻度呼び出し(見た目のクールダウンリセット演出など)に対しては妥当な選択だったためと推測される。今回のような「一発限りの正確な値設定」という用途では、順序保証の面で不向きだった。
