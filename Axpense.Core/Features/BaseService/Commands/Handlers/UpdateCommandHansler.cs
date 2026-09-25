using MediatR;
using Axpense.Core.Features.BaseService.Commands.Models;
using Axpense.Data.Entities;
using Axpense.Infrastructure.Intertfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Axpense.Core.Features.BaseService.Commands.Handlers
{
    public class UpdateCommandHansler<T> :IRequestHandler<UpdateCommand<T>, int> where T : BaseEntity   
    {
        #region Fields
        private readonly IUnitOfWork _unitOfWork;
        #endregion


        #region constructor
        public UpdateCommandHansler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        #endregion

        #region Methods
        public async Task<int> Handle(UpdateCommand<T> request, CancellationToken cancellationToken)
        {

            return await _unitOfWork.Repository<T>().UpdateAsync(request.Entity, request.UserId, cancellationToken);

        }
        #endregion 
    }

}
