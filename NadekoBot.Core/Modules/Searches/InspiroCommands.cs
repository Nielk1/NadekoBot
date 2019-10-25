using Discord;
using Discord.Commands;
using NadekoBot.Extensions;
using Newtonsoft.Json;
using System.Net.Http;
using System.Threading.Tasks;
using NadekoBot.Common;
using NadekoBot.Common.Attributes;

namespace NadekoBot.Modules.Searches
{
    public partial class Searches
    {
        [Group]
        public class InspiroCommands : NadekoSubmodule
        {
            private readonly IHttpClientFactory _httpFactory;

            public InspiroCommands(IHttpClientFactory factory)
            {
                _httpFactory = factory;
            }
            
            [NadekoCommand, Usage, Description, Aliases]
            [Priority(0)]
            public async Task Inspiro()
            {
                using (ctx.Channel.EnterTypingState())
                {
                    using (var http = _httpFactory.CreateClient())
                    {
                        var response = await http.GetStringAsync("http://inspirobot.me/api?generate=true").ConfigureAwait(false);
                        if (response == null || string.IsNullOrWhiteSpace(response))
                            return;

                        await ctx.Channel.EmbedAsync(new EmbedBuilder()
                            .WithOkColor()
                            .WithImageUrl(response)
                            .WithDescription(response.Replace(@"https://generated.inspirobot.me/", @"http://inspirobot.me/share?iuid=")));
                    }
                }
            }
        }
    }
}
