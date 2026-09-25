using MediatR;
using Axpense.Core.Features.BaseService.Commands.Models;
using Axpense.Core.Features.BaseService.Queries.Models;
using Axpense.Data.Entities;
using Axpense.Infrastructure.Intertfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Axpense.Core.Features.BaseService.Commands.Handlers
{
    public class UpdateFieldCommandHandler<T> : IRequestHandler<UpdateFieldCommand<T>, bool> where T : BaseEntity
    {
        #region Fields
        private readonly IUnitOfWork _unitOfWork;
        #endregion


        #region constructor
        public UpdateFieldCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        #endregion

        #region Methods
        public async Task<bool> Handle(UpdateFieldCommand<T> request, CancellationToken cancellationToken)
        {

            return await _unitOfWork.Repository<T>().UpdateFieldsync(request.ID , request.updateAction, cancellationToken);
           
        }
        #endregion
    }
}
