using System.Collections.Generic;
using System.Threading.Tasks;
using Preferred.Api.Models;

namespace Preferred.Api.Services
{
    public interface IReservationAdminService
    {
        Task<List<ReservationLookupOptionDto>> GetClubOptionsAsync();
        Task<List<ReservationLookupOptionDto>> GetTrainerOptionsAsync();
        Task<List<ReservationLookupOptionDto>> GetUserOptionsAsync(string? keyword = null);

        Task<List<ReservationClubAdminDto>> GetClubListAsync(int page, int pageSize, ReservationClubAdminSearchParams? searchParams = null);
        Task<int> GetClubCountAsync(ReservationClubAdminSearchParams? searchParams = null);
        Task<ReservationClubAdminDto?> GetClubByIdAsync(int id);
        Task<bool> CreateClubAsync(ReservationClubAdminEditDto dto);
        Task<bool> UpdateClubAsync(int id, ReservationClubAdminEditDto dto);
        Task<bool> DeleteClubAsync(int id);

        Task<List<ReservationTrainerAdminDto>> GetTrainerListAsync(int page, int pageSize, ReservationTrainerAdminSearchParams? searchParams = null);
        Task<int> GetTrainerCountAsync(ReservationTrainerAdminSearchParams? searchParams = null);
        Task<ReservationTrainerAdminDto?> GetTrainerByIdAsync(int id);
        Task<bool> CreateTrainerAsync(ReservationTrainerAdminEditDto dto);
        Task<bool> UpdateTrainerAsync(int id, ReservationTrainerAdminEditDto dto);
        Task<bool> DeleteTrainerAsync(int id);

        Task<List<ReservationSessionAdminDto>> GetSessionListAsync(int page, int pageSize, ReservationSessionAdminSearchParams? searchParams = null);
        Task<int> GetSessionCountAsync(ReservationSessionAdminSearchParams? searchParams = null);
        Task<ReservationSessionAdminDto?> GetSessionByIdAsync(int id);
        Task<bool> CreateSessionAsync(ReservationSessionAdminEditDto dto);
        Task<bool> UpdateSessionAsync(int id, ReservationSessionAdminEditDto dto);
        Task<bool> DeleteSessionAsync(int id);

        Task<List<ReservationScheduleAdminDto>> GetScheduleListAsync(int page, int pageSize, ReservationScheduleAdminSearchParams? searchParams = null);
        Task<int> GetScheduleCountAsync(ReservationScheduleAdminSearchParams? searchParams = null);
        Task<ReservationScheduleAdminDto?> GetScheduleByIdAsync(int id);
        Task<bool> CreateScheduleAsync(ReservationScheduleAdminEditDto dto);
        Task<bool> UpdateScheduleAsync(int id, ReservationScheduleAdminEditDto dto);
        Task<bool> DeleteScheduleAsync(int id);

        Task<List<ReservationPackageAdminDto>> GetPackageListAsync(int page, int pageSize, ReservationPackageAdminSearchParams? searchParams = null);
        Task<int> GetPackageCountAsync(ReservationPackageAdminSearchParams? searchParams = null);
        Task<ReservationPackageAdminDto?> GetPackageByIdAsync(int id);
        Task<bool> CreatePackageAsync(ReservationPackageAdminEditDto dto);
        Task<bool> UpdatePackageAsync(int id, ReservationPackageAdminEditDto dto);
        Task<bool> DeletePackageAsync(int id);

        Task<List<ReservationOrderAdminDto>> GetOrderListAsync(int page, int pageSize, ReservationOrderAdminSearchParams? searchParams = null);
        Task<int> GetOrderCountAsync(ReservationOrderAdminSearchParams? searchParams = null);
        Task<bool> UpdateOrderStatusAsync(int id, string statusCode);
    }
}
