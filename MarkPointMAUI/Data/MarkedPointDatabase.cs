using MarkPointMAUI.Models;
using SQLite;

namespace MarkPointMAUI.Data
{
    public class MarkedPointDatabase : IMarkedPointRepository
    {
        readonly SQLiteAsyncConnection _database;

        public MarkedPointDatabase()
        {
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "markedpoints.db3");
            _database = new SQLiteAsyncConnection(dbPath);
            // Ensure table exists (synchronously during startup)
            _database.CreateTableAsync<MarkedPoint>().GetAwaiter().GetResult();
        }

        public Task<List<MarkedPoint>> GetPointsAsync()
        {
            return _database.Table<MarkedPoint>().ToListAsync();
        }

        public Task<MarkedPoint> GetPointAsync(int id)
        {
            return _database.Table<MarkedPoint>().Where(p => p.Id == id).FirstOrDefaultAsync();
        }

        public Task<int> SavePointAsync(MarkedPoint point)
        {
            if (point.Id == 0)
                return _database.InsertAsync(point);
            return _database.UpdateAsync(point);
        }

        public Task<int> DeletePointAsync(MarkedPoint point)
        {
            return _database.DeleteAsync(point);
        }
    }
}
