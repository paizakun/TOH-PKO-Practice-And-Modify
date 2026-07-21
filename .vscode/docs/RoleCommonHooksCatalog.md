# 役職共通フック・登録式APIカタログ

役職(`Roles/`配下)を実装・改修するときに使える「既存の共通化ポイント」の一覧。名前表示マーク専用の詳細は[`RoleTextApiStandard.md`](RoleTextApiStandard.md) / [`RoleTextWritingGuide.md`](RoleTextWritingGuide.md)を参照。技術基盤(バニラRPC・送信の仕組み)は[`VanillaRpcCatalog.md`](VanillaRpcCatalog.md)を参照。

## 背景

Mark系の共通化を進める中で、「共通化できるはずなのに個別実装のまま」の候補を見つけるには、まず**今すでにある共通化パターンの型**を把握しておく必要がある。ここでは`RoleBase`のvirtualメソッドと`CustomRoleManager`の登録式フィールドを棚卸しする。新しい役職を書くとき・既存役職の重複に気づいたときに、まずここを見て「使えるフックが既にないか」を確認してから実装すること。

## 2つの共通化の形

### 形式1: `RoleBase`のvirtualメソッド override

役職クラスが`RoleBase`を継承し、自分に関係するフックだけをoverrideする。**1インスタンス(1プレイヤー)の振る舞いを定義する**のに使う。役職共通の呼び出し側(ゲームループ・パッチ)が、そのインスタンスのメソッドを直接呼ぶ。

### 形式2: `CustomRoleManager`の登録式フィールド(`HashSet<Func<...>>`/`HashSet<Action<...>>`)

役職・Addonが初期化時に自分の静的関数を`.Add(...)`で登録し、**役職の種類を問わず全員に対して横断的に評価される**処理を追加する。呼び出し側は登録済み関数を全部ループで呼ぶ。

判断基準はMark系と同じ:「1インスタンスの状態だけで完結するか(形式1)」「役職を問わず横断的に効くべきか(形式2)」。

## `RoleBase`のvirtualメソッド一覧(`Roles/Core/RoleBase.cs`)

約45個。カテゴリ別に整理。

| カテゴリ | メソッド/プロパティ |
|---|---|
| ライフサイクル | `Add()`, `ChengeRoleAdd()`, `OnDestroy()`, `OnSpawn()`, `StartGameTasks()`, `ChangeColor()`, `OnLeftPlayer()` |
| 通信/RPC | `ReceiveRPC()`, `OnVentilationSystemUpdate()` |
| キル/マーダー | `OnCheckMurderAsTarget()`, `OnMurderPlayerAsTarget()`, `OnDead()` |
| シェイプシフト/バニッシュ | `OnShapeshift()`, `CanDesyncShapeshift`, `CheckShapeshift()`, `CheckVanish()` |
| 能力ボタン | `CanUseAbilityButton()`, `OnFixedUpdate()`, `GetAbilityButtonText()`, `OverrideAbilityButton()` |
| ベント | `CanClickUseVentButton`, `OnEnterVent()`, `CanVentMoving()` |
| 通報/会議/投票 | `OnReportDeadBody()`, `OnStartMeeting()`, `MeetingAddMessage()`, `CheckVoteAsVoter()`, `ModifyVote()`, `OnExileWrapUp()`, `AfterMeetingTasks()`, `CancelReportDeadBody()`, `VotingResults()`, `AfterMeetingRole` |
| タスク/サボタージュ | `OnCompleteTask()`, `OnInvokeSabotage()`, `OnSabotage()`, `AfterSabotage()`, `CanTask()` |
| 表示系(名前・マーク・色) | `NotifyRolesCheckOtherName`, `OverrideDisplayRoleNameAsSeen/Seer()`, `OverrideTrueRoleName()`, `OverrideProgressTextAsSeer/Seen()`, `GetRoleStatusText()`, `GetMark()`, `GetLowerText()`, `GetSuffix()`, `AllEnabledColor`, `GetTemporaryName()` |
| 勝利判定/その他 | `ApplyGameOptions()`, `TellResults()`, `CheckGuess()`, `CheckWinner()`, `HaveAddRole()`, `Misidentify()` |

新しい役職固有の振る舞いを実装するときは、まずこの表にドンピシャの既存フックがないか確認する。無ければ形式2、それも無ければ初めて新規のvirtualメソッド追加を検討する。

## `CustomRoleManager`の登録式フィールド一覧(`Roles/Core/CustomRoleManager.cs`)

役職・Addonが`.Add(...)`で登録する`HashSet`系フィールド。**この形式のフィールドは`CustomRoleManager.cs`にしか存在しない**(Modules配下には同様のパターンは無い)。

| フィールド | シグネチャ | 用途 |
|---|---|---|
| `MarkOthers` | `Func<PlayerControl, PlayerControl, bool, string>` | 名前表示Mark(詳細は`RoleTextApiStandard.md`) |
| `LowerOthers` | `Func<PlayerControl, PlayerControl, bool, bool, string>` | 名前表示LowerText |
| `SuffixOthers` | `Func<PlayerControl, PlayerControl, bool, string>` | 名前表示Suffix |
| `OnEnterVentOthers` | `Func<PlayerPhysics, int, bool>` | ベント侵入確定後の横断フック |
| `OnCompleteTaskOthers` | `Action<PlayerControl, bool>` | タスク完了時の横断フック |
| `OnMurderPlayerOthers` | `Action<MurderInfo>` | 他役職のキル処理への割り込み |
| `OnFixedUpdateOthers` | `Action<PlayerControl>` | 毎フレーム系の横断フック |
| `RoleHandlers` | `Dictionary<CustomRoles, HashSet<Action<MessageReader, byte>>>` | 役職別のRPCハンドラ登録(役職ごとにグルーピングされる点が他と異なる) |

