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
    //[NadekoModule("Battlezone", "!")]
    public partial class Battlezone : NadekoTopLevelModule
    {
        private readonly IBotCredentials _creds;
        private readonly DbService _db;
        private readonly DiscordShardedClient _client;

        private readonly BZ98Service _bz98;
        private readonly BZ2Service _bz2;



        public Battlezone(IBotCredentials creds, DbService db, DiscordShardedClient client, BZ98Service bz98, BZ2Service bz2)
        {
            _creds = creds;
            _db = db;
            _client = client;

            _bz98 = bz98;
            _bz2 = bz2;
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task GamesBZ2()
        {
            using (Context.Channel.EnterTypingState())
            {
                var gamelist = await _bz2.GetGames();
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
                var gamelist = await _bz98.GetGames();
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

            if (channel == null && !_creds.IsOwner(Context.User))
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

            if (channel == null && !_creds.IsOwner(Context.User))
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

            using (var uow = _db.UnitOfWork)
            {
                if (_bz2.BZ2GameProperties.ContainsKey(type) && _bz2.BZ2GameProperties[type].ContainsKey(key))
                {

                    BZ2GameProperty deadProp = null;
                    if (_bz2.BZ2GameProperties[type].TryRemove(key, out deadProp))
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
                string types = string.Join(", ", _bz2.BZ2GameProperties.Keys.Select(dr => $"\"{dr}\" `{_bz2.BZ2GameProperties[dr].Count}`"));
                await Context.Channel.SendMessageAsync($"📄 TermTypes: {types}").ConfigureAwait(false);
            }
            else
            {
                if (_bz2.BZ2GameProperties.ContainsKey(type))
                {
                    var gameProps = _bz2.BZ2GameProperties[type];

                    if (page < 1 || ((page - 1) > (gameProps.Count / 20)))
                        return;
                    string toSend = "";

                    using (var uow = _db.UnitOfWork)
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
            using (var uow = _db.UnitOfWork)
            {
                if (_bz2.BZ2GameProperties.ContainsKey(sm.TermType) && _bz2.BZ2GameProperties[sm.TermType].ContainsKey(sm.Term))
                    uow.BZ2GameProperties.Remove(_bz2.BZ2GameProperties[sm.TermType][sm.Term].Id);

                uow.BZ2GameProperties.Add(sm);

                await uow.CompleteAsync().ConfigureAwait(false);
            }

            {
                lock (_bz2.BZ2GameProperties)
                {
                    if (!_bz2.BZ2GameProperties.ContainsKey(sm.TermType))
                        _bz2.BZ2GameProperties[sm.TermType] = new ConcurrentDictionary<string, BZ2GameProperty>();

                    _bz2.BZ2GameProperties[sm.TermType][sm.Term] = sm;
                }
            }
        }
    }
}
