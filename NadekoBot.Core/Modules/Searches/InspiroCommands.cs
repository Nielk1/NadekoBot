using Discord;
using Discord.Commands;
using NadekoBot.Extensions;
using Newtonsoft.Json;
using System.Net.Http;
using System.Threading.Tasks;
using NadekoBot.Common;
using NadekoBot.Common.Attributes;
using System.Text.RegularExpressions;

namespace NadekoBot.Modules.Searches
{
    public partial class Searches
    {
        [Group]
        public class InspiroCommands : NadekoSubmodule
        {
            private Regex InspiroRegex = new Regex(@"((((\??iuid)?=)?(?'path'a))?\/)?(?'id'[a-zA-Z0-9]{10})(\.(?'ext'jpg)?)?$");
            private readonly IHttpClientFactory _httpFactory;

            public InspiroCommands(IHttpClientFactory factory)
            {
                _httpFactory = factory;
            }
            
            [NadekoCommand, Usage, Description, Aliases]
            [Priority(0)]
            public async Task Inspiro([Leftover] string query = null)
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    query = null;
                }
                else
                {
                    Match m = InspiroRegex.Match(query);
                    if (m.Groups["id"].Length > 0)
                    {
                        string path = m.Groups["path"]?.Value;
                        if (string.IsNullOrWhiteSpace(path)) path = "a";
                        string ext = m.Groups["ext"]?.Value;
                        if (string.IsNullOrWhiteSpace(ext)) ext = "jpg";
                        query = @"https://generated.inspirobot.me/" + path + "/" + m.Groups["id"].Value + "." + ext;
                    }
                    else
                    {
                        await ReplyErrorLocalizedAsync("inspiro_query_failed_parse").ConfigureAwait(false);
                        return;
                    }
                }

                using (ctx.Channel.EnterTypingState())
                {
                    using (var http = _httpFactory.CreateClient())
                    {
                        if (query != null)
                        {
                            HttpResponseMessage msg = await http.GetAsync(query, HttpCompletionOption.ResponseHeadersRead);
                            if(msg.StatusCode != System.Net.HttpStatusCode.OK)
                            {
                                await ReplyErrorLocalizedAsync("inspiro_query_not_fount").ConfigureAwait(false);
                                return;
                            }
                        }

                        var response = query ?? await http.GetStringAsync("http://inspirobot.me/api?generate=true").ConfigureAwait(false);
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
