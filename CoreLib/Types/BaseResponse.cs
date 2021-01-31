namespace CoreLib.Types {
	/// <summary>
	/// Base response info.
	/// </summary>
	public class BaseResponse {
		/// <summary>
		/// If success.
		/// </summary>
		public bool Success { get; set; }
		/// <summary>
		/// Error code.
		/// </summary>
		public int Rescode { get; set; }
		/// <summary>
		/// Error message.
		/// </summary>
		public string Message { get; set; }
	};
}
