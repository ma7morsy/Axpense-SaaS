using MediatR;
using Axpense.Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using Axpense.Data.PageModel;

namespace Axpense.Core.Features.BaseService.Queries.Models
{
    public class GetPagedListQuery<T, TResult> : IRequest<PagedResult<TResult>?>
        where T : BaseEntity
    {
        public Expression<Func<T, bool>> Filter { get; set; }
        public Expression<Func<T, TResult>> Selector { get; set; }
        public Expression<Func<T, object>>? OrderBy { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public bool IsDescending { get; set; }

        public GetPagedListQuery(
            Expression<Func<T, bool>> filter,
            Expression<Func<T, TResult>> selector,
            Expression<Func<T, object>>? orderBy = null,
            int pageNumber = 1,
            int pageSize = 10,
            bool isDescending = false)
        {
            Filter = filter;
            Selector = selector;
            OrderBy = orderBy;
            PageNumber = pageNumber;
            PageSize = pageSize;
            IsDescending = isDescending;
        }
    }
   
}
