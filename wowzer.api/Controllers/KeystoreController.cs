using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Mvc;

using wowzer.api.Services;

namespace wowzer.api.Controllers
{
    /// <summary>
    /// Displays weather information.
    /// </summary>
    [Route("keystore/"), ApiController, Produces("application/json")]
    public class KeystoreController : ControllerBase
    {
        private IKeyStore KeyStore { get; }
        private ILogger<KeystoreController> Logger { get; }

        private delegate object PropertyGetterDelegate(KeyRecord record);
        private Dictionary<string, PropertyGetterDelegate> _propertyGetters = new(StringComparer.CurrentCultureIgnoreCase);

        public KeystoreController(IKeyStore keyStore, ILogger<KeystoreController> logger) : base()
        {
            KeyStore = keyStore;
            Logger = logger;

            foreach (var property in typeof(KeyRecord).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = property.GetCustomAttribute<JsonIgnoreAttribute>();
                if (attr != null && attr.Condition == JsonIgnoreCondition.Always)
                    continue;

                var getter = property.GetGetMethod();
                if (getter == null)
                    continue;

                var arg = Expression.Parameter(typeof(KeyRecord));
                var call = Expression.Call(arg, getter);
                var adaptedCall = Expression.Convert(call, typeof(object));
                var lambda = Expression.Lambda<PropertyGetterDelegate>(adaptedCall, arg).Compile();
                // var del = (PropertyGetterDelegate) Delegate.CreateDelegate(typeof(PropertyGetterDelegate), getter);
                _propertyGetters.Add(property.Name, lambda);
            }
        }

        /// <summary>
        /// Obtains a summary of all known keys in the keystore.
        /// </summary>
        /// <returns></returns>
        [Route("summary"), HttpGet]
        public IEnumerable<KeyRecord> Summary() => KeyStore.Records;

        /// <summary>
        /// Retrieves information about a specific key, given its ID.
        /// </summary>
        /// <param name="id">The ID of the key to look for.</param>
        /// <returns></returns>
        /// <response code="200">If a key was found for the given ID.</response>
        /// <response code="404">If no key can be found for the given ID.</response>
        [Route("id/{id}"), HttpGet]
        public ActionResult<KeyRecord> GetRecord(int id)
        {
            var record = KeyStore.TryGetRecord(id);
            if (record != null)
                return Ok(record);

            return NotFound();
        }

        /// <summary>
        /// Retrieves the value of a property of a specific key, given its ID.
        /// </summary>
        /// <param name="id">The ID of the key to look for.</param>
        /// <param name="property">The property to retrieve</param>
        /// <returns></returns>
        /// <response code="200">If a key was found for the given ID.</response>
        /// <response code="204">If a key was found for the given ID but no value was set for the property queried.</response>
        /// <response code="404">If no key can be found for the given ID or if the property does not exist.</response>
        [Route("id/{id}/{property}"), HttpGet]
        public ActionResult<object> GetRecordProperty(int id, string property)
        {
            var record = KeyStore.TryGetRecord(id);
            if (record != null && _propertyGetters.TryGetValue(property, out var getter))
                return Ok(getter(record));

            return NotFound();
        }
    }
}
