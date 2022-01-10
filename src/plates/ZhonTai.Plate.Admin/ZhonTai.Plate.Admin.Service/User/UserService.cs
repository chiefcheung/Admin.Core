using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZhonTai.Common.Attributes;
using ZhonTai.Common.Configs;
using ZhonTai.Common.Domain.Repositories;
using ZhonTai.Common.Helpers;
using ZhonTai.Common.Domain.Dto;
using ZhonTai.Plate.Admin.Domain.Api;
using ZhonTai.Plate.Admin.Domain.PermissionApi;
using ZhonTai.Plate.Admin.Domain.Role;
using ZhonTai.Plate.Admin.Domain.RolePermission;
using ZhonTai.Plate.Admin.Domain.Tenant;
using ZhonTai.Plate.Admin.Domain.User;
using ZhonTai.Plate.Admin.Domain.UserRole;
using ZhonTai.Plate.Admin.Service.Contracts;
using ZhonTai.Plate.Admin.Service.Auth.Dto;
using ZhonTai.Plate.Admin.Service.User.Dto;
using ZhonTai.Tools.DynamicApi;
using ZhonTai.Tools.DynamicApi.Attributes;

namespace ZhonTai.Plate.Admin.Service.User
{
    /// <summary>
    /// 用户服务
    /// </summary>
    [DynamicApi(Area = "admin")]
    public class UserService : BaseService, IUserService, IDynamicApi
    {
        private readonly AppConfig _appConfig;
        private readonly IUserRepository _userRepository;
        private readonly IRepositoryBase<UserRoleEntity> _userRoleRepository;
        private readonly ITenantRepository _tenantRepository;
        private readonly IApiRepository _apiRepository;

        private IRoleRepository _roleRepository => LazyGetRequiredService<IRoleRepository>();

        public UserService(
            AppConfig appConfig,
            IUserRepository userRepository,
            IRepositoryBase<UserRoleEntity> userRoleRepository,
            ITenantRepository tenantRepository,
            IApiRepository apiRepository
        )
        {
            _appConfig = appConfig;
            _userRepository = userRepository;
            _userRoleRepository = userRoleRepository;
            _tenantRepository = tenantRepository;
            _apiRepository = apiRepository;
        }

        /// <summary>
        /// 查询用户
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<IResultOutput> GetAsync(long id)
        {
            var entity = await _userRepository.Select
            .WhereDynamic(id)
            .IncludeMany(a => a.Roles.Select(b => new RoleEntity { Id = b.Id }))
            .ToOneAsync();

            var roles = await _roleRepository.Select.ToListAsync(a => new { a.Id, a.Name });

            return ResultOutput.Ok(new { Form = Mapper.Map<UserGetOutput>(entity), Select = new { roles } });
        }

        /// <summary>
        /// 查询分页
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public async Task<IResultOutput> GetPageAsync(PageInput input)
        {
            var list = await _userRepository.Select
            .WhereDynamicFilter(input.DynamicFilter)
            .Count(out var total)
            .OrderByDescending(true, a => a.Id)
            .IncludeMany(a => a.Roles.Select(b => new RoleEntity { Name = b.Name }))
            .Page(input.CurrentPage, input.PageSize)
            .ToListAsync();

            var data = new PageOutput<UserListOutput>()
            {
                List = Mapper.Map<List<UserListOutput>>(list),
                Total = total
            };

            return ResultOutput.Ok(data);
        }

        /// <summary>
        /// 查询登录用户信息
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<ResultOutput<AuthLoginOutput>> GetLoginUserAsync(long id)
        {
            var output = new ResultOutput<AuthLoginOutput>();
            var entityDto = await _userRepository.Select.DisableGlobalFilter("Tenant").WhereDynamic(id).ToOneAsync<AuthLoginOutput>();
            if (_appConfig.Tenant && entityDto?.TenantId.Value > 0)
            {
                var tenant = await _tenantRepository.Select.DisableGlobalFilter("Tenant").WhereDynamic(entityDto.TenantId).ToOneAsync(a => new { a.TenantType, a.DataIsolationType });
                if (null != tenant)
                {
                    entityDto.TenantType = tenant.TenantType;
                    entityDto.DataIsolationType = tenant.DataIsolationType;
                }
            }
            return output.Ok(entityDto);
        }

        /// <summary>
        /// 查询表单下拉数据
        /// </summary>
        /// <returns></returns>
        public async Task<IResultOutput> GetSelectAsync()
        {
            var roles = await _roleRepository.Select.ToListAsync(a => new { a.Id, a.Name });

            return ResultOutput.Ok(new { Select = new { roles } });
        }

        /// <summary>
        /// 查询用户基本信息
        /// </summary>
        /// <returns></returns>
        public async Task<IResultOutput> GetBasicAsync()
        {
            if (!(User?.Id > 0))
            {
                return ResultOutput.NotOk("未登录！");
            }

            var data = await _userRepository.GetAsync<UserUpdateBasicInput>(User.Id);
            return ResultOutput.Ok(data);
        }

