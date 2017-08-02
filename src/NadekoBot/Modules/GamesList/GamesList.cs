using Discord;
using Discord.Commands;
using NadekoBot.Attributes;
using NadekoBot.Extensions;
using NadekoBot.Services.GamesList;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NadekoBot.Modules.GamesList
{
    public class GamesList : NadekoTopLevelModule
    {
        private readonly GamesListService _service;

        public GamesList(GamesListService service)
        {
            _service = service;
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Games(string type, [Remainder] string restOfLine = null)
        {
            if(!_service.IsValidGameType(type))
            {
                await ReplyErrorLocalized("invalid_gametype").ConfigureAwait(false);
                return;
            }

            var channel = Context.Channel as ITextChannel;

            await _service.GetGames(channel, type, restOfLine);
        }
    }
}
