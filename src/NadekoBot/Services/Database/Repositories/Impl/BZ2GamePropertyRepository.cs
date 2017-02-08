using NadekoBot.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace NadekoBot.Services.Database.Repositories.Impl
{
    public class BZ2GamePropertyRepository : Repository<BZ2GameProperty>, IBZ2GamePropertyRepository
    {
        public BZ2GamePropertyRepository(DbContext context) : base(context)
        {
        }
    }
}
