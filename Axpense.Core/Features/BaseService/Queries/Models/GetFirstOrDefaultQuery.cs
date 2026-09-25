using MediatR;
using Axpense.Data.Entities;
using System;
using System.Linq.Expressions;

namespace Axpense.Core.Features.BaseService.Queries.Models
{
    public class GetFirstOrDefaultQuery<T> : IRequest<T?> where T : BaseEntity
    {
        public Expression<Func<T, bool>> Filter { get; set; }

        public GetFirstOrDefaultQuery(Expression<Func<T, bool>> filter)
        {
            Filter = filter;
        }
    }
}