using Discord;
using Discord.Commands;
using NadekoBot.Attributes;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using NadekoBot.Extensions;
using System.Text.RegularExpressions;
using System.Reflection;
using NadekoBot.Services.Impl;
using System.Net.Http;
using System.Collections.Concurrent;
using System.Threading;
using ImageSharp;
using System.Collections.Generic;
using Newtonsoft.Json;
using Discord.WebSocket;
using NadekoBot.Services;
using NadekoBot.Modules.Battlezone.Commands.BZ2;

namespace NadekoBot.Modules.Battlezone
{
    [NadekoModule("Battlezone", "!")]
    public partial class Battlezone : DiscordModule
    {
        [NadekoCommand, Usage, Description, Aliases]
        public async Task GamesBZ2()
        {
            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);

            var gamelist = await BZ2Provider.GetGames();
            if (gamelist == null)
            {
                await Context.Channel.SendErrorAsync("Failed to get game list.").ConfigureAwait(false);
                return;
            }

            EmbedBuilder top = gamelist.GetTopEmbed();
            await Context.Channel.EmbedAsync(top).ConfigureAwait(false);
                
            var games = gamelist.GET.Where(dr => !dr.IsMarker());
            int itr = 1;
            int cnt = games.Count();
            var gamesIter = games.Select(dr => dr.GetEmbed(itr++, cnt)).ToList();

            foreach (var game in gamesIter)
            {
                await Context.Channel.EmbedAsync(game).ConfigureAwait(false);
            }
        }
    }
}
