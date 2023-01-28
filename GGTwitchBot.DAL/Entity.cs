using System.ComponentModel.DataAnnotations;

namespace GGTwitchBot.DAL
{
    public abstract class Entity
    {
        [Key]
        public int Id { get; set; }
    }
}
