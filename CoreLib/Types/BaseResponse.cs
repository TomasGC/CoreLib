using MongoDB.Bson;

namespace CoreLib.Types {
    /// <summary>
    /// Base Response inherited.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class BaseResponse<T> where T : BaseResponse<T>, new() {
        static readonly string badAuthMessage = "Bad credentials.";

        /// <summary>
        /// Success.
        /// </summary>
        public bool Success { get; set; } = true;
        /// <summary>
        /// Error code.
        /// </summary>
        public long ErrorCode { get; set; }
        /// <summary>
        /// Error message.
        /// </summary>
        public string Message { get; set; }
        /// <summary>
        /// Used to return any Id (for creation or other).
        /// </summary>
        public ObjectId ObjectId { get; set; }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public BaseResponse() { }

        /// <summary>
        /// Bad authentication.
        /// </summary>
        /// <returns></returns>
        public static T BadAuth() {
            T response = new T();
            response.Success = false;
            response.ErrorCode = -400;
            response.Message = badAuthMessage;
            return response;
        }

        /// <summary>
        /// Exception.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public static T Exception(string message) {
            T response = new T();
            response.Success = false;
            response.ErrorCode = -401;
            response.Message = message?.Trim();
            return response;
        }

        /// <summary>
        /// Failure.
        /// </summary>
        /// <param name="errorCode"></param>
        /// <param name="message"></param>
        /// <param name="objectId"></param>
        /// <returns></returns>
        public static T Fail(long errorCode, string message, ObjectId? objectId = null) {
            T response = new T();
            response.Success = false;
            response.ErrorCode = errorCode;
            response.Message = message?.Trim();
            response.ObjectId = objectId.HasValue ? objectId.Value : ObjectId.Empty;
            return response;
        }
    };

    /// <summary>
    /// Base Response.
    /// Used for success.
    /// </summary>
    public sealed class BaseResponse : BaseResponse<BaseResponse> {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public BaseResponse() { }

        /// <summary>
        /// Success constructor.
        /// </summary>
        /// <param name="objectId"></param>
        /// <param name="message"></param>
        public BaseResponse(ObjectId objectId, string message = null) {
            ObjectId = objectId;
            Message = message?.Trim();
        }

        /// <summary>
        /// Success constructor.
        /// </summary>
        /// <param name="errorCode"></param>
        /// <param name="message"></param>
        /// <param name="objectId"></param>
        public BaseResponse(long errorCode, string message, ObjectId objectId) {
            ErrorCode = errorCode;
            Message = message?.Trim();
            ObjectId = objectId;
        }
    };
}
