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
using NadekoBot.Modules.Battlezone.Commands.BZ98;

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
            using (Context.Channel.EnterTypingState())
            {
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

        [NadekoCommand, Usage, Description, Aliases]
        public async Task GamesBZ98()
        {
            using (Context.Channel.EnterTypingState())
            {
                var gamelist = await BZ98Provider.GetGames();
                if (gamelist == null)
                {
                    await Context.Channel.SendErrorAsync("Failed to get game list.").ConfigureAwait(false);
                    return;
                }

                EmbedBuilder top = gamelist.GetTopEmbed();
                await Context.Channel.EmbedAsync(top).ConfigureAwait(false);

                var games = gamelist.Games;
                int itr = 1;
                int cnt = games.Count();
                var gamesIter = games.Select(dr => dr.GetEmbed(itr++, cnt)).ToList();

                foreach (var game in gamesIter)
                {
                    await Context.Channel.EmbedAsync(game).ConfigureAwait(false);
                }
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task AddBZ2GameProperty(string type, string key, [Remainder] string message)
        {
            var channel = Context.Channel as ITextChannel;
            if (string.IsNullOrWhiteSpace(message) || string.IsNullOrWhiteSpace(key))
                return;

            type = type.ToLowerInvariant();

            if (channel == null && !NadekoBot.Credentials.IsOwner(Context.User))
            {
                try { await Context.Channel.SendErrorAsync("Insufficient permissions. Requires Bot ownership for global access."); } catch { }
                return;
            }

            if (!new[] { "name", "shell", "version", "mod" }.Contains(type))
            {
                await Context.Channel.SendErrorAsync($"Unknown term type '{type}'").ConfigureAwait(false);
                return;
            }

            if (type == "name")
            {
                key = key.ToLowerInvariant(); // only for bzn based stuff
            }

            if (type == "shell")
            {
                key = key.ToLowerInvariant(); // only for bzn based stuff
                Uri uriResult;
                if (!Uri.TryCreate(message, UriKind.Absolute, out uriResult))
                {
                    try { await Context.Channel.SendErrorAsync("Could not parse URL."); } catch { }
                    return;
                }
            }

            var sm = new BZ2GameProperty()
            {
                TermType = type,
                Term = key,
                Value = message,
            };

            await AddBZ2GameProperty(sm).ConfigureAwait(false);

            await Context.Channel.SendConfirmAsync($"Added #{sm.Id} '{sm.TermType}' : '{sm.Term}' = '{sm.Value}'").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task RemoveBZ2GameProperty(string type, string key)
        {
            var channel = Context.Channel as ITextChannel;

            type = type.ToLowerInvariant();

            if (channel == null && !NadekoBot.Credentials.IsOwner(Context.User))
            {
                try { await Context.Channel.SendErrorAsync("Insufficient permissions. Requires Bot ownership for global access."); } catch { }
                return;
            }

            if (!new[] { "name", "shell", "version", "mod" }.Contains(type))
            {
                await Context.Channel.SendErrorAsync($"Unknown term type '{type}'").ConfigureAwait(false);
                return;
            }

            if (type == "name")
            {
                key = key.ToLowerInvariant(); // only for bzn based stuff
            }

            using (var uow = DbHandler.UnitOfWork())
            {
                if (BZ2GameProperties.ContainsKey(type) && BZ2GameProperties[type].ContainsKey(key))
                {

                    BZ2GameProperty deadProp = null;
                    if (BZ2GameProperties[type].TryRemove(key, out deadProp))
                    {
                        uow.BZ2GameProperties.Remove(deadProp.Id);
                        await uow.CompleteAsync().ConfigureAwait(false);
                        await Context.Channel.SendConfirmAsync($"Removed #{deadProp.Id} '{deadProp.TermType}' : '{deadProp.Term}'  = '{deadProp.Value}'").ConfigureAwait(false);
                        return;
                    }
                    await Context.Channel.SendErrorAsync($"Failed to Remove #{deadProp.Id} '{deadProp.TermType}' : '{deadProp.Term}'  = '{deadProp.Value}'").ConfigureAwait(false);
                    return;
                }
            }

            await Context.Channel.SendErrorAsync($"Unknown term type '{type}' with key '{key}'.").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task ListBZ2GameProperties(string type = null, int page = 1)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                string types = string.Join(", ", BZ2GameProperties.Keys.Select(dr => $"\"{dr}\" `{BZ2GameProperties[dr].Count}`"));
                await Context.Channel.SendMessageAsync($"📄 TermTypes: {types}").ConfigureAwait(false);
            }
            else
            {
                if (BZ2GameProperties.ContainsKey(type))
                {
                    var gameProps = BZ2GameProperties[type];

                    if (page < 1 || ((page - 1) > (gameProps.Count / 20)))
                        return;
                    string toSend = "";

                    using (var uow = DbHandler.UnitOfWork())
                    {
                        var i = 1 + 20 * (page - 1);
                        int NumWidth = gameProps.Count.ToString().Length;
                        int TermWidth = gameProps.AsEnumerable().Skip((page - 1) * 20).Take(20).Max(dr => dr.Key.Length);
                        toSend = Format.Code($"📄 Term '{type}' page {page}") + "\n\n" + Format.Code(String.Join("\n", gameProps.AsEnumerable().Skip((page - 1) * 20).Take(20).Select(p => $"{(i++).ToString().PadLeft(NumWidth)} | {p.Value.Term.PadRight(TermWidth)} | {p.Value.Value}")));
                    }

                    await Context.Channel.SendMessageAsync(toSend).ConfigureAwait(false);
                }
                else
                {
                    await Context.Channel.SendErrorAsync($"Unknown term type '{type}'").ConfigureAwait(false);
                }
            }
        }

        private async Task AddBZ2GameProperty(BZ2GameProperty sm)
        {
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
        }

        public static string GetBZ2GameProperty(string termType, string term)
        {
            if (BZ2GameProperties.ContainsKey(termType) && BZ2GameProperties[termType].ContainsKey(term))
                return BZ2GameProperties[termType][term].Value;
            return null;
        }
    }
}
