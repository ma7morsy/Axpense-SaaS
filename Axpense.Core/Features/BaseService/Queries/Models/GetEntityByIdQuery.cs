using MediatR;
using Axpense.Data.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Axpense.Core.Features.BaseService.Queries.Models
{
    public class GetEntityByIdQuery<T> : IRequest<T?> where T : BaseEntity
    {
        public Guid Id { get; }
        public GetEntityByIdQuery(Guid id)
        {
            Id = id;
        }

    }
}
