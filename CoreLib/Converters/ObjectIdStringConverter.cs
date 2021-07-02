using MongoDB.Bson;
using Newtonsoft.Json;
using System;

namespace CoreLib.Converters {
    /// <summary>
    /// Converter from ObjectId to string and vice versa.
    /// </summary>
    public sealed class ObjectIdStringConverter : JsonConverter {
        /// <summary>
        /// Verify if object is an ObjectId.
        /// </summary>
        /// <param name="objectType"></param>
        /// <returns></returns>
		public override bool CanConvert(Type objectType) => objectType == typeof(ObjectId);

        /// <summary>
        /// Convert from string to ObjectId.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="objectType"></param>
        /// <param name="existingValue"></param>
        /// <param name="serializer"></param>
        /// <returns></returns>
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            if (reader.TokenType != JsonToken.String)
                throw new Exception($"Unexpected token parsing ObjectId. Expected String, got {reader.TokenType}.");

            var value = reader.Value as string;
            return string.IsNullOrEmpty(value) ? ObjectId.Empty : new ObjectId(value);
        }

        /// <summary>
        /// Convert from ObjectId to string.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="value"></param>
        /// <param name="serializer"></param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            if (value is ObjectId) {
                var objectId = (ObjectId)value;
                writer.WriteValue(objectId != ObjectId.Empty ? objectId.ToString() : string.Empty);
            }
            else
                throw new Exception("Expected ObjectId value.");
        }
    };
}
