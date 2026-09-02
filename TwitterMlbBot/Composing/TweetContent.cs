using System.Text;

namespace TwitterMlbBot.Composing
{
    /// <summary>
    /// ツイート文面（値オブジェクト）。Xの文字数上限に関する知識をここに集約する
    /// </summary>
    /// <param name="Text">文面テキスト</param>
    internal record TweetContent(string Text)
    {
        /// <summary>
        /// Xの文字数上限（重み付きカウント基準）
        /// </summary>
        public const int CharacterLimit = 280;

        /// <summary>
        /// Xの重み付きルールで数えた文面の文字数。
        /// Xは単純な文字数ではなく、ラテン文字等（重み1）と、それ以外のCJK文字・絵文字等（重み2）を区別して
        /// 上限を判定する（twitter-textの文字数設定に準拠）。string.Lengthで数えると絵文字や日本語を
        /// 含む文面で実際より少なく見積もり、X API側で拒否（403）されて初めて超過に気付くことになる
        /// </summary>
        public int CharacterCount => Text.EnumerateRunes().Sum(Weight);

        /// <summary>
        /// 文字数上限を超えている可能性があるか
        /// </summary>
        public bool ExceedsCharacterLimit => CharacterCount > CharacterLimit;

        /// <summary>
        /// 1文字（コードポイント）あたりの重み。
        /// URLは一律23字として数えられるが、リンク入り投稿は料金が高い（$0.20/件）ため文面に含めない前提で扱わない。
        /// ZWJ結合絵文字や異体字セレクタ付き絵文字は1絵文字=2として数えるのが正確だが、ここでは構成コードポイントを
        /// それぞれ数える（実際より多く見積もる安全側の近似。誤って少なく見積もることはない）
        /// </summary>
        private static int Weight(Rune rune)
        {
            int codePoint = rune.Value;
            bool isSingleWeight =
                codePoint <= 4351                                // 基本ラテン〜グルジア文字（ラテン・ギリシャ・キリル・アラビア文字等）
                || (codePoint >= 8192 && codePoint <= 8205)      // 各種スペース・ゼロ幅文字
                || (codePoint >= 8208 && codePoint <= 8223)      // ダッシュ・引用符
                || (codePoint >= 8242 && codePoint <= 8247);     // プライム記号
            return isSingleWeight ? 1 : 2;
        }
    }
}
