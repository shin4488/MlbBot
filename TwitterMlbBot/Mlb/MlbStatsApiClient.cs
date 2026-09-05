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
            SeasonsResponse? parsed = JsonSerializer.Deserialize<SeasonsResponse>(responseBody);
            string? endDate = parsed?.Seasons?.FirstOrDefault()?.RegularSeasonEndDate;
            if (string.IsNullOrEmpty(endDate))
            {
                // 日程が不明のまま投稿可否を判断しない（黙って止まる・止まらないどちらの誤動作も避け、エラー通知に倒す）
                throw new InvalidOperationException($"{year}年のレギュラーシーズン終了日をStats APIレスポンスから取得できませんでした。");
            }
            return new SeasonCalendar(DateOnly.ParseExact(endDate, "yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        // Stats APIレスポンスの形（このクライアント内だけの転送用の型。ドメインにはSeasonCalendarへ変換して渡す）
        private sealed record SeasonsResponse(
            [property: JsonPropertyName("seasons")] List<SeasonResponse>? Seasons);

        private sealed record SeasonResponse(
            [property: JsonPropertyName("regularSeasonEndDate")] string? RegularSeasonEndDate);
    }
}
