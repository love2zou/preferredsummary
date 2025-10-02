using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Preferred.Api.Data;
using Preferred.Api.Models;

namespace Preferred.Api.Services
{
    /// <summary>
    /// 分类服务实现
    /// </summary>
    public class CategoryService : ICategoryService
    {
        private readonly ApplicationDbContext _context;

        public CategoryService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<CategoryListDto>> GetCategoryList(int page = 1, int pageSize = 10, CategorySearchParams searchParams = null)
        {
            var query = _context.Categories.AsQueryable();

            // 应用搜索条件
            if (searchParams != null)
            {
                if (!string.IsNullOrEmpty(searchParams.CategoryCode))
                {
                    query = query.Where(c => c.CategoryCode.Contains(searchParams.CategoryCode));
                }
                if (!string.IsNullOrEmpty(searchParams.CategoryName))
                {
                    query = query.Where(c => c.CategoryName.Contains(searchParams.CategoryName));
                }
                if (!string.IsNullOrEmpty(searchParams.Description))
                {
                    query = query.Where(c => c.Description.Contains(searchParams.Description));
                }
            }

            // 排序和分页
            var categories = await query
                .OrderBy(c => c.SeqNo)
                .ThenByDescending(c => c.CrtTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new CategoryListDto
                {
                    Id = c.Id,
                    CategoryCode = c.CategoryCode,
                    CategoryName = c.CategoryName,
                    CategoryIcon = c.CategoryIcon,
                    Description = c.Description,
                    SeqNo = c.SeqNo,
                    CrtTime = c.CrtTime,
                    UpdTime = c.UpdTime
                })
                .ToListAsync();

            return categories;
        }

        public async Task<int> GetCategoryCount(CategorySearchParams searchParams = null)
        {
            var query = _context.Categories.AsQueryable();

            // 应用搜索条件
            if (searchParams != null)
            {
                if (!string.IsNullOrEmpty(searchParams.CategoryCode))
                {
                    query = query.Where(c => c.CategoryCode.Contains(searchParams.CategoryCode));
                }
                if (!string.IsNullOrEmpty(searchParams.CategoryName))
                {
                    query = query.Where(c => c.CategoryName.Contains(searchParams.CategoryName));
                }
                if (!string.IsNullOrEmpty(searchParams.Description))
                {
                    query = query.Where(c => c.Description.Contains(searchParams.Description));
                }
            }

            return await query.CountAsync();
        }

        public async Task<Category> GetCategoryById(int id)
        {
            return await _context.Categories.FindAsync(id);
        }

        public async Task<List<string>> GetCategoryCodeList()
        {
            return await _context.Categories
                .Select(c => c.CategoryCode)
                .Distinct()
                .OrderBy(code => code)
                .ToListAsync();
        }

        public async Task<bool> CreateCategory(CategoryDto categoryDto)
        {
            try
            {
                var category = new Category
                {
                    CategoryCode = categoryDto.CategoryCode,
                    CategoryName = categoryDto.CategoryName,
                    CategoryIcon = categoryDto.CategoryIcon,
                    Description = categoryDto.Description,
                    SeqNo = categoryDto.SeqNo,
                    CrtTime = DateTime.Now,
                    UpdTime = DateTime.Now
                };

                _context.Categories.Add(category);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UpdateCategory(int id, CategoryDto categoryDto)
        {
            try
            {
                var category = await _context.Categories.FindAsync(id);
                if (category == null)
                    return false;

                category.CategoryCode = categoryDto.CategoryCode;
                category.CategoryName = categoryDto.CategoryName;
                category.CategoryIcon = categoryDto.CategoryIcon;
                category.Description = categoryDto.Description;
                category.SeqNo = categoryDto.SeqNo;
                category.UpdTime = DateTime.Now;

                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DeleteCategory(int id)
        {
            try
            {
                var category = await _context.Categories.FindAsync(id);
                if (category == null)
                    return false;

                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> IsCategoryCodeExists(string categoryCode, int? excludeId = null)
        {
            var query = _context.Categories.Where(c => c.CategoryCode.ToLower() == categoryCode.ToLower());
            
            if (excludeId.HasValue)
            {
                query = query.Where(c => c.Id != excludeId.Value);
            }
            
            return await query.AnyAsync();
        }

        public async Task<BatchDeleteResult> BatchDeleteCategories(int[] ids)
        {
            var result = new BatchDeleteResult();
            
            foreach (var id in ids)
            {
                try
                {
                    var category = await _context.Categories.FindAsync(id);
                    if (category != null)
                    {
                        _context.Categories.Remove(category);
                        await _context.SaveChangesAsync();
                        result.SuccessCount++;
                    }
                    else
                    {
                        result.FailCount++;
                        result.FailedIds.Add(id);
                    }
                }
                catch
                {
                    result.FailCount++;
                    result.FailedIds.Add(id);
                }
            }
            
            return result;
        }

        public async Task<bool> UpdateCategorySeqNo(int id, int seqNo)
        {
            try
            {
                var category = await _context.Categories.FindAsync(id);
                if (category == null)
                    return false;

                category.SeqNo = seqNo;
                category.UpdTime = DateTime.Now;
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}