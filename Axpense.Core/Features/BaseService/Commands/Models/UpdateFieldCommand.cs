using MediatR;
using Axpense.Data.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Axpense.Core.Features.BaseService.Commands.Models
{
    public class UpdateFieldCommand<T> : IRequest<bool> where T : BaseEntity
    {
        public Guid ID { set;get; }
        public Action<T> updateAction;

        public UpdateFieldCommand(Guid Id, Action<T> UpdateAction)
        {
            ID = Id;
            updateAction = UpdateAction;
        }

    }
}
