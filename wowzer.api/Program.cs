
namespace wowzer.api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(builder => {
                    builder.ConfigureKestrel(serverOptions => {
                        serverOptions.Limits.MaxConcurrentConnections = 500;
                        serverOptions.Limits.MaxConcurrentUpgradedConnections = 500;
                    }).UseStartup<Startup>();
                })
                .Build()
                .Run();
        }
    }
}
