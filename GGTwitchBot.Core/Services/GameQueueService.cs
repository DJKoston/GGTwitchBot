using GGTwitchBot.DAL;
using GGTwitchBot.DAL.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace GGTwitchBot.Core.Services
{
    public interface IGameQueueService
    {
        string GetNextInQueue();
        void AddToQueue(string userName);
        bool CheckIfInQueue(string userName);
        void RemoveFromQueue(string userName);
    }

    public class GameQueueService : IGameQueueService
    {
        private readonly DbContextOptions<Context> _options;

        public GameQueueService(DbContextOptions<Context> options)
        {
            _options = options;
        }

        public string GetNextInQueue()
        {
            using var context = new Context(_options);

            var QueueCount = context.GameQueue.Count();

            string nextUser = null;

            if (QueueCount > 0) 
            {
                var dbUser = context.GameQueue.OrderBy(x => x.Id).FirstOrDefault();

                nextUser = dbUser.userName;

                context.Remove(dbUser);

                context.SaveChanges();
            }

            return nextUser;
        }

        public async void AddToQueue(string userName)
        {
            using var context = new Context(_options);

            var addQueue = new GameQueue { userName= userName };

            await context.AddAsync(addQueue);
            await context.SaveChangesAsync();
        }

        public bool CheckIfInQueue(string userName)
        {
            using var context = new Context(_options);

            bool isInQueue = false;

            var checks = context.GameQueue.FirstOrDefault(x => x.userName == userName);

            if (checks != null) { isInQueue = true; }

            return isInQueue;
        }

        public async void RemoveFromQueue(string userName)
        {
            using var context = new Context(_options);

            var userToRemove = await context.GameQueue.FirstOrDefaultAsync(x => x.userName == userName);

            context.Remove(userToRemove);

            await context.SaveChangesAsync();
        }
    }
}
