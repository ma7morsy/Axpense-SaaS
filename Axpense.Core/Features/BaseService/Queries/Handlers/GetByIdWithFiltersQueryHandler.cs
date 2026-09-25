using MediatR;
using Axpense.Core.Features.BaseService.Queries.Models;
using Axpense.Data.Entities;
using Axpense.Infrastructure.Intertfaces;
using System.Threading;
using System.Threading.Tasks;

namespace Axpense.Core.Features.BaseService.Queries.Handlers
{
    public class GetByIdWithFiltersQueryHandler<T, TResult> : IRequestHandler<GetByIdWithFiltersQuery<T, TResult>, TResult>
        where T : BaseEntity
    {

        #region Fields
        private readonly IUnitOfWork _unitOfWork;
        #endregion


        #region constructor
        public GetByIdWithFiltersQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        #endregion

        #region Methods
        public async Task<TResult?> Handle(GetByIdWithFiltersQuery<T, TResult> request, CancellationToken cancellationToken)
        {
            return await _unitOfWork.Repository<T>()
                .GetByIdAsync(request.Filter, request.Selector, request.OrderBy, request.IsDescending, cancellationToken);
        }
        #endregion
    }
}
