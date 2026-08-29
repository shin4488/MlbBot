namespace TwitterMlbBot.Composing
{
    /// <summary>
    /// ツイート文面（値オブジェクト）。Xの文字数上限に関する知識をここに集約する
    /// </summary>
    /// <param name="Text">文面テキスト</param>
    internal record TweetContent(string Text)
    {
        /// <summary>
        /// Xの文字数上限
        /// （実際のXの判定はURLやCJK文字に重み付けした独自カウントのため、これは英数字ベースの近似値）
        /// </summary>
        public const int CharacterLimit = 280;

        /// <summary>
        /// 文面の文字数
        /// </summary>
        public int CharacterCount => Text.Length;

        /// <summary>
        /// 文字数上限を超えている可能性があるか
        /// </summary>
        public bool ExceedsCharacterLimit => CharacterCount > CharacterLimit;
    }
}
