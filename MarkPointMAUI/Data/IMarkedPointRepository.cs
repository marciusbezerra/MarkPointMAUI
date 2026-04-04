using MarkPointMAUI.Models;

namespace MarkPointMAUI.Data
{
    public interface IMarkedPointRepository
    {
        Task<List<MarkedPoint>> GetPointsAsync();
        Task<MarkedPoint> GetPointAsync(int id);
        Task<int> SavePointAsync(MarkedPoint point);
        Task<int> DeletePointByIdAsync(int pointId);
    }
}
