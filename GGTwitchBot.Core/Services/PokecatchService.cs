using GGTwitchBot.DAL;
using GGTwitchBot.DAL.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace GGTwitchBot.Core.Services
{
    public interface IPokecatchService
    {
        List<string> GetPokecatchersListAsync(string streamer);
        Task<int> GetPokecatchersCountAsync(string streamer);
        Task AddCatchAsync(string streamer, string catcher);
        Task RemoveCatchAsync(string streamer, string catcher);
        Task RemoveAllCatchesAsync(string streamer);
    }

    public class PokecatchService : IPokecatchService
    {
        private readonly DbContextOptions<Context> _options;

        public PokecatchService(DbContextOptions<Context> options)
        {
            _options = options;
        }

        public List<string> GetPokecatchersListAsync(string streamer)
        {
            using var context = new Context(_options);

            List<string> list = new();
            var throwersDb = context.Pokecatches.Where(x => x.StreamerUsername == streamer);

            foreach (var thrower in throwersDb)
            {
                list.Add(thrower.CatcherUsername);
            }

            return list;
        }

        public async Task<int> GetPokecatchersCountAsync(string streamer)
        {
            using var context = new Context(_options);

            return await context.Pokecatches.Where(x => x.StreamerUsername == streamer).CountAsync();
        }

        public async Task AddCatchAsync(string streamer, string catcher)
        {
            using var context = new Context(_options);

            Pokecatches pokecatch = new();
            pokecatch.StreamerUsername = streamer;
            pokecatch.CatcherUsername = catcher;

            await context.AddAsync(pokecatch);

            await context.SaveChangesAsync();
        }

        public async Task RemoveCatchAsync(string streamer, string catcher)
        {
            using var context = new Context(_options);

            var pokecatch = context.Pokecatches.FirstOrDefault(x => x.StreamerUsername == streamer && x.CatcherUsername == catcher);

            context.Remove(pokecatch);

            await context.SaveChangesAsync();
        }

        public async Task RemoveAllCatchesAsync(string streamer)
        {
            using var context = new Context(_options);

            var pokecatchers = context.Pokecatches.Where(x => x.StreamerUsername == streamer);

            foreach (var pokecatch in pokecatchers)
            {
                context.Remove(pokecatch);
            }

            await context.SaveChangesAsync();
        }
    }
}