**`MarkOthers`/`LowerOthers`/`SuffixOthers`は、`.Add`による事前登録以外に、`RoleBase.GetBroadcastMark`/`GetBroadcastLowerText`/`GetBroadcastSuffix`をoverrideする方法も使える**(`OnDead`と同じ、`AllActiveRoles`を使ったブロードキャスト方式。新規実装ではこちらを優先する)。詳細は[`RoleTextApiStandard.md`](RoleTextApiStandard.md)を参照。命名を`GetMarkOthers`にしなかったのは、既存の役職ファイル24個・32箇所が方式2-B(HashSet登録)用の静的関数名として既にその名前を使っており、同名にすると`CS0114`(継承メンバーの隠蔽)警告と実質的な機能欠落が起きるため。

命名規則はMark系(`RoleTextApiStandard.md`)以外は現状明文化されていない。登録先が増えたときは同様に「登録関数名は登録先の名前に対応させる」規則を検討する。

**`RoleBase`に新しいvirtualメソッドを追加する際の注意**: `MagicalGirl`/`Assassin`/`JackalWolf`/`Villain`は「変身先/憑依先の役職」を`addRole`という別の`RoleBase`インスタンスとして保持し、ほぼ全virtualメソッドを`addRole?.Xxx(...)`と手動で中継している。`AllActiveRoles`にはラッパー自身のインスタンスしか登録されないため、**新しいvirtualメソッドを追加するたびに、この4ファイルにも中継コードを追加しないと、変身先の役職でそのメソッドが黙って発火しなくなる**(コンパイルエラーにはならず、実行時に静かに欠落するため気づきにくい)。

## ハイブリッド式の実例: KillCooldown

「形式1(値を返すだけ)」と「共通適用ヘルパー」を組み合わせた、参考にすべきパターン。

- `Roles/Core/Interfaces/IKiller.cs` — `float CalculateKillCooldown() => Options.DefaultKillCooldown;` というdefault interface method。キル可能な役職は`IKiller`を実装し、クールダウンを変えたい場合だけoverrideする
- `Modules/ExtendedPlayerControl/ExtendedPlayerControl.cs` — `SetKillCooldown()`/`SetMaxKillCooldown()`/`SetCurrentKillCooldown()`/`ResetKillCooldown()`という適用側の拡張メソッド群。`ResetKillCooldown()`が内部で`(player.GetRoleClass() as IKiller)?.CalculateKillCooldown()`を呼び、IKillerから値を取得して実際に適用する

つまり「**役職側は値/条件を返すだけ、実際の適用(RPC送信含む)は共通ヘルパー側が一括して行う**」という責務分離。Mark系(`GetMark`→文字列を返すだけ、RPC送信は`NotifyRoles`側)と全く同じ構造であり、**今後の共通化候補もこの形に寄せるのが良い**(役職側にRPC送信や副作用のあるコードを直接書かせない)。

## `Modules/`配下の共通ヘルパー群(概要)

- `Modules/Utils/*.cs` — 役職から広く呼ばれる汎用ヘルパー(`Utils.cs`, `UtilsRoletext.cs`, `UtilsRoleInfo.cs`, `UtilsOption.cs`, `UtilsTask.cs`, `UtilsName.cs`, `UtilsNotifyRoles.cs`, `UtilsTextComponent.cs`, `UtilsLog.cs`, `UtilsLoad.cs`)
- `Modules/ExtendedPlayerControl/ExtendedPlayerControl.cs` — `PlayerControl`拡張メソッド集(KillCooldown操作等、役職から最も広く呼ばれる部類)
- `Modules/ExtendedPlayerControl/ExtendedRpc.cs` — RPC送信の拡張メソッド(詳細は`VanillaRpcCatalog.md`)
- 機能別の共通モジュール: `Modules/AbilityGate.cs`(能力使用可否ゲート), `Modules/GuessManager.cs`(推理系), `Modules/DivinationManager.cs`(占い系), `Modules/TargetArrow.cs`(矢印表示), `Modules/Tooltip.cs`, `Modules/Camouflague.cs`, `Modules/OperatePlayerSpeedModifier.cs`(速度変更), `Modules/VentManager.cs`, `Modules/RPC.cs`(RPC基盤)

「役職固有の実装を書く前に、まずこのカタログと`Modules/`のファイル名を見て、既存の共通ヘルパーがないか確認する」ことを新規実装時の最初のステップとする。

## 未解決の検討事項

- 「共通化できるはずなのに個別実装のまま残っている候補」は、このカタログだけでは見つからない。役職ファイル間の類似コードをgrepで横断比較する追加調査が必要(次の着手候補)
- `MarkOthers`以外の登録式フィールド(`OnMurderPlayerOthers`等)にも、命名規則の統一が必要かどうかは未検討
