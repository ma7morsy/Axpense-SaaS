using MediatR;
using Axpense.Core.Features.BaseService.Queries.Models;
using Axpense.Data.Entities;
using Axpense.Infrastructure.Intertfaces;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Axpense.Core.Features.BaseService.Queries.Handlers
{
    public class GetEntityListQueryHandler<T> : IRequestHandler<GetEntityListQuery<T>, List<T>>
            where T : BaseEntity
    {
        #region Fields
        private readonly IUnitOfWork _unitOfWork;
        #endregion


        #region constructor
        public GetEntityListQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        #endregion

        #region Methods
        public async Task<List<T>> Handle(GetEntityListQuery<T> request, CancellationToken cancellationToken)
        {
            return await _unitOfWork.Repository<T>().GetAllAsync();
        }
     
        #endregion
    }
}