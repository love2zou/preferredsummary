using System.Threading.Tasks;
using Preferred.Api.Models;

namespace Preferred.Api.Services
{
    public interface IReservationDemoService
    {
        Task<ReservationDemoSeedResultDto> SeedDemoDataAsync();
    }
}
