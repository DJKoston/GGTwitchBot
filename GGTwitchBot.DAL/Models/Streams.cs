
namespace GGTwitchBot.DAL.Models
{
    public class Streams : Entity
    {
        public string StreamerUsername { get; set; }
        public bool BetaTester { get; set; }
    }
}
