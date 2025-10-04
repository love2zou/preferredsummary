using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Preferred.Api.Models;

namespace Preferred.Api.Services
{
    public interface IBookingService
    {
        Task<List<BoundCoachDto>> GetBoundCoachesAsync(int memberId);
        Task<bool> BindCoachAsync(int memberId, int coachId);
        Task<bool> UnbindCoachAsync(int memberId, int coachId);
        Task<List<AvailableSlotDto>> GetAvailableSlotsAsync(int coachId, DateTime bookDate);
        Task<bool> BatchCreateAsync(CreateBatchRequest request);
        Task<List<BookingItemDto>> ListAsync(int memberId);
        Task<bool> CancelAsync(int id);
        Task<List<BoundMemberDto>> GetBoundMembersAsync(int coachId);
    }
}