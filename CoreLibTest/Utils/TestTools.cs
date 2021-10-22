using CoreLib.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CoreLibTest {
	/// <summary>
	/// Base test class.
	/// </summary>
	[TestClass]
	public sealed class TestTools : BaseTest {
		/// <summary>
		/// Here we do not test mongo methods.
		/// </summary>
		protected override bool IsMongoNeeded { get; set; } = false;

		/// <summary>
		/// Test EnumParse method.
		/// </summary>
		/// <param name="str"></param>
		/// <param name="day"></param>
		[DataTestMethod]
		[DataRow("Tuesday", DayOfWeek.Tuesday)]
		[DataRow("Glablu", DayOfWeek.Sunday)]
		public void EnumsParse(string str, DayOfWeek day) {
			Assert.AreEqual(Tools.EnumParse<DayOfWeek>(str), day);				
		}

		/// <summary>
		/// Test GetListOfEnumValues method.
		/// </summary>
		[TestMethod]
		public void GetListOfEnumValues() {
			List<DayOfWeek> createdList = new List<DayOfWeek> { DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday };
			List<DayOfWeek> computedList = Tools.GetListOfEnumValues<DayOfWeek>();
			
			Assert.IsTrue(createdList.SequenceEqual(computedList));

			computedList = Tools.GetListOfEnumValues(DayOfWeek.Monday, DayOfWeek.Tuesday);
			Assert.IsFalse(createdList.SequenceEqual(computedList));

			createdList = new List<DayOfWeek> { DayOfWeek.Sunday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday };
			Assert.IsTrue(createdList.SequenceEqual(computedList));
		}

		/// <summary>
		/// Test SequencesEqual method.
		/// </summary>
		[TestMethod]
		public void SequencesEqual()
		{
			List<int> intsFirst = new List<int> { 1, 2, 3 };
			List<int> intsSecond = new List<int> { 1, 2, 3 };
			List<int> intsThird = new List<int> { 5, 2 };
			Assert.IsTrue(Tools.SequencesEqual(intsFirst, intsSecond));
			Assert.IsFalse(Tools.SequencesEqual(intsFirst, intsThird));
		}
	};
}
