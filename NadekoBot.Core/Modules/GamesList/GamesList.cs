using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NadekoBot.Common.Attributes;
using NadekoBot.Core.Services.GamesList;
using NadekoBot.Extensions;
using NadekoBot.Services.GamesList;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NadekoBot.Modules.GamesList
{
    public class GamesList : NadekoTopLevelModule<GamesListService>
    {
        private readonly DiscordSocketClient _client;
        //private DataGameListGame[] LastGameList;

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
                if (!string.IsNullOrWhiteSpace(list.Header.Image)) embed.WithThumbnailUrl(list.Header.Image);
                if (!string.IsNullOrWhiteSpace(list.Header.Credit)) embed.WithFooter(efb => efb.WithText(list.Header.Credit));

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

                //LastGameList = list.Games;
                if (!string.IsNullOrWhiteSpace(restOfLine) && restOfLine.Trim().ToLowerInvariant() == "all")
                {
                    for (int x = 0; x < (list.Games?.Length ?? 0); x++)
                    {
                        await channel.EmbedAsync(GetGameEmbed(list.Games, x)).ConfigureAwait(false);
                    }
                    return;
                    //return Task.CompletedTask;
                }

                int page = 0;
                int.TryParse(restOfLine?.Trim(), out page);
                page--;
                if (page < 0) page = 0;
                if (page >= (list.Games?.Length ?? 0)) page = list.Games?.Length ?? 0;
                // this appears to pre-generate the pages because I don't have to cache this
                await Context.Channel.SendPaginatedConfirmAsync(_client, page, /*async*/ (curPage) =>
                {
                    return GetGameEmbed(list.Games, curPage);
                }, list.Games?.Length ?? 0, 1, addPaginatedFooter: false);
            }
            //Format.Sanitize
        }

        private EmbedBuilder GetGameEmbed(DataGameListGame[] GamesList, int index = 0)
        {
            var game = GamesList[index];

            EmbedBuilder embed = new EmbedBuilder();

            string playerCountData = string.Empty;
            if (game.CurPlayers.HasValue || game.MaxPlayers.HasValue)
            {
                playerCountData = $" [{(game.CurPlayers.HasValue ? game.CurPlayers.Value.ToString() : "?")}{(game.MaxPlayers.HasValue ? $"/{game.MaxPlayers.Value.ToString()}" : string.Empty)}]";
            }

            string PlayerCountImage = string.Empty;
            if (game.CurPlayers.HasValue && game.MaxPlayers.HasValue)
            {
                float fullnessRatio = 1.0f * game.CurPlayers.Value / game.MaxPlayers.Value;

                if (fullnessRatio >= 1.0f)
                {
                    PlayerCountImage = "🌕 ";
                }
                else if (fullnessRatio >= 0.75f)
                {
                    PlayerCountImage = "🌖 ";
                }
                else if (fullnessRatio >= 0.50f)
                {
                    PlayerCountImage = "🌗 ";
                }
                else if (fullnessRatio >= 0.25f)
                {
                    PlayerCountImage = "🌘 ";
                }
                else if (fullnessRatio >= 0.0f)
                {
                    PlayerCountImage = "🌑 ";
                }
                else
                {
                    PlayerCountImage = "👽 ";
                }
            }

            switch (game.Status)
            {
                case EDataGameListServerGameStatus.Locked: embed.WithColor(new Color(0xbe, 0x19, 0x31)).WithTitle($"⛔ {game.Name}{playerCountData}"); break;
                case EDataGameListServerGameStatus.Passworded: embed.WithColor(new Color(0xff, 0xac, 0x33)).WithTitle($"🔐 {game.Name}{playerCountData}"); break;
                case EDataGameListServerGameStatus.Open: embed.WithOkColor().WithTitle($"{PlayerCountImage}{game.Name}{playerCountData}"); break;
                case EDataGameListServerGameStatus.Unknown: embed.WithColor(new Color(0xff, 0xff, 0x00)).WithTitle($"❓ {game.Name}{playerCountData}"); break;
                case EDataGameListServerGameStatus.NotSet:
                default: embed.WithColor(new Color(0x55, 0x55, 0x55)).WithTitle($"{game.Name}{playerCountData}"); break;
            }

            if (!string.IsNullOrWhiteSpace(game.Image)) embed.WithThumbnailUrl(game.Image);

            string footer = $"[{index + 1}/{GamesList.Length}]";
            if (!string.IsNullOrWhiteSpace(game.Footer)) footer += " " + game.Footer;
            embed.WithFooter(efb => efb.WithText(footer));

            {
                StringBuilder builder = new StringBuilder();

                int lenKey = game.Properties.Max(dr => dr.Item1.Length);

                game.Properties.ForEach(dr =>
                {
                    builder.AppendLine($"{dr.Item1.PadRight(lenKey)} | {dr.Item2}");
                });

                string retVal = Format.Code(builder.ToString(), "css");

                embed.WithDescription(retVal);
            }

            if (game.Players.Count > 0)
            {
                int indexPad = game.Players.Select(player => (player.Index.HasValue ? player.Index.Value.ToString() : string.Empty).Length).Max();
                int classPad = game.Players.Select(player => (player.PlayerClass ?? string.Empty).Length).Max();

                embed.AddField(efb => efb.WithName(game.PlayersHeader ?? "Players")
                                         .WithValue(string.Join("\n",game.Players.Select(player =>
                                         {
                                             string[] Parts = new string[]
                                             {
                                                (indexPad != 0 ? ($"`{(player.Index.HasValue ? player.Index.Value.ToString() : string.Empty).PadLeft(indexPad)}`") : null),
                                                (indexPad != 0 ? ($"`{((player.PlayerClass ?? string.Empty).PadRight(classPad)).Replace("`", "\\`")}`") : null),
                                                (string.IsNullOrWhiteSpace(player.Url) ? Format.Sanitize(player.Name) : $"[{Format.Sanitize(player.Name)}]({player.Url})")
                                             };

                                             return string.Join(" ", Parts);
                                         }))).WithIsInline(false));
            }

            return embed;
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