        /// <summary>
        /// 查询用户权限信息
        /// </summary>
        /// <returns></returns>
        public async Task<IList<UserPermissionsOutput>> GetPermissionsAsync()
        {
            var key = string.Format(CacheKey.UserPermissions, User.Id);
            var result = await Cache.GetOrSetAsync(key, async () =>
            {
                return await _apiRepository
                .Where(a => _userRoleRepository.Orm.Select<UserRoleEntity, RolePermissionEntity, PermissionApiEntity>()
                .InnerJoin((b, c, d) => b.RoleId == c.RoleId && b.UserId == User.Id)
                .InnerJoin((b, c, d) => c.PermissionId == d.PermissionId)
                .Where((b, c, d) => d.ApiId == a.Id).Any())
                .ToListAsync<UserPermissionsOutput>();
            });
            return result;
        }

        /// <summary>
        /// 新增
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        [Transaction]
        public async Task<IResultOutput> AddAsync(UserAddInput input)
        {
            if (input.Password.IsNull())
            {
                input.Password = "111111";
            }

            input.Password = MD5Encrypt.Encrypt32(input.Password);

            var entity = Mapper.Map<UserEntity>(input);
            var user = await _userRepository.InsertAsync(entity);

            if (!(user?.Id > 0))
            {
                return ResultOutput.NotOk();
            }

            if (input.RoleIds != null && input.RoleIds.Any())
            {
                var roles = input.RoleIds.Select(a => new UserRoleEntity { UserId = user.Id, RoleId = a });
                await _userRoleRepository.InsertAsync(roles);
            }

            return ResultOutput.Ok();
        }

        /// <summary>
        /// 修改
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        [Transaction]
        public async Task<IResultOutput> UpdateAsync(UserUpdateInput input)
        {
            if (!(input?.Id > 0))
            {
                return ResultOutput.NotOk();
            }

            var user = await _userRepository.GetAsync(input.Id);
            if (!(user?.Id > 0))
            {
                return ResultOutput.NotOk("用户不存在！");
            }

            Mapper.Map(input, user);
            await _userRepository.UpdateAsync(user);

            await _userRoleRepository.DeleteAsync(a => a.UserId == user.Id);

            if (input.RoleIds != null && input.RoleIds.Any())
            {
                var roles = input.RoleIds.Select(a => new UserRoleEntity { UserId = user.Id, RoleId = a });
                await _userRoleRepository.InsertAsync(roles);
            }

            return ResultOutput.Ok();
        }

        /// <summary>
        /// 更新基本信息
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public async Task<IResultOutput> UpdateBasicAsync(UserUpdateBasicInput input)
        {
            var entity = await _userRepository.GetAsync(input.Id);
            entity = Mapper.Map(input, entity);
            var result = (await _userRepository.UpdateAsync(entity)) > 0;

            //清除用户缓存
            await Cache.DelAsync(string.Format(CacheKey.UserInfo, input.Id));

            return ResultOutput.Result(result);
        }

        /// <summary>
        /// 修改密码
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public async Task<IResultOutput> ChangePasswordAsync(UserChangePasswordInput input)
        {
            if (input.ConfirmPassword != input.NewPassword)
            {
                return ResultOutput.NotOk("新密码和确认密码不一致！");
            }

            var entity = await _userRepository.GetAsync(input.Id);
            var oldPassword = MD5Encrypt.Encrypt32(input.OldPassword);
            if (oldPassword != entity.Password)
            {
                return ResultOutput.NotOk("旧密码不正确！");
            }

            input.Password = MD5Encrypt.Encrypt32(input.NewPassword);

            entity = Mapper.Map(input, entity);
            var result = (await _userRepository.UpdateAsync(entity)) > 0;

            return ResultOutput.Result(result);
        }

        /// <summary>
        /// 删除
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<IResultOutput> DeleteAsync(long id)
        {
            var result = false;
            if (id > 0)
            {
                result = (await _userRepository.DeleteAsync(m => m.Id == id)) > 0;
            }

            return ResultOutput.Result(result);
        }

        /// <summary>
        /// 软删除
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [Transaction]
        public async Task<IResultOutput> SoftDeleteAsync(long id)
        {
            var result = await _userRepository.SoftDeleteAsync(id);
            await _userRoleRepository.DeleteAsync(a => a.UserId == id);

            return ResultOutput.Result(result);
        }

        /// <summary>
        /// 批量软删除
        /// </summary>
        /// <param name="ids"></param>
        /// <returns></returns>
        [Transaction]
        public async Task<IResultOutput> BatchSoftDeleteAsync(long[] ids)
        {
            var result = await _userRepository.SoftDeleteAsync(ids);
            await _userRoleRepository.DeleteAsync(a => ids.Contains(a.UserId));

            return ResultOutput.Result(result);
        }
    }
}