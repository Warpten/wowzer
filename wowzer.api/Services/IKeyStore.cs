using System.Text.Json.Serialization;

namespace wowzer.api.Services
{
    public interface IKeyStore
    {
        public IEnumerable<KeyRecord> Records { get; }

        public KeyRecord? TryGetRecord(int id);
    }

    /// <summary>
    /// Describes a CASC key.
    /// </summary>
    public record KeyRecord
    {
        /// <summary>
        /// The ID of this key.
        /// </summary>
        public int ID { get; init; }

        /// <summary>
        /// The actual key.
        /// </summary>
        public ulong Key { get; init; }

        /// <summary>
        /// A user-defined description of the key. This value can be null, in which case it will not be serialized.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Description { get; init; } = null;
    }
}
