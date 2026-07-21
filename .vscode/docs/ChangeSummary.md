# フォーク後の変更まとめ（簡易版）

フォーク元(`paizakun/TOH-PKO-Practice-And-Modify`)から現時点までに、自分(Ereshkigal)が加えた**実装の変更・追加**を新しい順にまとめたもの。
詳細は [ChangeDetails.md](ChangeDetails.md) を参照。

## コミット済みの変更

| コミット | 内容 |
|---|---|
| `9d17d205` | READMEをフォークに沿う内容に変更（ドキュメントのみ） |
| `695626a4` | 練習モード(Practice)を新規作成。`/cmd cr`（役職変更コマンド）を有効化した専用モード |
| `6b69711a` | `/cmd cr`でシェイプシフター系の役職に変更した際、能力が使えなくなる問題を修正。Expressの速度解除タイミングを`OnDestroy`に修正 |
| `efbb969b` | EvilMovingのキルクールが`Main.NormalOptions.KillCooldown`（変動する共有値）を直接参照していたのを、生成時にキャッシュする方式に修正 |
| `8c78dde7` / `18f2881b` | 個人環境差分の`TownOfHost.csproj`をgit管理から除外（環境設定のみ） |
| `44200a0f` | **速度バフ/デバフ統一API `OperatePlayerSpeedModifier` を新規実装**。毎フレーム処理を一元化する`TickManager`も新設。検証用に`SpeedAdderTest`役職を追加 |
| `be9e8a58` | **キルクールダウン同期API `SyncKillCooldown` を新規実装**（後にAPI分割）。検証用に`KillCoolSyncTest`役職を追加 |
| `27daed2c` | GlobalChat接続エラーログが再試行毎に出続けるのを、初回のみ通知するように抑制 |
| `0a792121` | **練習モード専用の`/cmd setconfig`コマンドを追加**。チャットから役職の設定値(出現率以外のオプション)を確認・変更できるようにした |
| `5aeb6479` | `SyncKillCooldown`が実際の秒数の半分になっていた不具合を修正（RPCの仕様上*2で送る補正を追加） |
| `3039c50f` | `KillCoolSyncTest`をバニラのPhantomボタン仕様に合わせて調整 |
| `097d8d7d` | **`/cmd`コマンドが秘匿されるべき内容を全クライアントに一時的に漏らしてしまう不具合を修正**。`InnerNetServer.Broadcast`をパケット単位で検査し、`/cmd`コマンドは中継せずホストが直接処理する方式に変更 |
| `21467bb7` | **キルクールダウン同期APIを`SetMaxKillCooldown`/`SetCurrentKillCooldown`に分割**。`RpcProtectedMurderPlayer`に`SendOption`を指定できるオーバーロードを追加し、`SetCurrentKillCooldown`は`SendOption.Reliable`で送信することで送信順序をタイムベースの遅延(`LagTime`)に依存せず保証。`KillCoolSyncTest`に`IKiller.CalculateKillCooldown()`を実装し、実際のキル成立時のクールダウンリセットで正しくMax値が使われるように修正 |
| `c5c0d777` | **投票発動の共通API `SelfVoteManager.HandleAbilityVote` を新設**。`ISelfVoter`系役職に共通する「通常投票→即発動」「セルフ投票→トグルして発動」というパターンを1メソッドに集約 |
| `63862347` | **役職無効化フック`RoleBase.AbilityEnabled`を新設**。Amnesiaが`Add`/`RemoveAmnesia`のタイミングでこのフラグを直接書き換えることで、能力停止状態を役職側に伝える設計に変更 |
| `f4420530` | **`AmateurTeller`を`DivinationManager`/`AbilityGate`という新設APIを使う形に再実装**。占い系役職共通の「開始→完了→開示」を`DivinationManager`に、能力メソッドの自動審査を`AbilityGate`(Harmonyの動的パッチ)に切り出した。`IntegerOptionItem.CreateWithRolePrefixedKey`、`UtilsRoleText.GetTeamDisplay`も新設。`GeneralOption.cantaskcount`を`requiredTaskCount`にリネーム |
| `ec2d33a2` | **APIの置き場所を整理**。`RoleBase`本体に直接生えていた`RegisterAbilityMethod`と、能力無効化に伴う緊急ボタン判定を`Modules/Utils/Utils.cs`の拡張メソッドに移動し、`RoleBase`自体を軽量化。`AbilityGate`のPrefixが`SelfVoteManager.Canuseability()`も合わせて審査するように強化 |
