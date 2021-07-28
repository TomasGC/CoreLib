using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using Serilog;
using System.Reflection;

namespace CoreLib.Types {
    /// <summary>
    /// The base type, only contains the id.
    /// </summary>
    public class BaseType {
        #region Consts
        /// <summary>
        /// Logger.
        /// </summary>
        [BsonIgnore]
        protected static readonly ILogger log = Log.ForContext(MethodBase.GetCurrentMethod().DeclaringType);
        #endregion

        /// <summary>
        /// Id of the object.
        /// </summary>
        [BsonId]
        public ObjectId _id { get; set; } = ObjectId.Empty;

        #region Dynamic Getters
        /// <summary>
        /// Verify if the item is new or not.
        /// </summary>
        [BsonIgnore]
        [JsonIgnore]
        public bool IsNew { get => _id == ObjectId.Empty; }
        #endregion

        /// <summary>
        /// Set id by default.
        /// </summary>
        public void SetNew() => _id = ObjectId.Empty;
    };

    /// <summary>
    /// Used when we have to serialize properties in MongoDB.
    /// </summary>
    public abstract class SerializationBaseType : BaseType {
        /// <summary>
        /// Serialization abstract method.
        /// </summary>
        public abstract void Serialization();
    };
}
