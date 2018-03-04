using Discord;
using Discord.WebSocket;
using NadekoBot.Extensions;
using System.Threading.Tasks;
using System.Linq;
using NadekoBot.Core.Services;
using NadekoBot.Core.Services.GamesList;
using System.Collections.Generic;
using System;

namespace NadekoBot.Services.GamesList
{
    public class GamesListService : INService
    {
        private readonly DiscordSocketClient _client;
        //private readonly DbService _db;
        //private readonly ILocalization _localization;
        //private readonly NadekoStrings _strings;

        private GameListBZ98Service _bz98;
        private GameListBZ2Service _bz2;
        private GameListBZCCService _bzcc;

        private readonly List<IGameList> _gameLists;
        private readonly Dictionary<string, IGameList> _gameListsKeyed;

        public int GamesListLength { get { return _gameLists?.Count ?? 0; } }

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

            //_bz98 = bz98;
            //_bz2 = bz2;
            //_bzcc = bzcc;

            _gameLists = new List<IGameList>();
        }

        public void AddGameListBZ98Service(GameListBZ98Service x) { _bz98 = x; }
        public void AddGameListBZ2Service(GameListBZ2Service x) { _bz2 = x; }
        public void AddGameListBZCCService(GameListBZCCService x) { _bzcc = x; }

        public void RegisterGameList(IGameList gameList)
        {
            _gameLists.Add(gameList);
            _gameListsKeyed.Add(gameList.Code, gameList);
        }

        public IGameList[] GetGamesList(ulong guildId, int page)
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

            /*switch (type.ToLowerInvariant())
            {
                case "bz2":
                    await GamesBZ2(channel);
                    break;
                case "bzr":
                case "bz98":
                case "bz98r":
                    await GamesBZ98(channel);
                    break;
                case "bzcc":
                    await GamesBZCC(channel);
                    break;
            }*/
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

        public async Task GamesBZCC(ITextChannel channel)
        {
            using (channel.EnterTypingState())
            {
                var gamelist = await _bzcc.GetGames();
                if (gamelist == null)
                {
                    await channel.SendErrorAsync("Failed to get game list.").ConfigureAwait(false);
                    return;
                }

                EmbedBuilder top = gamelist.GetTopEmbed();
                await channel.EmbedAsync(top).ConfigureAwait(false);

                var games = gamelist.GET;//.Where(dr => !dr.IsMarker());
                int itr = 1;
                int cnt = games.Count();
                var gamesIter = games.Select(dr => dr.GetEmbed(itr++, cnt)).ToList();

                foreach (var game in gamesIter)
                {
                    await channel.EmbedAsync(await game).ConfigureAwait(false);
                }
            }
        }

        public async Task GamesBZ2(ITextChannel channel)
        {
            using (channel.EnterTypingState())
            {
                var gamelist = await _bz2.GetGames();
                if (gamelist == null)
                {
                    await channel.SendErrorAsync("Failed to get game list.").ConfigureAwait(false);
                    return;
                }

                EmbedBuilder top = gamelist.GetTopEmbed();
                await channel.EmbedAsync(top).ConfigureAwait(false);

                var games = gamelist.GET.Where(dr => !dr.IsMarker());
                int itr = 1;
                int cnt = games.Count();
                var gamesIter = games.Select(dr => dr.GetEmbed(itr++, cnt)).ToList();

                foreach (var game in gamesIter)
                {
                    await channel.EmbedAsync(await game).ConfigureAwait(false);
                }
            }
        }

        public async Task GamesBZ98(ITextChannel channel)
        {
            using (channel.EnterTypingState())
            {
                var gamelist = await _bz98.GetGames();
                if (gamelist == null)
                {
                    await channel.SendErrorAsync("Failed to get game list.").ConfigureAwait(false);
                    return;
                }

                EmbedBuilder top = gamelist.GetTopEmbed();
                await channel.EmbedAsync(top).ConfigureAwait(false);

                var games = gamelist.Games;
                int itr = 1;
                int cnt = games.Count();
                var gamesIter = games.Select(dr => dr.GetEmbed(itr++, cnt)).ToList();

                foreach (var game in gamesIter)
                {
                    await channel.EmbedAsync(game).ConfigureAwait(false);
                }
            }
        }
    }
}
