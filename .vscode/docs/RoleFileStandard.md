# 役職ファイルの構成標準（フック中心）

役職クラス(`RoleBase`派生)内のメンバー並び順の標準。定義・実装・フックが混在して読みにくくなるのを防ぐため、以下の順序を基本とする。
既存ファイルを触る機会があれば、この順序に合わせて整理する（一括リライトは行わない）。

## 並び順

1. `RoleInfo`（役職定義。`SimpleRoleInfo.Create`等）
2. コンストラクタ
3. オプション関連
   - オプション値を保持するフィールド(`static OptionItem ...`)
   - `AssignOptions`（オプション値をフィールドへ反映）
   - `SetupOptionItem`（オプション項目の生成）
   - `enum Option`
4. その他のフィールド（能力の内部状態など）
5. ライフサイクルフック（`Add`, `OnDestroy`）
6. 判定・状態プロパティ（`CanUseXxx`など、フックから参照される内部条件）
7. ゲームプレイフック（`override`されるコールバック群。`CheckVoteAsVoter`, `OnReportDeadBody`, `CancelReportDeadBody`, `OnCompleteTask`, `OnMurderPlayerAsTarget`等）
   - フックの直後に、そのフックからしか呼ばれない専用メソッド（例: `UseVoteAbility`）を続けて置く
8. 表示系フック（`GetRoleStatusText`, `GetLowerText`, `OverrideDisplayRoleNameAsSeer`等。`MarkOthers`等への登録関数の命名規則は[`RoleTextApiStandard.md`](RoleTextApiStandard.md)を参照）
9. RPC（`SendRPC`, `ReceiveRPC`）
10. 実績（`achievements`フィールド, `Load`）

## 狙い

- 「このクラスがどのフックに反応して何をするか」を上から読めば把握できる状態を目指す
- オプション定義・内部状態といった「準備」部分と、フックによる「振る舞い」部分を分離する
- 参考実装: [`Roles/Crewmate/AmateurTeller.cs`](../../Roles/Crewmate/AmateurTeller.cs)

## 命名・実装の指針

- **共通の実装はなるべく他役職と共有できる場所(`Modules/`配下等)へ移す**。複数役職で同じロジックが重複しているなら、専用の共通APIを切り出す(例: `SelfVoteManager.HandleAbilityVote`)。役職クラス側は「自分固有の値・条件」だけを渡す形にする
- **コールバック登録**(`CustomRoleManager.MarkOthers`等への`.Add(...)`)を渡す関数には、登録先の型・いつ呼ばれるか(呼び出しループの場所や頻度)をXMLコメントで明記する。呼び出し側の登録行にも短い一言コメントを添える
- **複数インスタンスを保持する`static`コレクション**(`static HashSet<RoleClass>`等)は、`OnDestroy`で`Remove(this)`を使う。`Clear()`は同ラウンド中に生存している他インスタンスまで巻き込んで消してしまうため使わない(ゲーム中の役職変更コマンド等で1インスタンスだけが破棄されるケースがあるため)
- **タイポは気づいた範囲で修正する**。ただし影響ファイル数を確認し、1〜2ファイルで完結するものはその場で直し、広範囲(enum値・共通メソッド名・共有リソースキー等)に及ぶものは影響範囲を提示してから実施する
