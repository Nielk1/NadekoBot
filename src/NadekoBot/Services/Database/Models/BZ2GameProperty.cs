using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;

namespace NadekoBot.Services.Database.Models
{
    public class BZ2GameProperty : DbEntity
    {
        public string TermType { get; set; }
        public string Term { get; set; }
        public string Value { get; set; }
    }
}