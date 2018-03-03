using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NadekoBot.Common.Attributes;
using NadekoBot.Extensions;
using NadekoBot.Services.GamesList;
using System.Linq;
using System.Threading.Tasks;

namespace NadekoBot.Modules.GamesList
{
    public class GamesList : NadekoTopLevelModule<GamesListService>
    {
        private readonly DiscordSocketClient _client;

        public GamesList(DiscordSocketClient client)
        {
            _client = client;
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Games(string type = null, [Remainder] string restOfLine = null)
        {
            var channel = Context.Channel as ITextChannel;

            /*if (string.IsNullOrWhiteSpace(type) || !_service.IsValidGameType(type))
            {
                var embed = new EmbedBuilder().WithOkColor()
                                .WithTitle("Games List")
                                .WithDescription($"<:game_icon_battlezone98redux:342134901975547916> `{Prefix}games bz98` | `{Prefix}games bz98r` | `{Prefix}games bzr`\n" +
                                                 $"<:game_icon_battlezone2:342134902587785219> `{Prefix}games bz2`");
                await channel.EmbedAsync(embed).ConfigureAwait(false);
                return;
            }*/

            await _service.GetGames(Prefix, channel, type, restOfLine);
        }

        [NadekoCommand(memberName: "GamesList"), Usage, Description, Aliases]
        public Task GamesListFunc(int page = 1)
        {
            if (--page < 0 || page > 100)
                return Task.CompletedTask;

            return Context.Channel.SendPaginatedConfirmAsync(_client, page, /*async*/ (curPage) =>
            {
                var games = _service.GetGamesList(Context.Guild.Id, curPage);

                var embed = new EmbedBuilder()
                    .WithTitle(GetText("gameslist"))
                    .WithOkColor();

                //if (!games.Any())
                if (games.Length == 0)
                    return embed.WithDescription("-");
                else
                {
                    for (int i = 0; i < games.Length; i++)
                    {
                        embed.AddField(
                            $"{games[i].Emoji} {games[i].Title}",
                            $"`{Prefix}games {games[i].Code}`");
                    }
                    return embed;
                }
            }, _service.GamesListLength, 10, addPaginatedFooter: false);
        }
    }
}
