using Discord;
using Discord.WebSocket;
using NadekoBot.Extensions;
using System.Threading.Tasks;
using System.Linq;

namespace NadekoBot.Services.GamesList
{
    public class GamesListService
    {
        private readonly DiscordSocketClient _client;
        //private readonly DbService _db;
        //private readonly ILocalization _localization;
        //private readonly NadekoStrings _strings;

        private readonly GameListBZ98Service _bz98;
        private readonly GameListBZ2Service _bz2;
        private readonly GameListBZCCService _bzcc;

        //public GamesListService(DiscordSocketClient client, DbService db, ILocalization localization, NadekoStrings strings, GameListBZ98Service bz98, GameListBZ2Service bz2)
        public GamesListService(DiscordSocketClient client, /*DbService db,*/
            GameListBZ98Service bz98,
            GameListBZ2Service bz2,
            GameListBZCCService bzcc)
        {
            _client = client;
            //_db = db;
            //_localization = localization;
            //_strings = strings;

            _bz98 = bz98;
            _bz2 = bz2;
            _bzcc = bzcc;
        }

        public bool IsValidGameType(string type)
        {
            switch(type.ToLowerInvariant())
            {
                case "bz2":
                case "bzr":
                case "bz98":
                case "bz98r":
                case "bzcc":
                    return true;
            }

            return false;
        }

        public async Task GetGames(ITextChannel channel, string type, string restOfLine)
        {
            switch (type.ToLowerInvariant())
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
            }
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
