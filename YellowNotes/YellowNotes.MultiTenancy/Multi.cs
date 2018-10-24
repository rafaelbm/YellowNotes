using System;
using System.Data.Entity;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Web.Http;
using System.Web.Http.Controllers;

namespace YellowNotes.MultiTenancy
{

    public class CustomAuthorizeAttribute : AuthorizeAttribute
    {
        private static Type _tenantRequestContextType = typeof(ITenantRequestContext);

        protected override bool IsAuthorized(HttpActionContext actionContext)
        {
            if (!base.IsAuthorized(actionContext))
                return false;

            var tenantRequestContext = actionContext.Request.GetDependencyScope()
                .GetService(_tenantRequestContextType) as ITenantRequestContext;

            var claimsPrincipal = actionContext.RequestContext.Principal.Identity as ClaimsPrincipal;
            var tenantName = claimsPrincipal.FindFirst(MyClaimTypes.TenantId).Value;

            //move that resolve logig inside tenantRequestContext
            tenantRequestContext.SetTenantName(tenantName);

            return true;
        }
    }

    public interface ITenantRequestContext
    {
        string TenantName { get; }

        void SetTenantName(string tenantName);
    }

    public class TenantRequestContext : ITenantRequestContext
    {
        public string TenantName { get; private set; }

        public void SetTenantName(string tenantName)
        {
            TenantName = tenantName;
        }
    }

    public class MyClaimTypes
    {
        public static string TenantId { get; } = "TenantName";
    }

    public class DbContextFactory
    {
        private ITenantRequestContext _tenantRequestContext;
        private ITenantStore _tenantStore;

        public DbContextFactory(ITenantRequestContext tenantRequestContext, ITenantStore tenantStore)
        {
            _tenantRequestContext = tenantRequestContext;
            _tenantStore = tenantStore;
        }

        public DbContext CreateDbContext<TDbContext>() where TDbContext : DbContext, new()
        {
            if (string.IsNullOrWhiteSpace(_tenantRequestContext.TenantName))
                return new TDbContext();

            var tenant = _tenantStore.Find(_tenantRequestContext.TenantName);
            if (tenant == null)
                return new TDbContext();

            return Activator.CreateInstance(typeof(TDbContext), tenant.ConnectionString) as TDbContext;
        }
    }

    public interface ITenantStore
    {
        TenantInfo Find(string tenantName);
    }

    public class TenantStore : ITenantStore
    {
        private TenantDbContext _context;

        public TenantStore(TenantDbContext context)
        {
            _context = context;
        }

        public TenantInfo Find(string tenantName)
        {
            return _context.Tenants
                .AsNoTracking()
                .FirstOrDefault(x => x.TenantName.Equals(tenantName, StringComparison.OrdinalIgnoreCase));
        }
    }

    public class TenantInfo
    {
        public int Id { get; set; }
        public string TenantName { get; set; }
        public string ConnectionString { get; set; }
    }

    public class TenantDbContext : DbContext
    {
        public DbSet<TenantInfo> Tenants { get; set; }

        public TenantDbContext()
        {
            Configuration.AutoDetectChangesEnabled = false;
            Configuration.LazyLoadingEnabled = false;
            Configuration.ProxyCreationEnabled = false;
        }
    }

    public class PerTenantDbContext : DbContext
    {

    }
}
