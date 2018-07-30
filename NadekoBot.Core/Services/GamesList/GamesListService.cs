using Discord;
using Discord.WebSocket;
using NadekoBot.Extensions;
using System.Threading.Tasks;
using System.Linq;
using NadekoBot.Core.Services;
using NadekoBot.Core.Services.GamesList;
using System.Collections.Generic;
using System;
using Microsoft.Extensions.Logging;
using NLog;
using Microsoft.Extensions.DependencyInjection;

namespace NadekoBot.Services.GamesList
{
    public class GamesListService : INService
    {
        private readonly DiscordSocketClient _client;
        //private readonly DbService _db;
        //private readonly ILocalization _localization;
        //private readonly NadekoStrings _strings;

        private readonly List<IGameList> _gameLists = new List<IGameList>();
        private readonly Dictionary<string, IGameList> _gameListsKeyed = new Dictionary<string, IGameList>();

        public int GamesListLength { get { return _gameLists?.Count ?? 0; } }

        private Logger _log;

        //public GamesListService(DiscordSocketClient client, DbService db, ILocalization localization, NadekoStrings strings, GameListBZ98Service bz98, GameListBZ2Service bz2)
        public GamesListService(
            DiscordSocketClient client//, /*DbService db,*/
                                      //GameListBZ98Service bz98,
                                      //GameListBZ2Service bz2,
                                      //GameListBZCCService bzcc
            )
        {
            _client = client;
            //_db = db;
            //_localization = localization;
            //_strings = strings;

            _log = LogManager.GetCurrentClassLogger();
            _log.Warn("GamesListService");

            //_bz98 = bz98;
            //_bz2 = bz2;
            //_bzcc = bzcc;

            //_gameLists = new List<IGameList>();
            //_gameListsKeyed = new Dictionary<string, IGameList>();
        }

        public void LoadDynamicServices(IServiceProvider services)
        {
            var itype = typeof(IGameList);
            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(p => itype.IsAssignableFrom(p));

            types.ForEach(type =>
            {
                IGameList gameList = (IGameList)services.GetService(type);

                _gameLists.Add(gameList);
                _gameListsKeyed.Add(gameList.Code, gameList);
            });
        }

        public IGameList[] GetGamesList(ulong? guildId, int page)
        {
            return _gameLists//.Where(x => x.GuildId == guildId)
                //.OrderByDescending(x => x.Xp + x.AwardedXp)
                .OrderBy(x => x.Title)
                .Skip(page * 10)
                .Take(10)
                .ToArray();
        }

        public bool IsValidGameType(string type)
        {
            return _gameListsKeyed.ContainsKey(type.ToLowerInvariant());
        }

        public async Task<DataGameList> GetGames(string type)
        {
            IGameList gameList = _gameListsKeyed[type.ToLowerInvariant()];

            return await gameList.GetGamesNew();
        }

        internal static string TimeAgoUtc(DateTime dt)
        {
            TimeSpan span = DateTime.UtcNow - dt;
            if (span.Days > 365)
            {
                int years = (span.Days / 365);
                if (span.Days % 365 != 0)
                    years += 1;
                return String.Format("about {0} {1} ago",
                years, years == 1 ? "year" : "years");
            }
            if (span.Days > 30)
            {
                int months = (span.Days / 30);
                if (span.Days % 31 != 0)
                    months += 1;
                return String.Format("about {0} {1} ago",
                months, months == 1 ? "month" : "months");
            }
            if (span.Days > 0)
                return String.Format("about {0} {1} ago",
                span.Days, span.Days == 1 ? "day" : "days");
            if (span.Hours > 0)
                return String.Format("about {0} {1} ago",
                span.Hours, span.Hours == 1 ? "hour" : "hours");
            if (span.Minutes > 0)
                return String.Format("about {0} {1} ago",
                span.Minutes, span.Minutes == 1 ? "minute" : "minutes");
            if (span.Seconds > 5)
                return String.Format("about {0} seconds ago", span.Seconds);
            if (span.Seconds <= 5)
                return "just now";
            return string.Empty;
        }
    }
}
