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
using NadekoBot.Services.Database.Models;
using NLog;
using System.Diagnostics;

namespace NadekoBot.Modules.Battlezone
{
    [NadekoModule("Battlezone", "!")]
    public partial class Battlezone : DiscordModule
    {
        public static ConcurrentDictionary<string, ConcurrentDictionary<string, BZ2GameProperty>> BZ2GameProperties { get; } = new ConcurrentDictionary<string, ConcurrentDictionary<string, BZ2GameProperty>>();

        private static new readonly Logger _log;

        static Battlezone()
        {
            _log = LogManager.GetCurrentClassLogger();
            var sw = Stopwatch.StartNew();
            using (var uow = DbHandler.UnitOfWork())
            {
                var items = uow.BZ2GameProperties.GetAll();
                BZ2GameProperties = new ConcurrentDictionary<string, ConcurrentDictionary<string, BZ2GameProperty>>();
                items.ForEach(BZ2Property =>
                {
                    lock (BZ2GameProperties)
                    {
                        if (!BZ2GameProperties.ContainsKey(BZ2Property.TermType))
                            BZ2GameProperties[BZ2Property.TermType] = new ConcurrentDictionary<string, BZ2GameProperty>();

                        BZ2GameProperties[BZ2Property.TermType][BZ2Property.Term] = BZ2Property;
                    }
                });
            }
            sw.Stop();
            _log.Debug($"Loaded in {sw.Elapsed.TotalSeconds:F2}s");
        }

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

        [NadekoCommand, Usage, Description, Aliases]
        public async Task AddBZ2ShellMap(string key, [Remainder] string message)
        {
            var channel = Context.Channel as ITextChannel;
            if (string.IsNullOrWhiteSpace(message) || string.IsNullOrWhiteSpace(key))
                return;

            key = key.ToLowerInvariant(); // only for bzn based stuff

            //if ((channel == null && !NadekoBot.Credentials.IsOwner(Context.User)) || (channel != null && !((IGuildUser)Context.User).GuildPermissions.Administrator))
            if (!NadekoBot.Credentials.IsOwner(Context.User))
            {
                try { await Context.Channel.SendErrorAsync("Insufficient permissions. Requires Bot ownership as I don't have custom role logic yet for this command."); } catch { }
                return;
            }

            var sm = new BZ2GameProperty()
            {
                TermType = "shell",
                Term = key,
                Value = message,
            };

            using (var uow = DbHandler.UnitOfWork())
            {
                if (BZ2GameProperties.ContainsKey(sm.TermType) && BZ2GameProperties[sm.TermType].ContainsKey(sm.Term))
                    uow.BZ2GameProperties.Remove(BZ2GameProperties[sm.TermType][sm.Term].Id);

                uow.BZ2GameProperties.Add(sm);

                await uow.CompleteAsync().ConfigureAwait(false);
            }

            {
                lock (BZ2GameProperties)
                {
                    if (!BZ2GameProperties.ContainsKey(sm.TermType))
                        BZ2GameProperties[sm.TermType] = new ConcurrentDictionary<string, BZ2GameProperty>();

                    BZ2GameProperties[sm.TermType][sm.Term] = sm;
                }
            }

            await Context.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                .WithTitle("New BZ2 Shell Map")
                .WithDescription($"#{sm.Id}")
                .WithThumbnailUrl(sm.Value)
                .AddField(efb => efb.WithName("Term").WithValue(sm.Term))
                .AddField(efb => efb.WithName("Value").WithValue(sm.Value))
                ).ConfigureAwait(false);
        }

        public static string GetBZ2GameProperty(string termType, string term)
        {
            if (BZ2GameProperties.ContainsKey(termType) && BZ2GameProperties[termType].ContainsKey(term))
                return BZ2GameProperties[termType][term].Value;
            return null;
        }
    }
}
