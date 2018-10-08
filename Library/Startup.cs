
using LibraryData;
using LibraryServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Library
{
    public class Startup
    {
        public Startup(IConfiguration config)
        {
            Configuration = config;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
            services.AddSingleton(Configuration);
            services.AddScoped<ILibraryAsset, LibraryAssetService>();
           // services.AddScoped<ILibraryCardService, LibraryCardService>();
            services.AddScoped<ILibraryBranch, LibraryBranchService>();
            services.AddScoped<IPatron, PatronService>();
            services.AddScoped<ICheckout, CheckoutService>();
           // services.AddScoped<ILibraryAssetService, LibraryAssetService>();
           // services.AddScoped<IBookService, BookService>();
           // services.AddScoped<IVideoService, VideoService>();
          //  services.AddScoped<IStatusService, StatusService>();

            services.AddDbContext<LibraryDbContext>(options =>
                options.UseSqlServer(Configuration.GetConnectionString("LibraryConnection")));
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseBrowserLink();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    "default",
                    "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}