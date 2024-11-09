using wowzer.fs.Utils;

namespace wowzer.fs.Extensions
{
    public static class EnumExtensions
    {
        public static Ordering ToOrdering(this int comparison)
        {
            return comparison switch
            {
                > 0 => Ordering.Greater,
                < 0 => Ordering.Less,
                0 => Ordering.Equal,
            };
        }
    }
}
