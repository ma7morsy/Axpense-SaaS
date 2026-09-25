using MediatR;
using Axpense.Data.Entities;
using System;

namespace Axpense.Core.Features.BaseService.Commands.Models
{
    public class AddNewCommand<T> : IRequest<bool>
        where T : BaseEntity
    {
        public T Entity { get; set; }
        public Guid CreatedById { get; set; }

        public AddNewCommand(T entity, Guid createdById)
        {
            Entity = entity;
            CreatedById = createdById;
        }
    }
}