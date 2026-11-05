namespace RA2LiveController.Models
{
    public class HouseSnapshot
    {
        public uint Address { get; set; }
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsPlayer { get; set; }
        public int Cash { get; set; }
        public int OreValue { get; set; }
        public double Firepower { get; set; }
        public double ROF { get; set; }
    }
}
