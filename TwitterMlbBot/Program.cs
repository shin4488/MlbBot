using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TwitterMlbBot.Mlb;
using TwitterMlbBot.Twitter;
using AutoMapper;
using System.Text.RegularExpressions;

namespace TwitterMlbBot
{
    public class Program
    {
        private static IMapper _mapper;

        /// <summary>
        /// MLB公式チームハッシュタグマップ（チーム名と公式タグが異なるもののみ定義）
        /// 毎シーズン変更の可能性があるため、ここで一元管理する
        /// </summary>
        private static readonly Dictionary<string, string> OfficialHashtagMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Diamondbacks", "Dbacks" },
                { "Braves",       "BravesCountry" },
                { "Orioles",      "Birdland" },
                { "Red Sox",      "DirtyWater" },
                { "Reds",         "ATOBTTR" },
                { "Guardians",    "GuardsBall" },
                { "Tigers",       "DNMW" },
                { "Phillies",     "RingTheBell" },
                { "Royals",       "FountainsUp" },
                { "Angels",       "RepTheHalo" },
                { "Marlins",      "FightinFish" },
                { "Brewers",      "ThisIsMyCrew" },
                { "Twins",        "NoPlaceLikeHERE" },
                { "Mets",         "LGM" },
                { "Yankees",      "RepBX" },
                { "Pirates",      "LetsGoBucs" },
                { "Padres",       "ForTheFaithful" },
                { "Mariners",     "TridentsUp" },
                { "Giants",       "SFGiants" },
                { "Cardinals",    "STLCards" },
                { "Rays",         "RaysUp" },
                { "Rangers",      "AllForTX" },
                { "Blue Jays",    "BlueJays50" },
                { "Nationals",    "Natitude" },
            };

        /// <summary>
        /// エントリーポイント
        /// WebAPI接続の関係で非同期エントリーポイントとしている
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static async Task Main(string[] args)
        {
            CreateMapping();

            // コマンドライン引数で西暦年が入力されたらその年を使用、入力されなかったら現在の西暦年を使用
            int year = args?.Length > 0 && int.TryParse(args[0], out int inputYear)
                ? inputYear
                : DateTime.Now.Year
                ;
            Mlb.Param mlbParam = new Mlb.Param() { Year = year };
            MlbService mlb = new MlbService();
            Result mlbResult = await mlb.GetStandingData(mlbParam);

            // Mlbクラスの戻り値用クラスからTwitterクラスの引数用クラスへMapping
            Twitter.Param twitterParam = MapToTwitterParam(mlbResult.ResultTeamList);

            TwitterService twitter = new TwitterService();
            await twitter.CreateTweet(twitterParam);
        }

        /// <summary>
        /// 「MLBのWebAPIレスポンスのチームデータ」を「Twitter用のグループ化したチームデータ」に変換
        /// </summary>
        /// <param name="resultTeamList">WebAPIレスポンスのチームデータ（非グループ化）</param>
        /// <returns></returns>
        private static Twitter.Param MapToTwitterParam(List<DetailResult> resultTeamList)
        {
            Twitter.Param twitterParam = new Twitter.Param();

            // WebAPIレスポンスの順位データ（JSON）をリーグごと・地区ごとのチームリストに変換
            var teamsListByLeageDivision = resultTeamList
                .GroupBy(team => new { team.League, team.Division })
                // All Starsというチームが、「リーグ: AL, 地区: AL」の「リーグ: NL, 地区: NL」形式で1チームだけ入ってるので、それは除外する
                .Where(g => g.Skip(1).Any())
                .ToList();

            // キーデータ（リーグ・地区）ごとにチームデータをTwitter用Paramクラスに詰め替え
            twitterParam.TeamsList = teamsListByLeageDivision
                .Select(teams =>
                {
                    // キーデータ（リーグ・地区）のマッピング
                    ParamByKey paramTeamListData = new ParamByKey
                    {
                        Key = new GroupKey(),
                        Teams = new List<DetailParam>()
                    };
                    paramTeamListData.Key.League = teams.Key.League;
                    paramTeamListData.Key.Division = teams.Key.Division;

                    // チームデータのマッピング
                    int ranking = 0;
                    List<DetailParam> teamList = teams
                    .Select(team =>
                    {
                        DetailParam param = new DetailParam
                        {
                            Ranking = ++ranking
                        };
                        _mapper.Map(team, param);
                        return param;
                    }).ToList();
                    paramTeamListData.Teams = teamList;
                    // 「#MLB #<1位チーム名> #<2位チーム名>」をタグ付けメッセージとする
                    paramTeamListData.TagMessage = "#MLB" +
                        " " + GetTeamHashtags(paramTeamListData.Teams.First().Name) +
                        " " + GetTeamHashtags(paramTeamListData.Teams[1].Name);
                    return paramTeamListData;

                }).ToList();

            return twitterParam;
        }

        /// <summary>
        /// MLBチーム名から公式Twitterハッシュタグ文字列を生成する。
        /// 公式タグがチーム名と異なる場合は、公式タグと元チーム名の両方を返す。
        /// </summary>
        /// <param name="teamName">チーム名（例: "Diamondbacks"）</param>
        /// <returns>ハッシュタグ文字列（例: "#Dbacks #Diamondbacks"）</returns>
        private static string GetTeamHashtags(string teamName)
        {
            string nameNoSpace = Regex.Replace(teamName, @"\s", "");
            return OfficialHashtagMap.TryGetValue(teamName, out string officialTag)
                // 公式タグ + 元チーム名タグの両方を付ける
                ? $"#{officialTag} #{nameNoSpace}"
                // チーム名と公式タグが同じ場合はそのまま使用
                : $"#{nameNoSpace}";
        }

        /// <summary>
        /// マッピング設定
        /// </summary>
        private static void CreateMapping()
        {
            // AutoMapperでのマッピング元・マッピング先クラスの結び付け
            var config = new MapperConfiguration(configuration =>
            {
                configuration.CreateMap<DetailResult, DetailParam>();
            });
            _mapper = config.CreateMapper();
        }
    }
}
