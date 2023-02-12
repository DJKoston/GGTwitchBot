using GGTwitchBot.DAL;
using GGTwitchBot.DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace GGTwitchBot.Core.Services
{
    public interface IStreamerService
    {
        List<Streams> GetAllStreams();
        void NewStream(string streamId);
        void NewBetaStream(string streamId);
        void DeleteStreamAsync(string streamId);
        void AddUserToBeta(string streamId);
        void RemoveUserFromBeta(string streamId);
        List<Streams> GetNonBetaStreamsToConnect();
        List<Streams> GetBetaStreamsToConnect();
    }

    public class StreamService : IStreamerService
    {
        private readonly DbContextOptions<Context> _options;

        public StreamService(DbContextOptions<Context> options)
        {
            _options = options;
        }

        public List<Streams> GetAllStreams()
        {
            using var context = new Context(_options);

            List<Streams> streams = new();

            var allStreams = context.Streams.Where(x => x.StreamerUsername != null);

            foreach (Streams stream in allStreams)
            {
                streams.Add(stream);
            }

            return streams;
        }

        public void NewStream(string streamId)
        {
            using var context = new Context(_options);

            Streams stream = new();
            stream.StreamerUsername = streamId;
            stream.BetaTester = false;

            context.Add(stream);

            context.SaveChanges();
        }

        public void NewBetaStream(string streamId)
        {
            using var context = new Context(_options);

            Streams stream = new();
            stream.StreamerUsername = streamId;
            stream.BetaTester = true;

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
        
        public void AddUserToBeta(string streamId)
        {
            using var context = new Context(_options);

            var stream = context.Streams.FirstOrDefault(x => x.StreamerUsername == streamId);

            stream.BetaTester = true;

            context.Update(stream);
            context.SaveChanges();
        }

        public void RemoveUserFromBeta(string streamId)
        {
            using var context = new Context(_options);

            var stream = context.Streams.FirstOrDefault(x => x.StreamerUsername == streamId);

            stream.BetaTester = false;

            context.Update(stream);
            context.SaveChanges();
        }

        public List<Streams> GetNonBetaStreamsToConnect()
        {
            using var context = new Context(_options);

            List<Streams> streams = new();

            var allStreams = context.Streams.Where(x => x.StreamerUsername != null).Where(x => x.BetaTester == false);

            foreach(Streams stream in allStreams)
            {
                streams.Add(stream);
            }

            return streams;
        }

        public List<Streams> GetBetaStreamsToConnect()
        {
            using var context = new Context(_options);

            List<Streams> streams = new();

            var allStreams = context.Streams.Where(x => x.StreamerUsername != null).Where(x => x.BetaTester == true);

            foreach (Streams stream in allStreams)
            {
                streams.Add(stream);
            }

            return streams;
        }
    }
}
