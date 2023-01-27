using GGTwitch.DAL;
using GGTwitch.DAL.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace GGTwitch.Core.Services
{
    public interface IPCGService
    {
        Task<PCG> GetPokemonByDexNumberAsync(string dexNumber);
        Task<PCG> GetPokemonByNameAsync(string pokemonName);
    }

    public class PCGService : IPCGService
    {
        private readonly DbContextOptions<Context> _options;

        public PCGService(DbContextOptions<Context> options)
        {
            _options = options;
        }

        public async Task<PCG> GetPokemonByDexNumberAsync(string dexEntry)
        {
            using var context = new Context(_options);

            return await context.PCG.FirstOrDefaultAsync(x => x.DexNumber == dexEntry);
        }

        public async Task<PCG> GetPokemonByNameAsync(string pokemonName)
        {
            using var context = new Context(_options);

            return await context.PCG.FirstOrDefaultAsync(x => x.Name == pokemonName);
        }
    }
}
