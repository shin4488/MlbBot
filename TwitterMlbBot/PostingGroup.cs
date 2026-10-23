namespace TwitterMlbBot;

/// <summary>
/// 1回の実行で投稿する地区のグループ。各値の名前は、文面の対象地区を選ぶためにAPIの地区名（East / Central / West）と同じにする。
/// </summary>
internal enum PostingGroup
{
    East,
    Central,
    West,
}
