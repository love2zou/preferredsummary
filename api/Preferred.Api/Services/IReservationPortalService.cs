using System.Threading.Tasks;
using Preferred.Api.Models;

namespace Preferred.Api.Services
{
    public interface IReservationPortalService
    {
        Task<ReservationFlowResponseDto?> GetMemberFlowAsync(int memberId);
        Task<ReservationCommerceResponseDto> GetCommerceAsync(int memberId);
        Task<ReservationMemberCenterResponseDto?> GetMemberCenterAsync(int memberId);
        Task<ReservationCoachWorkbenchResponseDto?> GetCoachWorkbenchAsync(int coachUserId);
    }
}
