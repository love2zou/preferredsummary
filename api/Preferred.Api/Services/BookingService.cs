using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Preferred.Api.Data;
using Preferred.Api.Models;

namespace Preferred.Api.Services
{
    public class BookingService : IBookingService
    {
        private readonly ApplicationDbContext _context;
        public BookingService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<BoundCoachDto>> GetBoundCoachesAsync(int memberId)
        {
            var rels = await _context.CoachMemberRelations
                .Where(r => r.MemberId == memberId)
                .ToListAsync();

            var coachIds = rels.Select(r => r.CoachId).Distinct().ToList();
            var coaches = await _context.Users
                .Where(u => coachIds.Contains(u.Id))
                .Select(u => new BoundCoachDto { CoachId = u.Id, CoachName = u.UserName })
                .ToListAsync();
            return coaches;
        }

        public async Task<bool> BindCoachAsync(int memberId, int coachId)
        {
            var exists = await _context.CoachMemberRelations
                .AnyAsync(r => r.MemberId == memberId && r.CoachId == coachId);
            if (exists) return true;

            var now = DateTime.Now;
            _context.CoachMemberRelations.Add(new CoachMemberRelation
            {
                MemberId = memberId,
                CoachId = coachId,
                SeqNo = 0,
                CrtTime = now,
                UpdTime = now
            });
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UnbindCoachAsync(int memberId, int coachId)
        {
            var rel = await _context.CoachMemberRelations
                .FirstOrDefaultAsync(r => r.MemberId == memberId && r.CoachId == coachId);
            if (rel == null) return false;
            _context.CoachMemberRelations.Remove(rel);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<AvailableSlotDto>> GetAvailableSlotsAsync(int coachId, DateTime bookDate)
        {
            var allSlots = GenerateHalfHourSlots("09:00", "18:00");
            var booked = await _context.BookTasks
                .Where(b => b.CoachId == coachId && b.BookDate.Date == bookDate.Date)
                .Select(b => b.BookTimeSlot)
                .ToListAsync();
            var set = new HashSet<string>(booked);
            var result = new List<AvailableSlotDto>();
            for (int i = 0; i < allSlots.Count; i++)
            {
                var (start, end) = allSlots[i];
                var key = $"{start}-{end}";
                result.Add(new AvailableSlotDto
                {
                    SlotId = i + 1,
                    StartTime = start,
                    EndTime = end,
                    IsAvailable = !set.Contains(key)
                });
            }
            return result;
        }

        public async Task<bool> BatchCreateAsync(CreateBatchRequest request)
        {
            var date = DateTime.Parse(request.BookDate);
            var now = DateTime.Now;
            foreach (var ts in request.TimeSlots)
            {
                var key = $"{ts.StartTime}-{ts.EndTime}";
                var exists = await _context.BookTasks
                    .AnyAsync(b => b.CoachId == request.CoachId && b.BookDate.Date == date.Date && b.BookTimeSlot == key);
                if (exists) continue;
                _context.BookTasks.Add(new BookTask
                {
                    CoachId = request.CoachId,
                    MemberId = request.MemberId,
                    BookDate = date,
                    BookTimeSlot = key,
                    SeqNo = 0,
                    CrtTime = now,
                    UpdTime = now
                });
            }
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<BookingItemDto>> ListAsync(int memberId)
        {
            var items = await _context.BookTasks
                .Where(b => b.MemberId == memberId)
                .OrderByDescending(b => b.BookDate)
                .ThenByDescending(b => b.BookTimeSlot)
                .ToListAsync();

            var coachIds = items.Select(i => i.CoachId).Distinct().ToList();
            var coachMap = await _context.Users
                .Where(u => coachIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.UserName);

            return items.Select(i =>
            {
                var parts = i.BookTimeSlot.Split('-', StringSplitOptions.RemoveEmptyEntries);
                var start = parts.Length > 0 ? parts[0] : "";
                var end = parts.Length > 1 ? parts[1] : "";
                return new BookingItemDto
                {
                    Id = i.Id,
                    MemberId = i.MemberId,
                    CoachId = i.CoachId,
                    CoachName = coachMap.TryGetValue(i.CoachId, out var name) ? name : "",
                    BookDate = i.BookDate.ToString("yyyy-MM-dd"),
                    StartTime = start,
                    EndTime = end,
                    Status = 0
                };
            }).ToList();
        }
        public async Task<List<BookingItemDto>> ListByCoachAsync(int coachId)
        {
            var items = await _context.BookTasks
                .Where(b => b.CoachId == coachId)
                .OrderByDescending(b => b.BookDate)
                .ThenByDescending(b => b.BookTimeSlot)
                .ToListAsync();

            var coachIds = items.Select(i => i.CoachId).Distinct().ToList();
            var coachMap = await _context.Users
                .Where(u => coachIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.UserName);

            return items.Select(i =>
            {
                var parts = i.BookTimeSlot.Split('-', StringSplitOptions.RemoveEmptyEntries);
                var start = parts.Length > 0 ? parts[0] : "";
                var end = parts.Length > 1 ? parts[1] : "";
                return new BookingItemDto
                {
                    Id = i.Id,
                    MemberId = i.MemberId,
                    CoachId = i.CoachId,
                    CoachName = coachMap.TryGetValue(i.CoachId, out var name) ? name : "",
                    BookDate = i.BookDate.ToString("yyyy-MM-dd"),
                    StartTime = start,
                    EndTime = end,
                    Status = 0
                };
            }).ToList();
        }

        public async Task<bool> CancelAsync(int id)
        {
            var entity = await _context.BookTasks.FindAsync(id);
            if (entity == null) return false;
            _context.BookTasks.Remove(entity);
            await _context.SaveChangesAsync();
            return true;
        }

        private static List<(string start, string end)> GenerateHalfHourSlots(string start, string end)
        {
            var list = new List<(string, string)>();
            var day = DateTime.Today;
            var s = DateTime.Parse($"{day:yyyy-MM-dd} {start}");
            var e = DateTime.Parse($"{day:yyyy-MM-dd} {end}");
            while (s < e)
            {
                var next = s.AddMinutes(30);
                list.Add((s.ToString("HH:mm"), next.ToString("HH:mm")));
                s = next;
            }
            return list;
        }

        public async Task<List<BoundMemberDto>> GetBoundMembersAsync(int coachId)
        {
            var rels = await _context.CoachMemberRelations
                .Where(r => r.CoachId == coachId)
                .ToListAsync();

            var memberIds = rels.Select(r => r.MemberId).Distinct().ToList();

            var members = await _context.Users
                .Where(u => memberIds.Contains(u.Id))
                .Select(u => new BoundMemberDto
                {
                    Id = u.Id,
                    UserName = u.UserName,
                    PhoneNumber = u.PhoneNumber
                })
                .ToListAsync();

            return members;
        }

        public async Task<List<ReservedByDateDto>> GetReservedByDateAsync(int coachId, DateTime bookDate)
        {
            var booked = await _context.BookTasks
                .Where(b => b.CoachId == coachId && b.BookDate.Date == bookDate.Date)
                .ToListAsync();

            var memberIds = booked.Select(b => b.MemberId).Distinct().ToList();
            var memberMap = await _context.Users
                .Where(u => memberIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.UserName);

            var result = new List<ReservedByDateDto>();
            foreach (var b in booked)
            {
                var parts = b.BookTimeSlot.Split('-', StringSplitOptions.RemoveEmptyEntries);
                var start = parts.Length > 0 ? parts[0] : "";
                var end = parts.Length > 1 ? parts[1] : "";
                result.Add(new ReservedByDateDto
                {
                    StartTime = start,
                    EndTime = end,
                    MemberName = memberMap.TryGetValue(b.MemberId, out var name) ? name : ""
                });
            }
            return result;
        }
    }
}