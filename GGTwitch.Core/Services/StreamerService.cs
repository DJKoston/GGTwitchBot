using GGTwitch.DAL;
using GGTwitch.DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace GGTwitch.Core.Services
{
    public interface IStreamerService
    {
        Task<Streams> GetStream(string streamId);
        void NewStreamAsync(string streamId);
        void DeleteStreamAsync(string streamId);
        List<Streams> GetStreamsToConnect();
    }

    public class StreamService : IStreamerService
    {
        private readonly DbContextOptions<Context> _options;

        public StreamService(DbContextOptions<Context> options)
        {
            _options = options;
        }

        public Task<Streams> GetStream(string streamId)
        {
            using var context = new Context(_options);

            return context.Streams.FirstOrDefaultAsync(x => x.StreamerUsername == streamId);
        }

        public void NewStreamAsync(string streamId)
        {
            using var context = new Context(_options);

            Streams stream = new();
            stream.StreamerUsername = streamId;

            context.Add(stream);

            context.SaveChanges();
        }

        public void DeleteStreamAsync(string streamId)
        {
            using var context = new Context(_options);

            var stream = context.Streams.FirstOrDefault(x => x.StreamerUsername == streamId);

            context.Remove(stream);

            context.SaveChanges();
        }

        public List<Streams> GetStreamsToConnect()
        {
            using var context = new Context(_options);

            List<Streams> streams = new();

            var allStreams = context.Streams.Where(x => x.StreamerUsername != null);

            foreach(Streams stream in allStreams)
            {
                streams.Add(stream);
            }

            return streams;
        }
    }
}
