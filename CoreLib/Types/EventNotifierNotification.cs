using System;

namespace CoreLib.Types {
	/// <summary>
	/// Event operation
	/// </summary>
	public enum EventNotifierOperation {
		Create,
		Update,
		Delete
	};

	/// <summary>
	/// Describe an event notification generic object
	/// </summary>
	public class EventNotifierNotification<T> where T : BaseType {
		public DateTime SubmitDate { get; set; } = DateTime.Now;
		public EventNotifierOperation Operation { get; set; }
		public T Data { get; set; }
	};
}
