using MediatR;
using Axpense.Core.Features.BaseService.Commands.Models;
using Axpense.Data.Entities;
using Axpense.Infrastructure.Intertfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Axpense.Core.Features.BaseService.Commands.Handlers
{
    public class DeleteCommandHandler<T> : IRequestHandler<DeleteCommand<T>, bool> where T : BaseEntity
    {  
        #region Fields
        private readonly IUnitOfWork _unitOfWork;
        #endregion


        #region constructor
        public DeleteCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        #endregion

        #region Methods
        public async Task<bool> Handle(DeleteCommand<T> request, CancellationToken cancellationToken)
        {

            return await _unitOfWork.Repository<T>().DeleteAsync(request.EntityId, cancellationToken);

        }
        #endregion 
    }
}
