using GGTwitch.DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace GGTwitch.DAL
{
    public class Context : DbContext
    {
        public Context(DbContextOptions<Context> options) : base(options) { }
        public DbSet<Streams> Streams { get; set; }
        public DbSet<Pokecatches> Pokecatches { get; set; }
        public DbSet<PCG> PCG { get; set; }
    }
}
