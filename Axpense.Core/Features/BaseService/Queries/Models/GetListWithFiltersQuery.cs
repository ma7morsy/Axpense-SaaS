using MediatR;
using Axpense.Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Axpense.Core.Features.BaseService.Queries.Models
{
    public class GetListWithFiltersQuery<T, TResult> : IRequest<List<TResult>>
        where T : BaseEntity
    {
        public Expression<Func<T, bool>> Filter { get; set; }
        public Expression<Func<T, TResult>> Selector { get; set; }
        public Expression<Func<T, object>>? OrderBy { get; set; }
        public bool IsDescending { get; set; }

        public GetListWithFiltersQuery(
            Expression<Func<T, bool>> filter,
            Expression<Func<T, TResult>> selector,
            Expression<Func<T, object>>? orderBy = null,
            bool isDescending = false)
        {
            Filter = filter;
            Selector = selector;
            OrderBy = orderBy;
            IsDescending = isDescending;
        }
    }
}