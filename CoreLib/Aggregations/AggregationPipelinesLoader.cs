using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Serilog;

namespace CoreLib.Aggregations {
	/// <summary>
	/// Used to store the list of aggregation file names.
	/// </summary>
	public sealed class AggregationTypes {
		public static List<string> Types { get; set; }
	};

	/// <summary>
	/// Class that loads all the aggregation pipelines.
	/// </summary>
	public sealed class AggregationPipelinesLoader {
		static readonly ILogger log = Log.ForContext(MethodBase.GetCurrentMethod().DeclaringType);

		/// <summary>
		/// Dictionnary containing all the aggregation pipelines.
		/// </summary>
		public Dictionary<int, List<BsonDocument>> Aggregates { get; set; } = new Dictionary<int, List<BsonDocument>>();

		/// <summary>
		/// Load the aggregation pipeline json files.
		/// </summary>
		/// <param name="basePath">Path of the folder that contains the aggregation files.</param>
		public void Configure(string basePath) {
			for (int i = 0; i < AggregationTypes.Types.Count; ++i) {
				try {
					Aggregates.Add(i, FileToAggregate(basePath, $"{AggregationTypes.Types[i]}.json"));
				}
				catch (Exception e) {
					log.Error($"Failed to load Aggregation Pipeline = [{AggregationTypes.Types[i]}] Json data! Exception = [{e.Message}], Stack = [{e.StackTrace}]");
					throw;
				}
			}
		}

		/// <summary>
		/// Convert json config to BsonDocument[].
		/// </summary>
		/// <param name="basePath">Path of the folder that contains the aggregation files.</param>
		/// <param name="fileName">Name of the aggregation file.</param>
		/// <returns>BsonDocument[]: Array containing all the instructions.</returns>
		static List<BsonDocument> FileToAggregate(string basePath, string fileName) {
			// Read all lines to be able to have comments into the Json file
			string[] bsonLines = File.ReadAllLines(Path.Combine(@basePath, fileName)).Where(x => x != null && !x.TrimStart().StartsWith("/") && !x.TrimStart().StartsWith("*")).ToArray();
			string bson = string.Empty;

			for (int i = 0; i < bsonLines.Length; ++i) {
				if (bsonLines[i].IndexOf('/') != 0)
					bson += bsonLines[i].Split('/')[0];
				else
					bson += bsonLines[i];
			}

			return BsonSerializer.Deserialize<List<BsonDocument>>(bson);
		}
	};
}
