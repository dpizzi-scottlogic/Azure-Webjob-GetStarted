using System.Data.Entity;

namespace Webjob_GetStarted_Common
{
    public class ContosoAdsContext : DbContext
    {
        public ContosoAdsContext() : base("name=ContosoAdsContext")
        {
        }

        public ContosoAdsContext(string connString) : base(connString)
        {
        }

        public DbSet<Ad> Ads { get; set; }
    
    }
}
