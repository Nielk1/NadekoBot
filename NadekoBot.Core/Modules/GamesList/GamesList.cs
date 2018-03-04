using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NadekoBot.Common.Attributes;
using NadekoBot.Core.Services.GamesList;
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

            if (type == null || !_service.IsValidGameType(type))
            {
                var embed = new EmbedBuilder().WithErrorColor()
                .WithTitle(GetText("gameslist"))
                .WithDescription(GetText("unknowngame", Prefix));
                await channel.EmbedAsync(embed).ConfigureAwait(false);
                return;
            }

            using (channel.EnterTypingState())
            {
                DataGameList list = await _service.GetGames(type);

                EmbedBuilder embed = new EmbedBuilder()
                    .WithColor(new Color(255, 255, 255))
                    .WithTitle($"{list.GameTitle} {GetText("gamelist")}")
                    .WithDescription($"{list.Header.Description}\n`{list.Games?.Length ?? 0} Game(s)`");
                if (!string.IsNullOrWhiteSpace(list.Header.Image)) embed = embed.WithThumbnailUrl(list.Header.Image);
                if (!string.IsNullOrWhiteSpace(list.Header.Credit)) embed = embed.WithFooter(efb => efb.WithText(list.Header.Credit));

                foreach (DataGameListServerStatus status in list.Header.ServerStatus)
                {
                    string StatusText = string.Empty;
                    switch (status.Status)
                    {
                        case EDataGameListServerStatus.Online: StatusText += "✅ Online"; break;
                        case EDataGameListServerStatus.Offline: StatusText += "❌ Offline"; break;
                        case EDataGameListServerStatus.NoGames: StatusText += "⚠ No Games"; break;
                        case EDataGameListServerStatus.Unknown: StatusText += "❓ Unknown"; break;
                    }
                    if (status.Updated.HasValue) StatusText += $"\nUpdated {GamesListService.TimeAgoUtc(status.Updated.Value)}";

                    if (string.IsNullOrWhiteSpace(StatusText)) StatusText = "-";

                    embed.AddField(efb => efb.WithName(status.Name).WithValue(StatusText).WithIsInline(true));
                }
                await channel.EmbedAsync(embed).ConfigureAwait(false);

                int counter = 0;
                if (list.Games != null)
                    foreach (var game in list.Games)
                    {
                        counter++;
                        EmbedBuilder localembed = new EmbedBuilder()
                            .WithColor(new Color(255, 255, 255))
                            .WithTitle($"{list.GameTitle}")
                            .WithDescription($"-");
                        if (!string.IsNullOrWhiteSpace(game.Image)) localembed = localembed.WithThumbnailUrl(game.Image);
                        localembed = localembed.WithFooter(efb => efb.WithText($"{counter}/{list.Games.Length}"));

                        await channel.EmbedAsync(localembed).ConfigureAwait(false);
                    }

                return;
            }

            //Format.Sanitize
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
