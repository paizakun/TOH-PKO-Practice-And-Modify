# 役職合成(addRole委譲)パターンのカタログ

`MagicalGirl`/`Assassin`/`JackalWolf`/`Villain`のように、1人のプレイヤーが「別の役職を内部に持つ」形で複数役職相当の振る舞いを実現している役職の、`RoleBase`各virtualメソッドの委譲ルールを分類する。[`RoleCommonHooksCatalog.md`](RoleCommonHooksCatalog.md)で触れた「1人が複数役職を同時に持てるようにすべきか」という議論の土台として、まず現状の実装がどう書かれているかをカタログ化した(コード変更はしていない)。

## 背景

`RoleBase`のvirtualメソッドは約45個あるが、これらは「文字列を返して連結するだけ」のもの(Mark/Suffix系)ばかりではなく、「1つの答えを返す」もの(`CanUseAbilityButton()`, `CalculateKillCooldown()`など)が大半を占める。1人のプレイヤーが複数の役職を同時に持てるようにする場合、後者の「1つの答え」をどちらの役職から採用するかという**結合ルール**が全メソッド分必要になる。現状はこれを`RoleBase`インスタンスを2つ持つラッパー役職(`addRole`/`AddRole`という別インスタンスを内部に保持するクラス)が、メソッドごとに手書きしている。この手書きルールを分類する。

## 4つの実装の性質の違い

| 役職 | addRoleの割当タイミング | 変化の向き | addRole保持フィールド |
|---|---|---|---|
| `MagicalGirl` | 会議中の投票操作で任意のタイミングに変身 | 何度でも変身⇔解除を繰り返す(`ClearTransform`で解除) | `addRole`(private) |
| `Assassin` | ゲーム開始時の`Add()`で1回だけ割当 | ゲーム中変化しない(固定) | `AddRole`(internal程度) |
| `JackalWolf` | ゲーム開始時の`Add()`で1回だけ割当 | ゲーム中変化しない(固定) | `AddRole` |
| `Villain` | ゲーム開始時に割当、条件成立で`Realize()`により**恒久的に**破棄 | 変装状態→覚醒状態への一方通行(逆戻りしない) | `addRole`(private) |

`Assassin`/`JackalWolf`は「ゲーム中ずっと2役職ぶんの振る舞いを持ち続ける」固定合成、`MagicalGirl`は「一時的な合成を繰り返す」動的合成、`Villain`は「合成状態から単一役職状態への一方通行の遷移」という、性質が全く異なる3種類が同じ`addRole`委譲という実装手段を共有している。

(参考: `AlienHijack`は`addRole`委譲ではなく、`CustomRoleManager.AllActiveRoles`の辞書エントリ自体を別インスタンスに差し替える、また異なる第4の方式を使う。[`RoleCommonHooksCatalog.md`](RoleCommonHooksCatalog.md)を参照)

## 結合ルールの3パターン

### パターン1: 排他ゲート(どちらか一方の答えだけを採用する)

最も多いパターン。フラグ(`IsTransformed`/`Realized`)で完全に切り替え、両方が同時に効くことはない。

```csharp
// MagicalGirl.cs
public override bool CanUseAbilityButton() => IsTransformed && (addRole?.CanUseAbilityButton() ?? false);
public float CalculateKillCooldown() => IsTransformed && addRole is IKiller killer ? killer.CalculateKillCooldown() : Options.DefaultKillCooldown;

// Villain.cs
public override bool OnCompleteTask(uint taskid) => !Realized ? (addRole?.OnCompleteTask(taskid) ?? true) : true;
```

`MagicalGirl`/`Villain`の大半のメソッドがこの形。

### パターン2: 素通し(自分側に競合する実装が無いので、判定不要)

`Assassin`/`JackalWolf`(固定合成、フラグ分岐が存在しない)のほぼ全メソッドがこの形。`MagicalGirl`/`Villain`にも一部ある。

