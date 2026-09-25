using MediatR;
using Axpense.Core.Features.BaseService.Queries.Models;
using Axpense.Data.Entities;
using Axpense.Data.PageModel;
using Axpense.Infrastructure.Intertfaces;
using System;
using System.Collections.Generic;
using System.Text;


namespace Axpense.Core.Features.BaseService.Queries.Handlers
{
    /// <summary>
    ///  public class GetPagedListQuery<T, TResult> : IRequest<PagedResult<TResult>?>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TResult"></typeparam>
    public class GetPagedListQueryHandler<T, TResult> : IRequestHandler<GetPagedListQuery<T, TResult>, PagedResult<TResult>?> where T : BaseEntity
    {
        #region Fields
        private readonly IUnitOfWork _unitOfWork;
        #endregion

        #region constructor
        public GetPagedListQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        #endregion
            
        #region Methods
        public async Task<PagedResult<TResult>?> Handle(GetPagedListQuery<T, TResult> request, CancellationToken cancellationToken)
        {
            return await _unitOfWork.Repository<T>().GetPagedList(
                request.Filter,
                request.Selector,
                request.OrderBy,
                request.PageNumber,
                request.PageSize,
                request.IsDescending,
                cancellationToken);
        }
        #endregion  
    }
}
