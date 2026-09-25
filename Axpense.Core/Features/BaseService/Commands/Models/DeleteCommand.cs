using MediatR;
using System;
using System.Collections.Generic;
using Axpense.Data.Entities;

using System.Text;

namespace Axpense.Core.Features.BaseService.Commands.Models
{
    public class DeleteCommand<T> : IRequest<bool> where T : BaseEntity
    {
        public Guid EntityId { get; set; }

        public DeleteCommand(Guid entityId)
        {
            EntityId = entityId;
        }   
    }
}