```csharp
// Assassin.cs / JackalWolf.cs (フラグなし、常に委譲)
public override bool OnEnterVent(PlayerPhysics physics, int ventId) => AddRole?.OnEnterVent(physics, ventId) ?? true;
public override void OnShapeshift(PlayerControl target) => AddRole?.OnShapeshift(target);
```

自分自身が独自の実装を持たないため、`addRole`が`null`のとき(`Assassin`/`JackalWolf`は未割当時、`MagicalGirl`/`Villain`は変身/覚醒前後の一方の状態)は`?? default`側が自然に採用されるだけで、明示的なフラグ判定すら不要。

### パターン3: 合成(両方の結果を組み合わせる)

`MagicalGirl`にのみ見られる、最も複雑なパターン。自分自身の状態表示と`addRole`側の内容を**両方**実行し、結果を連結する。

```csharp
// MagicalGirl.cs GetLowerText: 変身中でも自分の残りターン数表示とaddRoleのLowerTextを両方連結
if (IsTransformed)
{
    var addText = addRole?.GetLowerText(seer, seen, isForMeeting, isForHud) ?? "";
    ...
    return addText == "" ? wrapped : addText + "\n" + wrapped;
}

// MagicalGirl.cs AfterMeetingTasks: addRole側の処理 + 自分自身の残りターン数カウントダウンを両方毎回実行
public override void AfterMeetingTasks()
{
    if (IsTransformed && transformedRole is not CustomRoles.Bakery)
        addRole?.AfterMeetingTasks();
    if (!AmongUsClient.Instance.AmHost || !IsTransformed) return;
    ...
    remainingTurns--;
    ...
}

// MagicalGirl.cs CheckWinner: addRole側の勝利判定 + 自分固有の勝利ブロック処理(Staff特有)を両方実行
public override void CheckWinner(GameOverReason reason)
{
    if (IsTransformed)
    {
        addRole?.CheckWinner(reason);
        if (... transformedRole is CustomRoles.Staff ...) CustomWinnerHolder.CantWinPlayerIds.Add(Player.PlayerId);
    }
}
```

`MagicalGirl`が「変身中も自分の変身残り時間という固有情報を持ち続ける」役職だからこそ必要になるパターンで、`Assassin`/`JackalWolf`/`Villain`には存在しない(これらは合成後、自分固有に維持すべき状態がほぼ無いため)。

## 今回のセッションでの関連事項

`RoleBase`に`GetBroadcastMark`/`GetBroadcastLowerText`/`GetBroadcastSuffix`を追加した際、この4クラスへの中継コード追加を最初忘れており、ビルドは通るが実行時に静かに機能しないという見落としがあった(詳細は[`RoleCommonHooksCatalog.md`](RoleCommonHooksCatalog.md)参照)。新規追加した3メソッドはいずれも**パターン2(素通し)**として実装済み。

## 未解決の検討事項(「1人が複数役職を持てるようにする」の実現性について)

- 上記3パターンのうち、**パターン3(合成)は役職固有のゲームデザイン判断そのもの**であり、汎用ルールに落とし込むのが最も難しい。「文字列を返すメソッドは常に連結」のような単純な汎用ルールにできるのはMark/Suffix系のような一部のメソッドに限られ、`AfterMeetingTasks`(void)や`CheckWinner`(void、副作用のみ)のように戻り値のないメソッドを「合成」する場合、実行順序(どちらを先に呼ぶか)まで役職ごとに意味を持つ可能性がある
- **パターン1(排他ゲート)を汎用化する**なら、「どちらの役職が優先されるか」を決めるルール(例: 常に`addRole`優先、フラグで切替可能にする等)を導入する余地はありそうだが、`Villain`の`Realized`のように「両方向ではなく一方通行の遷移」という制約も個別にあるため、汎用フラグ1つでは表現しきれない可能性がある
- 「1人が複数役職を持てる」機構を本気で作るなら、まずこの3パターンをカバーする最小限のメソッド群(全45個ではなく、実際に4クラスで使われている範囲)から着手し、汎用化の実現性を検証するのが良さそう
