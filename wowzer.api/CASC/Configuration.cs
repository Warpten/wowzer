using wowzer.api.Blizzard;

namespace wowzer.api.CASC
{
    public class Configuration
    {
        private Dictionary<string, string> _configuration = [];

        private Configuration(IResource resource)
        {
            while (true)
            {
                var line = resource.ReadLine();
            }
        }
    }
}
