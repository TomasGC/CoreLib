using MongoDB.Bson;

namespace CoreLib.Types {
	public class BaseRequest {
	};

	public sealed class DeleteRequest : BaseRequest	{
		public ObjectId id { get; set; }
	}
}
