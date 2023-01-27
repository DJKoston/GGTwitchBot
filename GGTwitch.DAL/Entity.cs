using System.ComponentModel.DataAnnotations;

namespace GGTwitch.DAL
{
    public abstract class Entity
    {
        [Key]
        public int Id { get; set; }
    }
}
