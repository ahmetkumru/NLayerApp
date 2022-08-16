using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NLayer.Core;
using NLayer.Core.DTOs;
using NLayer.Core.Repositories;
using NLayer.Core.Services;
using NLayer.Core.UnitOfWorks;
using NLayer.Service.Exceptions;

namespace NLayer.Caching
{
    public class ProductServiceWithCaching : IProductService
    {
        private const string cacheProductKey = "productsCache";
        private readonly IMapper _mapper;
        private readonly IMemoryCache _memoryCache;
        private readonly IProductRepository _productRepository;
        private readonly IUnitOfWork _unitOfWork;

        public ProductServiceWithCaching(IMapper mapper, IMemoryCache memoryCache, IProductRepository productRepository, IUnitOfWork unitOfWork)
        {
            _mapper = mapper;
            _memoryCache = memoryCache;
            _productRepository = productRepository;
            _unitOfWork = unitOfWork;

            //Sadece bu cache key ine sahip data var mı yok mu onu öğrenmeye çalışıyorum.Cachedeki datayı almak istemiyorum.
            // Memory boşuna allocate etmesin diye _ kullanarak allocate etmesini engelliyorum.
            if (!_memoryCache.TryGetValue(cacheProductKey, out _))
            {
                _memoryCache.Set(cacheProductKey, _productRepository.GetProductsWithCategory().Result);
            }
        }

        public async Task<Product> AddAsync(Product entity)
        {
            await _productRepository.AddAsync(entity);
            await _unitOfWork.CommitAsync();
            await CacheAllProductsAsync();
            return entity;

        }

        public async Task<IEnumerable<Product>> AddRangeAsync(IEnumerable<Product> entities)
        {
            await _productRepository.AddRangeAsync(entities);
            await _unitOfWork.CommitAsync();
            await CacheAllProductsAsync();
            return entities;
        }

        public Task<bool> AnyAsync(Expression<Func<Product, bool>> expression)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Product>> GetAllAsync()
        {
            return Task.FromResult(_memoryCache.Get<IEnumerable<Product>>(cacheProductKey));   
        }

        public Task<Product> GetByIdAsync(int id)//istediği productı asenkron oalrak döndük. Neden from result çünü await kullanmıyoruz.
            // iş bu await kullanmadığımız için async de kullanmıyoruz. bizden de bir task bekliyor. task.from result static methodu product ımızı dönüyoruz.
        {
            var product = _memoryCache.Get<List<Product>>(cacheProductKey).FirstOrDefault(x => x.Id == id);
            if(product == null)
            {
                throw new NotFoundException($"{typeof(Product).Name}({id}) does not exist"); 
            }

            return Task.FromResult(product);
        }

        public Task<CustomResponseDto<List<ProductWithCategoryDto>>> GetProductsWithCategory()
        {
            var products = _memoryCache.Get<IEnumerable<Product>>(cacheProductKey);
            var productsWithCategoryDto = _mapper.Map<List<ProductWithCategoryDto>>(products);
            return Task.FromResult(CustomResponseDto<List<ProductWithCategoryDto>>.Success(200,productsWithCategoryDto));
        }

        public async Task RemoveAsync(Product entity)
        {
            _productRepository.Remove(entity);
            await _unitOfWork.CommitAsync();
            await CacheAllProductsAsync();
            
            
        }

        public async Task RemoveRangeAsync(IEnumerable<Product> entities)
        {
            _productRepository.RemoveRange(entities);
            await _unitOfWork.CommitAsync();
            await CacheAllProductsAsync();
        }

        public async Task UpdateAsync(Product entity)
        {
           _productRepository.Update(entity);
           await _unitOfWork.CommitAsync();
            await CacheAllProductsAsync();

            
        }

        public IQueryable<Product> Where(Expression<Func<Product, bool>> expression)
        {
            return _memoryCache.Get<List<Product>>(cacheProductKey).Where(expression.Compile()).AsQueryable();
        }

        public async Task CacheAllProductsAsync()
        {
           await _memoryCache.Set(cacheProductKey, _productRepository.GetAll().ToListAsync());
        }
    }
}
