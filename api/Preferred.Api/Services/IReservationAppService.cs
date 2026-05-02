using System.Collections.Generic;
using System.Threading.Tasks;
using Preferred.Api.Models;

namespace Preferred.Api.Services
{
    public interface IReservationAppService
    {
        Task<List<ReservationClubDto>> GetClubsAsync(string? city = null);
        Task<ReservationHomeDto?> GetHomeAsync(int memberId);
        Task<List<ReservationTrainerCardDto>> GetTrainersAsync(ReservationTrainerQuery query);
        Task<ReservationTrainerDetailDto?> GetTrainerDetailAsync(int trainerId);
        Task<ReservationBookingPageDto?> GetBookingPageAsync(int trainerId, int memberId);
        Task<ReservationCreateResultDto?> CreateReservationAsync(ReservationCreateRequest request);
        Task<List<ReservationOrderItemDto>> GetMemberReservationsAsync(int memberId, string? status = null);
        Task<bool> CancelReservationAsync(ReservationCancelRequest request);
        Task<ReservationProfileDto?> GetProfileAsync(int memberId);
    }
}
