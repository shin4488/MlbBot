using CoreTweet;
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
        private const string _tweetedErrorMessage = "Sorry. Currently this bot is getting drunk.";
        private static IMapper _mapper;

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
            twitter.CreateTweet(twitterParam);
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
                .ToList();

            // キーデータ（リーグ・地区）ごとにチームデータをTwitter用Paramクラスに詰め替え
            twitterParam.TeamsList = teamsListByLeageDivision
                .Select(teams =>
                {
                    // キーデータ（リーグ・地区）のマッピング
                    ParamByKey paramTeamListData = new ParamByKey
                    {
                        Key = new GroupKey(),
                        // 「#MLB #<1位チーム名>」をタグ付けメッセージとする
                        TagMessage = "#MLB #" + Regex.Replace(teams.First().Name, @"\s", ""),
                        Teams = new List<DetailParam>()
                    };
                    paramTeamListData.Key.League = teams.Key.League;
                    paramTeamListData.Key.Division = teams.Key.Division;

                    // チームデータのマッピング
                    int ranking = 0;
                    paramTeamListData.Teams = teams
                    .Select(team =>
                    {
                        DetailParam param = new DetailParam
                        {
                            Ranking = ++ranking
                        };
                        _mapper.Map(team, param);
                        return param;
                    }).ToList();
                    return paramTeamListData;

                }).ToList();

            return twitterParam;
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
