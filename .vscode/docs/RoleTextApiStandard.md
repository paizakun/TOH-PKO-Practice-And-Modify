# 名前表示マークAPI整備方針

役職・Addonが名前表示テキスト(Mark/LowerText/Suffix/SubRoleMark)を実装する際に、どのAPIを使うべきかを定義する。
「どう書くか」の実践的な手順は[`RoleTextWritingGuide.md`](RoleTextWritingGuide.md)を参照。ファイル内のメンバー並び順規約は[`RoleFileStandard.md`](RoleFileStandard.md)を参照(本ドキュメントとは役割が異なり、重複しない)。

## 背景

`UtilsTextComponent`([`../../Modules/Utils/UtilsTextComponent.cs`](../../Modules/Utils/UtilsTextComponent.cs))への集約作業中に調査した結果、名前表示マークの実装スタイルが役職ごとに大きくバラついていることが分かった。

- `CustomRoleManager.MarkOthers`/`LowerOthers`/`SuffixOthers`への登録関数(44件)の命名が、`GetMarkOthers`/`GetLowerTextOthers`(多数派)の他に、`OtherMark`/`OtherArrow`/`GetTrashMarkOthers`/`AbilityMark`/`ImpostorMark`/`GetCaughtLowerText`など最低7系統に分裂している
- [`Roles/Impostor/Sniper.cs`](../../Roles/Impostor/Sniper.cs)は`GetMarkOthers`という名前の関数を`SuffixOthers`に登録しており、関数名と実際の登録先パイプラインが意味的に一致しない
- Addon/Buff/DeBuff系34クラスは`SubRoleMark`という**静的な固定文字列プロパティ**方式を使っており、通常役職の`GetMark`(**関数呼び出し**)方式とはAPIの形自体が異なる

これらは**今回のドキュメント整備だけで一括修正はしない**(44+133+34箇所に及ぶため、ライブの対人戦Modでは影響範囲が大きすぎる)。新規に書くコードからこの方針に従い、既存コードは改修の機会があるときに順次寄せていく。

## 3つの拡張点

### 1. `RoleBase.GetMark` / `GetLowerText` / `GetSuffix` のoverride

役職クラス([`Roles/Core/RoleBase.cs`](../../Roles/Core/RoleBase.cs))が持つ仮想メソッド。**その役職自身の内部状態を見て、自分固有のマークを組み立てる場合**に使う。

- 例: [`Arsonist.GetMark`](../../Roles/Neutral/Arsonist.cs)は自分が持つ`IsDoused`辞書を見て`▲`/`△`を返す
- `seer`/`seen`の両方が渡ってくるが、実行されるのは常に「**seerの役職クラス**」側のメソッド(target側の役職クラスではない)。つまり「seerが特定の役職を持っているときだけ、targetに対してどう見えるか」を表現するAPIである

### 2. 役職を問わず横断的に効くMark/LowerText/Suffix — 2つの実現方法

**役職の種類を問わず、全seer×全targetの組み合わせに対して無条件に評価してよい、横断的なマーク**を追加したい場合、以下の2通りの実現方法がある。

#### 2-A. `RoleBase.GetBroadcastMark` / `GetBroadcastLowerText` / `GetBroadcastSuffix` のoverride(新規実装ではこちらを優先)

`CustomRoleManager.AllActiveRoles`(`Dictionary<byte, RoleBase>`、全プレイヤーの役職インスタンスのレジストリ)を使い、`.Add`による事前登録なしで、全役職インスタンスに対して自動的にブロードキャストされる。詳細な仕組み・実装例は[`RoleCommonHooksCatalog.md`](RoleCommonHooksCatalog.md)を参照。

**命名について**: 素直に付けるなら`GetMarkOthers`のような名前にしたいところだが、**既存の役職ファイル24個・32箇所が、方式2-B(HashSet登録)用の静的関数名として既に`GetMarkOthers`/`GetLowerTextOthers`という名前を使っている**。同じ名前をvirtualメソッドに使うと、既存のstatic関数が意図せず基底クラスのメンバーを隠す(`CS0114`警告、`override`ではなく`new`相当の隠蔽になり、ブロードキャストされない)という実害が出るため、あえて`GetBroadcastMark`のように明確に別の名前にしている。

