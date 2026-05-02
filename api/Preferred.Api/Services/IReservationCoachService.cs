using System.Collections.Generic;
using System.Threading.Tasks;
using Preferred.Api.Models;

namespace Preferred.Api.Services
{
    public interface IReservationCoachService
    {
        Task<ReservationCoachDashboardDto?> GetDashboardAsync(int coachUserId);
        Task<List<ReservationCoachMemberDto>> GetMembersAsync(int coachUserId);
        Task<ReservationCoachScheduleDto?> GetScheduleAsync(int coachUserId, string? scheduleDate = null);
        Task<List<ReservationCoachReservationDto>> GetReservationsAsync(int coachUserId, string? statusCode = null);
        Task<ReservationCoachProfileDto?> GetProfileAsync(int coachUserId);
    }
}
