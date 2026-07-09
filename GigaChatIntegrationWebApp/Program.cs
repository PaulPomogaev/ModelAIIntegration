using ModelAIIntegrationCore.GigaChat;
using ModelAIIntegrationCore.Knowledge;
using ModelAIIntegrationCore.Tutor;
using Microsoft.Extensions.DependencyInjection;

namespace ModelAIIntegrationWebApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddSingleton<LanguageModelRegistry>();

            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(50);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            }); 

            builder.Services.AddHttpContextAccessor();

            // Add services to the container.
            builder.Services.AddControllersWithViews();

            var app = builder.Build();
                        
            app.UseHttpsRedirection();
            app.UseRouting();
            app.UseSession();

            app.MapStaticAssets();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Chat}/{action=Index}/{id?}")
                .WithStaticAssets();

            app.Run();
        }
    }
}
