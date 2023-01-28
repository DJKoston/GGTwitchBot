namespace GGTwitchBot.DAL.Models
{
    public class PCG : Entity
    {
        public string DexNumber { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Tier { get; set; }
        public string Generation { get; set; }
        public string DexInfo { get; set; }
        public string Weight { get; set; }
        public string SuggestedBalls { get; set; }
        public int BST { get; set; }
    }
}
