using MediatR;
using Axpense.Data.Entities;
using System.Collections.Generic;

namespace Axpense.Core.Features.BaseService.Queries.Models
{
    public class GetEntityListQuery<T> : IRequest<List<T>> where T : BaseEntity
    {

    }
}