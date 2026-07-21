# 名前表示マーク執筆ガイド(決定木・コード例)

「役職やAddonで、こういう表示を実装したい」と思ったときに、どのAPIをどう書けばいいかのクックブック。
どのAPIが何のためにあるかの全体方針は[`RoleTextApiStandard.md`](RoleTextApiStandard.md)を参照。

## 決定木

```
そのマーク/テキストは…
│
├─ 自分の役職クラスが持つ内部状態(フィールド)だけを見て決まる？
│   └─ Yes → GetMark / GetLowerText / GetSuffix を override する(パターンA)
│
├─ 役職の種類を問わず、全seer×全targetに一律適用したい条件？
│  (例: 「投票結果を見た」「Connectingである」のような、役職に紐づかない横断的な状態)
│   └─ Yes → CustomRoleManager.MarkOthers / LowerOthers / SuffixOthers に登録する(パターンB)
│
└─ Addon/Buff/DeBuffで、常に同じ固定の短い記号を1つ出すだけでよい？
    └─ Yes → SubRoleMark 静的プロパティを定義する(パターンC)
```

迷ったら「条件分岐が要るか」で判断する。条件分岐が要らない固定の記号ならパターンC、要るならパターンAかB。

## パターンA: `GetMark`のoverride

自分の内部状態(フィールド)を見て、seer/seenごとにマークを組み立てる。

```csharp
// Roles/Neutral/Arsonist.cs を簡略化した例
public override string GetMark(PlayerControl seer, PlayerControl seen, bool isForMeeting = false)
{
    seen ??= seer;
    if (IsDousedPlayer(seen.PlayerId)) //既に塗り終えている
        return Utils.ColorString(RoleInfo.RoleColor, "▲");
    if (!isForMeeting && TargetInfo?.TargetId == seen.PlayerId) //塗っている最中
        return Utils.ColorString(RoleInfo.RoleColor, "△");
    return "";
}
```

**注意**: `seer`側の役職クラスのメソッドが呼ばれる(target側ではない)。「この役職を持つプレイヤーから見て、対象がどう見えるか」を書く。

条件が複数の要素にまたがって複雑になる場合は、`GetMark`自体は薄く保ち、内部で組み立て専用の`private static`メソッドに分割する(1つの`return`に長い文字列補間を詰め込まない)。

```csharp
// Roles/Crewmate/AmateurTeller.cs の例(★マークと矢印の組み立てを分離)
public static string OtherArrow(PlayerControl seer, PlayerControl seen, bool isForMeeting = false)
{
    ...
    return BuildPendingTargetMark(GetArrowText(seer, tell));
}
private static string BuildPendingTargetMark(string arrowText) => $"<color=#6b3ec3>★{arrowText}</color>";
private static string GetArrowText(PlayerControl seer, AmateurTeller tell) =>
    canSeeArrowAsTarget ? $"\n{TargetArrow.GetArrows(seer, tell.Player.PlayerId)}" : "";
```

色タグで複数の要素(マーク本体+矢印)を1つに囲みたい場合、要素ごとに`ColorString`で別々に囲むと見た目の色が分かれてしまう。**同じ色で見せたい要素は、組み立て用メソッドの戻り値を先に文字列として合成してから、まとめて1つの色タグで包む**こと。

## パターンB: 役職を問わず横断的に効くMark/LowerText/Suffix(第一候補: virtual override)

役職を問わず、全seer×全targetの組み合わせで評価してよい条件を追加したいときは、`RoleBase.GetBroadcastMark`/`GetBroadcastLowerText`/`GetBroadcastSuffix`をoverrideする(新規実装ではこちらを使う。`CustomRoleManager.AllActiveRoles`を通じて`.Add`登録なしで自動的にブロードキャストされる)。

```csharp
public override string GetBroadcastMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
{
    seen ??= seer;
    // thisインスタンス自身の状態だけを見ればよい(他インスタンスを探す必要はない)
    if (!何らかの条件) return "";
    return Utils.ColorString(色, "記号");
}
```

`.Add`による登録は不要。ブロードキャストされる時点で「このインスタンス自身」に閉じた判定で済む(`AmateurTeller.GetBroadcastMark`を参照、`RoleTextApiStandard.md`の実例)。

**`addRole`委譲パターンの役職(`MagicalGirl`/`Assassin`/`JackalWolf`/`Villain`)を新設・改修する場合の注意**: これらは変身先/憑依先の役職を`addRole`という別インスタンスとして持ち、`AllActiveRoles`にはラッパー自身しか登録されない。`GetBroadcastMark`等をoverrideする役職が、これらの変身先候補になり得るなら、ラッパー側にも以下の中継コードが入っていることを確認する:

```csharp
public override string GetBroadcastMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    => addRole?.GetBroadcastMark(seer, seen, isForMeeting) ?? "";
```

### 代替: `CustomRoleManager.MarkOthers`等への`.Add`登録(既存コードとの整合が必要な場合)

既存の44箇所はこの方式のままになっている。新規に真似しない。登録用関数の命名は`GetXxxMark`/`GetXxxLowerText`/`GetXxxSuffix`のように、登録先パイプラインが名前から分かるようにする(詳細は`RoleTextApiStandard.md`の方式2-B)。

**必ず守ること(この方式を使う場合)**:
- 登録関数名は登録先(`MarkOthers`/`LowerOthers`/`SuffixOthers`)に対応させる。`SuffixOthers`に`GetXxxMark`という名前の関数を登録するような、パイプラインと命名がズレる書き方はしない
- この関数は**全seer×全targetの組み合わせで毎回呼ばれる**ため、重い処理(ループ内でのAPI呼び出しやアロケーションの多い処理)を書かない
- 登録している行には、`RoleFileStandard.md`の指針通り「いつ呼ばれるか」を短いコメントで添える

## パターンC: `SubRoleMark`静的プロパティ

Addon/Buff/DeBuffクラスに、固定の短い記号を持たせるだけの場合。

```csharp
// Roles/AddOns/Common/Buff/Seeing.cs の例
public static string SubRoleMark = Utils.ColorString(RoleColor, "☯");
```

このプロパティ自体は条件分岐を持てない(値なので)。表示するかどうかの判定は`UtilsRoleText.GetSubRoleMarks`側のswitch文が行う。**「常に付ける」以外の条件付き表示がしたくなったら、この方式ではなくパターンB(`MarkOthers`登録)に切り替える**こと。

## 未解決の検討事項

- Ghost系役職(`AsistingAngel`/`Ghostbuttoner`/`GhostReseter`等)は共通して`OtherMark`という命名を使っている。Ghost系内では一貫しているため、新規Ghost役職もこれに倣うか、統一命名(`GetXxxMark`)に寄せるかは未決定。着手する際はGhost系役職をまとめて確認してから判断する
