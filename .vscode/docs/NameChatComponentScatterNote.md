# 名前表示・チャット系コンポーネントの分散メモ（未着手）

`AmateurTeller`の整理中に気づいた、別テーマの懸念事項。**まだ調査・着手していない**、次にこのテーマを扱うときの入り口用メモ。

## 気になっていること

名前表示(役職名・マーク・矢印・色)まわりのロジックが、単一の「名前表示コンポーネント」にまとまっておらず、複数ファイルに分散している。チャット系コンポーネントも同様に散らばっている感覚がある。

## 関連が確認できたファイル（洗い出し途中）

`MarkOthers` / `LowerOthers` / `SuffixOthers` / `GetDisplayRoleName` / `GetMisidentify` を grep した限りで関与しているファイル:

- `Modules/Utils/UtilsRoletext.cs` — 役職名・マーク文字列の構築ロジック本体
- `Modules/Utils/UtilsNotifyRoles.cs` — 全プレイヤー×全プレイヤーを走査して名前を配信するループ
- `Modules/NameColorManager.cs`
- `Modules/Utils/UtilsOption.cs`
- `Modules/ExtendedPlayerControl/ExtendedPlayerControl.cs` — `GetMisidentify`等の拡張メソッド
- `Modules/GameMode/MurderMystery.cs`, `Modules/GameMode/SuddenDeathMode.cs` — ゲームモード側からもMarkOthers等に登録
- `Patches/ChatBubblePatch.cs`, `Patches/ChatCommandPatch.cs` — チャット表示側からの参照
- `Patches/HudPatch.cs`, `Patches/MeetingHudPatch.cs`, `Patches/IntroPatch.cs`, `Patches/RoleGuideButtonPatch.cs`, `Patches/HauntMenuMinigamePatch.cs`, `Patches/PlayerContorols/FixedUpatePatch.cs`, `Patches/onGameStartedPatch.cs` — 各UI/イベントのタイミングで名前構築を呼び出す側

## 次にやること（案）

1. 上記ファイルを実際に読み、責務ごとに整理する（構築ロジック／配信ループ／呼び出し元、で本当に3層に分かれているか確認）
2. `CustomRoleManager`の`MarkOthers`/`LowerOthers`/`SuffixOthers`のような「用途ごとに似た形のHashSet<Func<...>>が並立している」構造が本当に整理の余地があるか検討
3. チャット系(`ChatBubblePatch`, `ChatCommandPatch`)も同様に分散度を確認し、必要なら整理方針を立てる

`RoleFileStandard.md`（役職ファイル単体の並び順規約）とは別スコープ。こちらはモジュール間の責務分割の話なので、着手する際は別のドキュメントとして扱う。

## 着手済み(2026-07-21)

`Modules/Utils/UtilsTextComponent.cs` を新設し、以下の重複を集約した:

- `BuildSubRolesFromAddon(state, player)`: `UtilsRoleText.GetTrueRoleNameData`と`GetSubRolesText`で丸ごと重複していた、RoleAddAddonsのGiveXxxフラグからSubroleリストを構築する処理(約55行)を共通化。
- `BuildSelfDecoration(seer, isForMeeting)` / `BuildTargetDecoration(seer, target, isForMeeting)`: `UtilsNotifyRoles.NotifyRoles`/`NotifyMeetingRoles`で4箇所(self/target × タスク/会議)重複していたMark/LowerText/Suffix構築を共通化。タスクフェーズ/会議フェーズで恋人マーク表示条件(OneLoveの扱い)やTaskBattle/追放者Suffixの有無がわずかに異なる箇所は`isForMeeting`引数で分岐し、従来の挙動を再現。
- `NameColorManager.GetColoredRealName(seer, target, isMeeting)`: `GetRealName`+`ApplyNameColorData`の2手順パターンが`ChatCommandPatch.cs`と`RpcMeetingColorName`(2箇所)で重複していたため共通化。

なお調査の結果、`ChatBubblePatch.cs`は当初想定した「GetMisidentify→ApplyNameColorData→GetRealName」の3手順重複パターンには該当せず(ローカルプレイヤー分岐は色のみ個別構築、他プレイヤー分岐は元々`ApplyNameColorData`単体呼び出し)、変更不要と判断した。

## 見送った範囲(次の入り口)

- `Patches/HudPatch.cs` / `Patches/PlayerContorols/FixedUpatePatch.cs` / `Patches/MeetingHudPatch.cs` には、NotifyRoles/NotifyMeetingRolesと同型のMark/Suffix構築コードがローカルHUD即時反映用に再実装されている(RPC送信を伴わない別経路)。今回はリスクを抑えるためtouchしていない。`UtilsTextComponent.BuildSelfDecoration`/`BuildTargetDecoration`に置き換えられる可能性が高いので、次に着手する際はここから読むとよい。
- `UtilsRoleText.GetWikitext`(ドキュメント生成)はテーマが異なるため未整理のまま。
