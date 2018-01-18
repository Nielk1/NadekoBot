using Discord;
using Discord.Commands;
using NadekoBot.Common.Attributes;
using NadekoBot.Extensions;
using NadekoBot.Services.GamesList;
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
        public async Task Games(string type = null, [Remainder] string restOfLine = null)
        {
            var channel = Context.Channel as ITextChannel;

            if (string.IsNullOrWhiteSpace(type) || !_service.IsValidGameType(type))
            {
                var embed = new EmbedBuilder().WithOkColor()
                                .WithTitle("Games List")
                                .WithDescription($"<:game_icon_battlezone98redux:342134901975547916> `{Prefix}games bz98` | `{Prefix}games bz98r` | `{Prefix}games bzr`\n" +
                                                 $"<:game_icon_battlezone2:342134902587785219> `{Prefix}games bz2`");
                await channel.EmbedAsync(embed).ConfigureAwait(false);
                return;
            }

            await _service.GetGames(channel, type, restOfLine);
        }
    }
}
