using MediatR;
using Axpense.Core.Features.BaseService.Commands.Models;
using Axpense.Data.Entities;
using Axpense.Infrastructure.Intertfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Axpense.Core.Features.BaseService.Commands.Handlers
{
    public class AddAsyncGetIDCommandHandler<T> :IRequestHandler<AddAsyncGetIDCommand<T>, (bool Success, Guid EntityId)> where T : BaseEntity   
    {
        #region Fields
        private readonly IUnitOfWork _unitOfWork;
        #endregion


        #region constructor
        public AddAsyncGetIDCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        #endregion

        #region Methods
        public async Task<(bool Success, Guid EntityId)> Handle(AddAsyncGetIDCommand<T> request, CancellationToken cancellationToken)
        {

            return await _unitOfWork.Repository<T>().AddAsyncGetID(request.Entity, request.CreatedById, cancellationToken);

        }
        #endregion 
    }
}
