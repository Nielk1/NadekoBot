using Discord;
using Discord.WebSocket;
using NadekoBot.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NadekoBot.Services.GamesList
{
    public class GamesListService
    {
        private readonly DiscordShardedClient _client;
        private readonly DbService _db;
        private readonly ILocalization _localization;
        private readonly NadekoStrings _strings;
        //private readonly Timer checkWarTimer;

        public ConcurrentDictionary<string, List<ITextChannel>> ActiveRequests { get; set; }

        public GamesListService(DiscordShardedClient client, DbService db, ILocalization localization, NadekoStrings strings)
        {
            _client = client;
            _db = db;
            _localization = localization;
            _strings = strings;

            using (var uow = _db.UnitOfWork)
            {
                ActiveRequests = new ConcurrentDictionary<string, List<ITextChannel>>();
            }
        }

        public bool IsValidGameType(string type)
        {
            switch(type.ToLowerInvariant())
            {
                case "help":
                case "bz2":
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
                                        .WithDescription( "<:game_icon_battlezone98redux:342134901975547916> `.games bz98`|`.games bz98r`\n"
                                                        + "<:game_icon_battlezone2:342134902587785219> `.games bz2`");
                        await channel.EmbedAsync(embed).ConfigureAwait(false);
                    }
                    break;
                case "bz2":
                    break;
                case "bz98":
                case "bz98r":
                    break;
            }
        }
    }
}
