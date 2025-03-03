using System.IO;
using Autofac.Extensions.DependencyInjection;
using Coupon.API.Extensions;
using Coupon.API.Infrastructure;
using Coupon.API.Infrastructure.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace Coupon.API
{
    public class Program
    {
        public static void Main(string[] args) =>
            CreateHostBuilder(args)
                .Build()
                .SeedDatabaseStrategy<CouponContext>(context => new CouponSeed().SeedAsync(context).Wait())
                .SubscribersIntegrationEvents()
                .Run();

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseServiceProviderFactory(new AutofacServiceProviderFactory())
                .ConfigureAppConfiguration((host, builder) =>
                {
                    builder.SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                        .AddJsonFile($"appsettings.{host.HostingEnvironment.EnvironmentName}.json", optional: false, reloadOnChange: true)
                        .AddEnvironmentVariables();

                    var config = builder.Build();

                    if (config.GetValue("UseVault", false))
                    {
                        builder.AddAzureKeyVault($"https://{config["Vault:Name"]}.vault.azure.net/", config["Vault:ClientId"], config["Vault:ClientSecret"]);
                    }
                })
                .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>())
                .UseSerilog((host, builder) =>
                {
                    var seqServerUrl = host.Configuration["Serilog:SeqServerUrl"];
                    var logstashUrl = host.Configuration["Serilog:LogstashUrl"];

                    builder.MinimumLevel.Verbose()
                        .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                        .Enrich.WithProperty("ApplicationContext", host.HostingEnvironment.ApplicationName)
                        .Enrich.FromLogContext()
                        .WriteTo.Console()
                        .WriteTo.Seq(string.IsNullOrWhiteSpace(seqServerUrl) ? "http://seq" : seqServerUrl)
                        .WriteTo.Http(string.IsNullOrWhiteSpace(logstashUrl) ? "http://logstash:8080" : logstashUrl)
                        .ReadFrom.Configuration(host.Configuration);
                });
    }
}