**重要な注意点(`addRole`委譲パターンとの相性)**: `MagicalGirl`/`Assassin`/`JackalWolf`/`Villain`のように、「変身先/憑依先の役職」を`addRole`という別の`RoleBase`インスタンスとして内部に保持し、ほぼ全virtualメソッドを`addRole?.Xxx(...)`と手動で中継している役職がある。`AllActiveRoles`にはラッパー側(`MagicalGirl`等)のインスタンスしか登録されず、`addRole`は登録されないため、**`GetBroadcastMark`等を新しく追加するたびに、これらのラッパー役職側にも中継コードを追加しないと、変身先の役職のブロードキャストが黙って発火しなくなる**。`RoleBase`に新しいvirtualメソッドを追加するときは、必ず`addRole`委譲パターンを持つ役職(現状4つ)にも同じ中継コードを追加すること。

#### 2-B. `CustomRoleManager.MarkOthers` / `LowerOthers` / `SuffixOthers` への登録(既存44箇所、既存コードとの整合が必要な場合の代替)

`.Add(...)`で静的関数を事前登録する従来方式。`CustomRoleManager.cs`の`GetMarkOthers`/`GetLowerTextOthers`/`GetSuffixOthers`(これは方式2-Aとは別の、CustomRoleManager側の集約static関数)が、登録された関数と2-Aのブロードキャスト結果の両方を結合して返す(内部で両方式を連結している)。

**命名規則(今回明文化):登録関数名は必ず登録先パイプライン名に対応させる**

| 登録先 | 命名 | 例 |
|---|---|---|
| `MarkOthers` | `GetXxxMark` | `GetDontReportMark`([`ReportDeadBodyPatch`](../../Patches/onGameStartedPatch.cs)) |
| `LowerOthers` | `GetXxxLowerText` | `GetCaughtLowerText`(`Spider.cs`) |
| `SuffixOthers` | `GetXxxSuffix` | - |

[`Sniper.cs`](../../Roles/Impostor/Sniper.cs)のように「Mark用の名前の関数をSuffixOthersに登録する」というパイプラインと命名のズレは、今後の新規実装では禁止する。既存のズレは改修時に是正する。新規実装は原則2-A(virtual override)を使い、この方式は既存コードの改修時のみ使う。

### 3. Addon/Buff/DeBuffの `SubRoleMark` 静的プロパティ

**Subroleとして常に固定の短い記号(絵文字1〜2文字程度)を出すだけでよい場合**に限定して使う。役職本体(Crewmate/Impostor/Neutral)には使わず、`Roles/AddOns/Common/Buff/`・`Roles/AddOns/Common/DeBuff/`配下のAddonクラスのみに適用する規約とする。

- 例: [`Seeing.SubRoleMark`](../../Roles/AddOns/Common/Buff/Seeing.cs)
- `UtilsRoleText.GetSubRoleMarks`([`Modules/Utils/UtilsRoletext.cs`](../../Modules/Utils/UtilsRoletext.cs))内のswitch文で参照され、役職名テキストの一部として合成される(Mark/Suffixとは別の合成経路)
- **関数ではなく値**なので、条件分岐(誰から見えるか等)を持たせたい場合はこの方式を使わず、方式2(`MarkOthers`登録)を使うこと

## どれを使うべきかの判断基準

[`RoleTextWritingGuide.md`](RoleTextWritingGuide.md)の決定木を参照。要約すると:

- 自分の役職クラス内の状態だけで完結する → 方式1(`GetMark`のoverride)
- 役職を問わず全員に一律適用したい・複数箇所から参照される横断的な条件 → 方式2(`MarkOthers`等への登録)
- Addonの固定記号を1つ出すだけ → 方式3(`SubRoleMark`)

## 既存の命名バリエーション一覧(移行対象、新規追加は禁止)

以下は今回の調査で見つかった、命名規則から外れている既存の登録関数。**新規にこのパターンを追加しない**。改修の機会があれば方式2の命名規則(`GetXxxMark`/`GetXxxLowerText`/`GetXxxSuffix`)に寄せる。

- `OtherMark`(`Balancer.cs`、Ghost系役職複数)
- `OtherArrow`(`AmateurTeller.cs`)
- `GetTrashMarkOthers`(`Monika.cs`)
- `AbilityMark`(`DemonicCrusher.cs`)
- `ImpostorMark`(`DemonicTracker.cs`)
- `GetCaughtLowerText`(`Spider.cs`、これは方式2の命名規則自体は満たしているが一覧の網羅性のため記載)
- `GetMarkOthers`が`SuffixOthers`に登録されている(`Sniper.cs`)— パイプライン不一致の例

Ghost系役職が共通して`OtherMark`という命名を使っている点は、Ghost系内では一定の内部一貫性がある。新規Ghost役職もこれに倣うか、統一命名(`GetXxxMark`)に寄せるかは別途要検討とし、本ドキュメントでは強制しない。
