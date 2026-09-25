using MediatR;
using Axpense.Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Axpense.Core.Features.BaseService.Queries.Models
{
    public class GetListwithFilterQuery<T> : IRequest<List<T>> where T : BaseEntity
    {

        public Expression<Func<T, bool>> Filter { get; set; }

        public GetListwithFilterQuery(Expression<Func<T, bool>> filter)
        {
            Filter = filter;
        }
    }
}
