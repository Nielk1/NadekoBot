using Discord;
using Discord.WebSocket;
using NadekoBot.Extensions;
using System.Threading.Tasks;
using System.Linq;

namespace NadekoBot.Services.GamesList
{
    public class GamesListService
    {
        private readonly DiscordShardedClient _client;
        private readonly DbService _db;
        private readonly ILocalization _localization;
        private readonly NadekoStrings _strings;

        private readonly GameListBZ98Service _bz98;
        private readonly GameListBZ2Service _bz2;

        public GamesListService(DiscordShardedClient client, DbService db, ILocalization localization, NadekoStrings strings, GameListBZ98Service bz98, GameListBZ2Service bz2)
        {
            _client = client;
            _db = db;
            _localization = localization;
            _strings = strings;

            _bz98 = bz98;
            _bz2 = bz2;
        }

        public bool IsValidGameType(string type)
        {
            switch(type.ToLowerInvariant())
            {
                case "help":
                case "bz2":
                case "bzr":
                case "bz98":
                case "bz98r":
                    return true;
            }

            return false;
        }

        public async Task GetGames(ITextChannel channel, string type, string restOfLine)
        {
            switch (type.ToLowerInvariant())
            {
                case "help":
                    {
                        var embed = new EmbedBuilder().WithOkColor()
                                        .WithTitle("Games List")
                                        .WithDescription("<:game_icon_battlezone98redux:342134901975547916> `.games bz98` | `.games bz98r` | `.games bzr`\n"
                                                       + "<:game_icon_battlezone2:342134902587785219> `.games bz2`");
                        await channel.EmbedAsync(embed).ConfigureAwait(false);
                    }
                    break;
                case "bz2":
                    await GamesBZ2(channel);
                    break;
                case "bzr":
                case "bz98":
                case "bz98r":
                    await GamesBZ98(channel);
                    break;
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
