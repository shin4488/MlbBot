using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace TwitterMlbBot.Mlb
{
    /// <summary>
    /// MLB公式のStats API（statsapi.mlb.com）からシーズン日程を取得するクライアント。
    /// 認証不要・無料の公開APIのため、キー等の設定は不要
    /// </summary>
    internal class MlbStatsApiClient : ISeasonCalendarProvider
    {
        private readonly HttpClient client;
        // sportId=1 はMLBの指定（Stats APIはマイナーリーグ等も扱うため必須）
        private static readonly string endpointFormat = "https://statsapi.mlb.com/api/v1/seasons/{0}?sportId=1";
        private readonly ILogger<MlbStatsApiClient> logger;

        public MlbStatsApiClient(HttpClient client, ILogger<MlbStatsApiClient> logger)
        {
            this.client = client;
            this.logger = logger;
        }

        public async Task<SeasonCalendar> GetSeasonCalendarAsync(int year)
        {
            string endpoint = string.Format(CultureInfo.InvariantCulture, endpointFormat, year);
            using HttpResponseMessage response = await client.GetAsync(endpoint).ConfigureAwait(false);
            string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                // クライアントは取得失敗だけを伝える。投稿続行や通知の判断は実行側の方針に委ねる
                throw new MlbApiException($"{year}年のシーズン日程", response.StatusCode, responseBody);
            }

            SeasonCalendar calendar = ParseSeasonCalendar(responseBody, year);
            logger.LogInformation(
                "Season calendar fetched: {Year} regular season ends {EndDate}", year, calendar.RegularSeasonEndDate);
            return calendar;
        }

        /// <summary>
        /// Stats APIのレスポンスからシーズン日程を取り出す
        /// </summary>
        internal static SeasonCalendar ParseSeasonCalendar(string responseBody, int year)
        {
            try
            {
                SeasonsResponse parsed = JsonSerializer.Deserialize<SeasonsResponse>(responseBody)
                    ?? throw new InvalidOperationException($"MLB公式の日程情報から{year}年のシーズンを特定できないため、シーズン終了を判断できません。");

                SeasonResponse season = parsed.GetSeason(year);
                return season.ToSeasonCalendar(year);
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException($"MLB公式の{year}年の日程情報を読み取れないため、シーズン終了を判断できません。", exception);
            }
        }

        // Stats APIレスポンスの形（このクライアント内だけの転送用の型。ドメインにはSeasonCalendarへ変換して渡す）
        private sealed record SeasonsResponse(
            [property: JsonPropertyName("seasons")] List<SeasonResponse?>? Seasons)
        {
            public SeasonResponse GetSeason(int year)
            {
                // 終了日の年だけでは、どのシーズンの日程かは確認できない。
                // 応答順に依存せずシーズンIDで選び、複数候補があれば推測せず取得失敗にする。
                string seasonId = year.ToString(CultureInfo.InvariantCulture);
                SeasonResponse[] matchingSeasons = Seasons?.OfType<SeasonResponse>()
                    .Where(season => season.SeasonId == seasonId).ToArray() ?? [];
                bool canIdentifySeason = matchingSeasons.Length == 1;
                if (!canIdentifySeason)
                {
                    throw new InvalidOperationException($"MLB公式の日程情報から{year}年のシーズンを特定できないため、シーズン終了を判断できません。");
                }

                return matchingSeasons[0];
            }
        }

        private sealed record SeasonResponse(
            [property: JsonPropertyName("seasonId")] string? SeasonId,
            [property: JsonPropertyName("regularSeasonEndDate")] DateOnly? RegularSeasonEndDate)
        {
            public SeasonCalendar ToSeasonCalendar(int year)
            {
                DateOnly endDate = RegularSeasonEndDate
                    ?? throw new InvalidOperationException($"MLB公式の日程情報に{year}年のレギュラーシーズン終了日が記載されていません。");
                bool isRequestedSeason = endDate.Year == year;
                // 別の年の日程による誤った投稿停止を防ぐため、この応答は取得失敗として扱う。
                // BotRunnerの日程取得失敗時の方針に従い、シーズン中でありうる時期は投稿を続ける。
                if (!isRequestedSeason)
                {
                    throw new InvalidOperationException($"対象は{year}年ですが、取得した終了日が{endDate.Year}年になっているため、シーズン終了を判断できません。");
                }

                return new SeasonCalendar(endDate);
            }
        }
    }
}
