using MediatR;
using Axpense.Core.Features.BaseService.Queries.Models;
using Axpense.Data.Entities;
using Axpense.Infrastructure.Intertfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Axpense.Core.Features.BaseService.Queries.Handlers
{
    public class GetListwithFilterQueryHandler<T> : IRequestHandler<GetListwithFilterQuery<T>, List<T>> where T : BaseEntity
    {
        #region Fields
        private readonly IUnitOfWork _unitOfWork;
        #endregion  


        #region constructor
        public GetListwithFilterQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        #endregion

        #region Methods
        public async Task<List<T>> Handle(GetListwithFilterQuery<T> request, CancellationToken cancellationToken)
        {
            return await _unitOfWork.Repository<T>().GetListAsync(request.Filter, cancellationToken);
        }
        #endregion
    }
}
