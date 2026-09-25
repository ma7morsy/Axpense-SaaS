using Axpense.Service.Common;

namespace Axpense.Service.Catalog
{
    public interface IPartsCatalogService
    {
        Task<PartsCatalogDto> GetAsync(Guid organizationId, CancellationToken ct = default);

        Task<ServiceResult<PartCategoryDto>> CreateCategoryAsync(Guid organizationId, Guid userId, CatalogNameRequest request, CancellationToken ct = default);
        Task<ServiceResult<PartCategoryDto>> UpdateCategoryAsync(Guid organizationId, Guid userId, Guid categoryId, CatalogNameRequest request, CancellationToken ct = default);
        /// <summary>Deletes the category and every part in it.</summary>
        Task<ServiceResult<bool>> DeleteCategoryAsync(Guid organizationId, Guid categoryId, CancellationToken ct = default);

        Task<ServiceResult<PresetPartDto>> CreatePartAsync(Guid organizationId, Guid userId, Guid categoryId, CatalogNameRequest request, CancellationToken ct = default);
        Task<ServiceResult<PresetPartDto>> UpdatePartAsync(Guid organizationId, Guid userId, Guid partId, CatalogNameRequest request, CancellationToken ct = default);
        Task<ServiceResult<bool>> DeletePartAsync(Guid organizationId, Guid partId, CancellationToken ct = default);
    }
}
