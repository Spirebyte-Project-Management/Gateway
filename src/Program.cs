using System.Threading.Tasks;
using Convey.Logging;
using Convey.Secrets.Vault;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Spirebyte.APIGateway;

public class Program
{
    public static Task Main(string[] args)
    {
        return CreateHostBuilder(args).Build().RunAsync();
    }

    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>())
            .UseLogging()
            .UseVault();
    }
}