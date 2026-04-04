namespace MarkPointMAUI.Models
{
using SQLite;

    public class MarkedPoint
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string? Name { get; set; }
        public double Lat { get; set; }
        public double Long { get; set; }
    }
}
