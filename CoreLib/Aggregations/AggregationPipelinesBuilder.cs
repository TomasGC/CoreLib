using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using CoreLib.Types;
using CoreLib.Utils;

namespace CoreLib.Managers {
	/// <summary>
	/// This class allows to add different conditions/checks for matches in mongo aggregations/searches.
	/// </summary>
	public static class Operations {
		#region Consts
		static readonly string inArray = "$in";
		static readonly string notInArray = "$nin";
		static readonly string and = "$and";
		static readonly string or = "$or";
		static readonly string ne = "$ne";
		static readonly string all = "$all";
		static readonly string gt = "$gt";
		static readonly string gte = "$gte";
		static readonly string lt = "$lt";
		static readonly string lte = "$lte";
		static readonly string toLower = "$toLower";
		static readonly string not = "$not";
		static readonly string cond = "$cond";
		static readonly string eq = "$eq";
		static readonly string ifNull = "$ifNull";
		static readonly string arrayElemAt = "$arrayElemAt";
		static readonly string expr = "$expr";
		static readonly string elemMatch = "$elemMatch";
		static readonly string size = "$size";
		#endregion

		/// <summary>
		/// Merge multiple conditions with AND.
		/// </summary>
		/// <param name="documents">Condition for the existance in the collection.</param>
		/// <returns></returns>
		public static BsonDocument And(params BsonDocument[] documents) => new BsonDocument(and, new BsonArray(documents));

		/// <summary>
		/// Merge multiple conditions with AND.
		/// </summary>
		/// <param name="documents">Condition for the existance in the collection.</param>
		/// <returns></returns>
		public static BsonDocument And(List<BsonDocument> documents) => new BsonDocument(and, new BsonArray(documents));

		/// <summary>
		/// Merge multiple conditions with OR.
		/// </summary>
		/// <param name="documents">Condition for the existance in the collection.</param>
		/// <returns></returns>
		public static BsonDocument Or(params BsonDocument[] documents) => new BsonDocument(or, new BsonArray(documents));

		/// <summary>
		/// Merge multiple conditions with AND.
		/// </summary>
		/// <param name="documents">Condition for the existance in the collection.</param>
		/// <returns></returns>
		public static BsonDocument Or(List<BsonDocument> documents) => new BsonDocument(or, new BsonArray(documents));

		/// <summary>
		/// Verify that the value is IN the array.
		/// </summary>
		/// <param name="value">Value to replace with.</param>
		/// <param name="values">Condition for the existance in the collection.</param>
		/// <returns></returns>
		public static BsonElement In<T>(string propertyName, IEnumerable<T> values) => new BsonElement(propertyName, new BsonDocument(inArray, new BsonArray(values)));

		/// <summary>
		/// Verify that the value is NOT IN the array.
		/// </summary>
		/// <param name="value">Value to replace with.</param>
		/// <param name="values">Condition for the existance in the collection.</param>
		/// <returns></returns>
		public static BsonElement NIn<T>(string propertyName, IEnumerable<T> values) => new BsonElement(propertyName, new BsonDocument(notInArray, new BsonArray(values)));

		/// <summary>
		/// Verify that the property is not equal to the value.
		/// </summary>
		/// <param name="value">Value to replace with.</param>
		/// <param name="value">Value to replace with.</param>
		/// <returns></returns>
		public static BsonElement NE(string propertyName, BsonValue value) => new BsonElement(propertyName, new BsonDocument(ne, value));

		/// <summary>
		/// Verify that the property is completely equal to all the values.
		/// </summary>
		/// <param name="value">Value to replace with.</param>
		/// <param name="values">Condition for the existance in the collection.</param>
		/// <returns></returns>
		public static BsonDocument All<T>(string propertyName, IEnumerable<T> values) => new BsonDocument(propertyName, new BsonDocument(all, new BsonArray(values)));

		/// <summary>
		/// Verify that the property is equal to the value.
		/// </summary>
		/// <param name="value">Value to replace with.</param>
		/// <param name="value">Value to replace with.</param>
		/// <returns></returns>
		public static BsonElement Eq(string propertyName, BsonValue value) => new BsonElement(propertyName, value is BsonString ? new BsonRegularExpression($"^{value.AsString}$", "i") : value);

		/// <summary>
		/// Verify that the property is greater than to the value.
		/// </summary>
		/// <param name="value">Value to replace with.</param>
		/// <param name="value">Value to replace with.</param>
		/// <returns></returns>
		public static BsonElement Gt(string propertyName, BsonValue value) => new BsonElement(propertyName, new BsonDocument(gt, value));

		/// <summary>
		/// Verify that the property is greater than or equal to the value.
		/// </summary>
		/// <param name="value">Value to replace with.</param>
		/// <param name="value">Value to replace with.</param>
		/// <returns></returns>
		public static BsonElement Gte(string propertyName, BsonValue value) => new BsonElement(propertyName, new BsonDocument(gte, value));

		/// <summary>
		/// Verify that the property is lower than to the value.
		/// </summary>
		/// <param name="value">Value to replace with.</param>
		/// <param name="value">Value to replace with.</param>
		/// <returns></returns>
		public static BsonElement Lt(string propertyName, BsonValue value) => new BsonElement(propertyName, new BsonDocument(lt, value));

		/// <summary>
		/// Verify that the property is lower than or equal to the value.
		/// </summary>
		/// <param name="value">Value to replace with.</param>
		/// <param name="value">Value to replace with.</param>
		/// <returns></returns>
		public static BsonElement Lte(string propertyName, BsonValue value) => new BsonElement(propertyName, new BsonDocument(lte, value));

		/// <summary>
		/// Convert string to lower
		/// </summary>
		/// <param name="value">Value to replace with.</param>
		/// <returns></returns>
		public static BsonDocument ToLower(string propertyName) => new BsonDocument(propertyName, new BsonDocument(toLower, $"${propertyName}"));

		/// <summary>
		/// Verify that the property contains the string value.
		/// </summary>
		/// <param name="value">Value to replace with.</param>
		/// <param name="value">Value to replace with.</param>
		/// <param name="caseSensitive">Matching is case sensitive.</param>
		/// <param name="diacriticSensitive">Matching is diacritic sensitive.</param>
		/// <returns></returns>
		public static BsonElement Contains(string propertyName, BsonValue value, bool caseSensitive = false, bool diacriticSensitive = false) => new BsonElement(propertyName, new BsonRegularExpression($"{(!diacriticSensitive ? Tools.GetDiacriticInsensitiveRegex(value.ToString()) : value)}", !caseSensitive ? "i" : string.Empty));

		/// <summary>
		/// Verify that the property does not contain the stringvalue.
		/// </summary>
		/// <param name="value">Value to replace with.</param>
		/// <param name="value">Value to replace with.</param>
		/// <returns></returns>
		public static BsonElement NContains(string propertyName, BsonValue value, bool caseInsensitive = false) => new BsonElement(propertyName, new BsonDocument(not, new BsonRegularExpression($".*{value}.*", caseInsensitive ? "i" : string.Empty)));

		/// <summary>
		/// Evaluates a boolean expression to return one of the two specified return expressions.
		/// </summary>
		/// <param name="ifExpression"></param>
		/// <param name="thenExpression"></param>
		/// <param name="elseExpression"></param>
		/// <returns></returns>
		public static BsonDocument Cond(BsonValue ifExpression, BsonValue thenExpression, BsonValue elseExpression) => new BsonDocument(cond, new BsonArray(new List<BsonValue> { ifExpression, thenExpression, elseExpression }));

		/// <summary>
		/// Evaluates an expression and returns the value of the expression if the expression evaluates to a non-null value.
		/// If the expression evaluates to a null value, including instances of undefined values or missing fields, returns the value of the replacement expression.
		/// </summary>
		/// <param name="expression"></param>
		/// <param name="replacementExpressionIfNull"></param>
		/// <returns></returns>
		public static BsonDocument IfNull(BsonValue expression, BsonValue replacementExpressionIfNull) => new BsonDocument(ifNull, new BsonArray(new List<BsonValue> { expression, replacementExpressionIfNull }));

		/// <summary>
		/// evaluate (array) length of specified expression.
		/// </summary>
		/// <param name="expression"></param>
		/// <returns></returns>
		public static BsonDocument Size(BsonDocument expression) => new BsonDocument(size, expression);

		/// <summary>
		/// Returns the element at the specified array index.
		/// </summary>
		/// <param name="array"></param>
		/// <param name="index"></param>
		/// <returns></returns>
		public static BsonDocument ArrayElemAt(string array, int index) => new BsonDocument(arrayElemAt, new BsonArray(new List<BsonValue> { array, index }));

		/// <summary>
		/// Allows the use of aggregation expressions within the query language.
		/// </summary>
		/// <param name="expression"></param>
		/// <returns></returns>
		public static BsonDocument Expr(BsonDocument expression) => new BsonDocument(expr, expression);

		/// <summary>
		/// Compares two values and returns:
		/// - true when the values are equivalent.
		/// - false when the values are not equivalent.
		/// 
		/// The $eq compares both value and type, using the specified BSON comparison order for values of different types.
		/// </summary>
		/// <param name="expression1"></param>
		/// <param name="expression2"></param>
		/// <returns></returns>
		public static BsonDocument EqInAggregation(BsonValue expression1, BsonValue expression2) => new BsonDocument(eq, new BsonArray(new List<BsonValue> { expression1, expression2 }));

		/// <summary>
		/// The $elemMatch operator matches documents that contain an array field with at least one element that matches all the specified query criteria.
		/// Note:
		/// - You cannot specify a $where expression in an $elemMatch.
		/// - You cannot specify a $text query expression in an $elemMatch.
		/// </summary>
		/// <param name="array">The array name from which we want to match an element.</param>
		/// <param name="queries">The queries used to match an element from specified @array.</param>
		/// <returns></returns>
		public static BsonElement ElemMatch(string array, List<BsonElement> queries) => new BsonElement(array, new BsonDocument(elemMatch, new BsonDocument(queries)));
	};

	/// <summary>
	/// Helper to create an aggregation pipeline.
	/// </summary>
	public static class AggregationPipelinesBuilder {
		static readonly ILogger log = Log.ForContext(MethodBase.GetCurrentMethod().DeclaringType);

		#region Consts
		static readonly string match = "$match";
		static readonly string limit = "$limit";
		static readonly string skip = "$skip";
		static readonly string sort = "$sort";
		static readonly string lookup = "$lookup";
		static readonly string lookupFrom = "from";
		static readonly string lookupLocalField = "localField";
		static readonly string lookupForeignField = "foreignField";
		static readonly string lookupAs = "as";
		static readonly string lookupPipeline = "pipeline";
		static readonly string lookupLet = "let";
		static readonly string unwind = "$unwind";
		static readonly string unwindPath = "path";
		static readonly string unwindPreserveNull = "preserveNullAndEmptyArrays";
		static readonly string set = "$set";
		static readonly string unset = "$unset";
		#endregion

		/// <summary>
		/// Display the json of the aggregation.
		/// </summary>
		/// <param name="aggregate">List of steps for the aggregation.</param>
		/// <returns></returns>
		public static List<BsonDocument> Print(this List<BsonDocument> aggregate) {
			Console.WriteLine($"[{aggregate.ToJson()}]");
			return aggregate;
		}

		/// <summary>
		/// Add a document to the pipeline.
		/// </summary>
		/// <param name="aggregate">List of steps for the aggregation.</param>
		/// <param name="document">Document to add in the aggregation.</param>
		/// <returns></returns>
		public static List<BsonDocument> AddDocument(this List<BsonDocument> aggregate, BsonDocument document) {
			if (document == null)
				return aggregate;

			aggregate.Add(document);
			return aggregate;
		}

		/// <summary>
		/// Join a collection to another the current one.
		/// https://docs.mongodb.com/manual/reference/operator/aggregation/lookup/
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aggregate">List of steps for the aggregation.</param>
		/// <param name="localField">Field of the current collection to match with the other collection's field.</param>
		/// <param name="foreignField">Field of the second collection.</param>
		/// <param name="propertyName">Output property name where will be stored the joined collection.</param>
		/// <returns></returns>
		public static List<BsonDocument> Lookup<T>(this List<BsonDocument> aggregate, string localField, string foreignField, string propertyName) where T : BaseType {
			aggregate.Add(new BsonDocument(lookup, new BsonDocument(new List<BsonElement>{
				new BsonElement(lookupFrom, typeof(T).Name),
				new BsonElement(lookupLocalField, localField),
				new BsonElement(lookupForeignField, foreignField),
				new BsonElement(lookupAs, propertyName)
			})));
			return aggregate;
		}

		/// <summary>
		/// Join a collection to another the current one, with some specific additional conditions.
		/// https://docs.mongodb.com/manual/reference/operator/aggregation/lookup/
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aggregate">List of steps for the aggregation.</param>
		/// <param name="asExpression">Output property name where will be stored the joined collection.</param>
		/// <param name="letExpression">Specifies variables to use in the pipeline field stages.</param>
		/// <param name="pipelineExpression">Pipeline to run on the joined collection.</param>
		/// <returns></returns>
		public static List<BsonDocument> Lookup<T>(this List<BsonDocument> aggregate, string asExpression, BsonDocument letExpression, BsonArray pipelineExpression) where T : BaseType {
			aggregate.Add(new BsonDocument(lookup, new BsonDocument(new List<BsonElement>{
				new BsonElement(lookupFrom, typeof(T).Name),
				new BsonElement(lookupAs, asExpression),
				new BsonElement(lookupLet, letExpression),
				new BsonElement(lookupPipeline, pipelineExpression)
			})));
			return aggregate;
		}

		/// <summary>
		/// Deconstructs an array field.
		/// Example: An object with an array/list of N int values will be split into N objects of 1 int value.
		/// https://docs.mongodb.com/manual/reference/operator/aggregation/unwind/
		/// </summary>
		/// <param name="aggregate">List of steps for the aggregation.</param>
		/// <param name="propertyName"></param>
		/// <returns></returns>
		public static List<BsonDocument> Unwind(this List<BsonDocument> aggregate, string propertyName) {
			aggregate.Add(new BsonDocument(unwind, new BsonDocument(new List<BsonElement> { new BsonElement(unwindPath, $"${propertyName}"), new BsonElement(unwindPreserveNull, true) })));
			return aggregate;
		}

		#region Matches
		/// <summary>
		/// This is used to filter the data.
		/// https://docs.mongodb.com/manual/reference/operator/aggregation/match/
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aggregate">List of steps for the aggregation.</param>
		/// <param name="filter">Filter operation to apply on the collection.</param>
		/// <returns></returns>
		public static List<BsonDocument> Match<T>(this List<BsonDocument> aggregate, Expression<Func<T, bool>> filter) where T : BaseType {
			if (filter == null)
				return aggregate;

			IBsonSerializer<T> fileInfoSerializer = MongoManager.SerializerRegistry.GetSerializer<T>();
			aggregate.Add(new BsonDocument(match, Builders<T>.Filter.Where(filter).Render(fileInfoSerializer, MongoManager.SerializerRegistry)));
			return aggregate;
		}

		/// <summary>
		/// This is used to filter the data.
		/// https://docs.mongodb.com/manual/reference/operator/aggregation/match/
		/// </summary>
		/// <param name="aggregate">List of steps for the aggregation.</param>
		/// <param name="propertyName">Name of the field to match in the collection.</param>
		/// <param name="value">Value to match in the collection.</param>
		/// <returns></returns>
		public static List<BsonDocument> Match(this List<BsonDocument> aggregate, string propertyName, bool value) {
			aggregate.Add(new BsonDocument(match, new BsonDocument(propertyName, value)));
			return aggregate;
		}

		/// <summary>
		/// This is used to filter the data.
		/// https://docs.mongodb.com/manual/reference/operator/aggregation/match/
		/// </summary>
		/// <param name="aggregate">List of steps for the aggregation.</param>
		/// <param name="propertyName">Name of the field to match in the collection.</param>
		/// <param name="value">Value to match in the collection.</param>
		/// <returns></returns>
		public static List<BsonDocument> Match(this List<BsonDocument> aggregate, string propertyName, int value) {
			aggregate.Add(new BsonDocument(match, new BsonDocument(propertyName, value)));
			return aggregate;
		}

		/// <summary>
		/// This is used to filter the data.
		/// https://docs.mongodb.com/manual/reference/operator/aggregation/match/
		/// </summary>
		/// <param name="aggregate">List of steps for the aggregation.</param>
		/// <param name="propertyName">Name of the field to match in the collection.</param>
		/// <param name="value">Value to match in the collection.</param>
		/// <returns></returns>
		public static List<BsonDocument> Match(this List<BsonDocument> aggregate, string propertyName, long value) {
			aggregate.Add(new BsonDocument(match, new BsonDocument(propertyName, value)));
			return aggregate;
		}

		/// <summary>
		/// This is used to filter the data.
		/// https://docs.mongodb.com/manual/reference/operator/aggregation/match/
		/// </summary>
		/// <param name="aggregate">List of steps for the aggregation.</param>
		/// <param name="propertyName">Name of the field to match in the collection.</param>
		/// <param name="value">Value to match in the collection.</param>
		/// <returns></returns>
		public static List<BsonDocument> Match(this List<BsonDocument> aggregate, string propertyName, string value) {
			if (string.IsNullOrEmpty(value))
				return aggregate;

			aggregate.Add(new BsonDocument(match, new BsonDocument(propertyName, value)));
			return aggregate;
		}

		/// <summary>
		/// This is used to filter the data.
		/// https://docs.mongodb.com/manual/reference/operator/aggregation/match/
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aggregate">List of steps for the aggregation.</param>
		/// <param name="propertyName">Name of the field to match in the collection.</param>
		/// <param name="value">Value to match in the collection.</param>
		/// <returns></returns>
		public static List<BsonDocument> Match<T>(this List<BsonDocument> aggregate, string propertyName, T value) where T : Enum {
			aggregate.Add(new BsonDocument(match, new BsonDocument(propertyName, value)));
			return aggregate;
		}

		/// <summary>
		/// This is used to filter the data.
		/// https://docs.mongodb.com/manual/reference/operator/aggregation/match/
		/// </summary>
		/// <param name="aggregate">List of steps for the aggregation.</param>
		/// <param name="conditions">Conditions for the existance in the collection.</param>
		/// <returns></returns>
		public static List<BsonDocument> Match(this List<BsonDocument> aggregate, BsonDocument conditions) {
			aggregate.Add(new BsonDocument(match, conditions));
			return aggregate;
		}

		/// <summary>
		/// This is used to filter the data.
		/// https://docs.mongodb.com/manual/reference/operator/aggregation/match/
		/// </summary>
		/// <param name="aggregate">List of steps for the aggregation.</param>
		/// <param name="conditions">Conditions for the existance in the collection.</param>
		/// <returns></returns>
		public static List<BsonDocument> Match(this List<BsonDocument> aggregate, BsonArray conditions) {
			aggregate.Add(new BsonDocument(match, conditions));
			return aggregate;
		}
		#endregion

		/// <summary>
		/// Limit the number of document to retrieve.
		/// https://docs.mongodb.com/manual/reference/operator/aggregation/limit/
		/// </summary>
		/// <param name="aggregate">List of steps for the aggregation.</param>
		/// <param name="limitation">How many documents we want to retrieve.</param>
		/// <returns></returns>
		public static List<BsonDocument> Limit(this List<BsonDocument> aggregate, int limitation) {
			aggregate.Add(new BsonDocument(limit, limitation));
			return aggregate;
		}

		/// <summary>
		/// Skip the N first documents.
		/// https://docs.mongodb.com/manual/reference/operator/aggregation/skip/
		/// </summary>
		/// <param name="aggregate">List of steps for the aggregation.</param>
		/// <param name="nbDocuments">Number of documents to skip.</param>
		/// <returns></returns>
		public static List<BsonDocument> Skip(this List<BsonDocument> aggregate, int nbDocuments) {
			aggregate.Add(new BsonDocument(skip, nbDocuments));
			return aggregate;
		}

		/// <summary>
		/// Sort ascending the collection.
		/// https://docs.mongodb.com/manual/reference/operator/aggregation/sort/
		/// </summary>
		/// <param name="aggregate">List of steps for the aggregation.</param>
		/// <param name="propertyName">Name of the field to sort the collection with.</param>
		/// <returns></returns>
		public static List<BsonDocument> SortAsc(this List<BsonDocument> aggregate, string propertyName) {
			aggregate.Add(new BsonDocument(sort, new BsonDocument(propertyName, 1)));
			return aggregate;
		}

		/// <summary>
		/// Sort ascending the collection.
		/// https://docs.mongodb.com/manual/reference/operator/aggregation/sort/
		/// </summary>
		/// <param name="aggregate">List of steps for the aggregation.</param>
		/// <param name="propertyNames">Name of the fields to sort the collection with.</param>
		/// <returns></returns>
		public static List<BsonDocument> SortAsc(this List<BsonDocument> aggregate, IEnumerable<string> propertyNames) {
			aggregate.Add(new BsonDocument(sort, new BsonDocument(propertyNames.Select(propertyName => new KeyValuePair<string, object>(propertyName, 1)))));
			return aggregate;
		}

		/// <summary>
		/// Sort descending the collection.
		/// https://docs.mongodb.com/manual/reference/operator/aggregation/sort/
		/// </summary>
		/// <param name="aggregate">List of steps for the aggregation.</param>
		/// <param name="propertyName">Name of the field to sort the collection with.</param>
		/// <returns></returns>
		public static List<BsonDocument> SortDesc(this List<BsonDocument> aggregate, string propertyName) {
			aggregate.Add(new BsonDocument(sort, new BsonDocument(propertyName, -1)));
			return aggregate;
		}

		/// <summary>
		/// Sort descending the collection.
		/// https://docs.mongodb.com/manual/reference/operator/aggregation/sort/
		/// </summary>
		/// <param name="aggregate">List of steps for the aggregation.</param>
		/// <param name="propertyNames">Name of the fields to sort the collection with.</param>
		/// <returns></returns>
		public static List<BsonDocument> SortDesc(this List<BsonDocument> aggregate, IEnumerable<string> propertyNames) {
			aggregate.Add(new BsonDocument(sort, new BsonDocument(propertyNames.Select(propertyName => new KeyValuePair<string, object>(propertyName, 1)))));
			return aggregate;
		}

		/// <summary>
		/// Adds new fields to documents. Can also be used to update the value of an existing field.
		/// https://docs.mongodb.com/manual/reference/operator/aggregation/set/
		/// </summary>
		/// <param name="aggregate">List of steps for the aggregation.</param>
		/// <param name="propertyName">Name of the field to add/update.</param>
		/// <param name="value">Value of the (new) field.</param>
		/// <returns></returns>
		public static List<BsonDocument> Set(this List<BsonDocument> aggregate, string propertyName, BsonValue value) {
			aggregate.Add(new BsonDocument(set, new BsonDocument(propertyName, BsonValue.Create(value))));
			return aggregate;
		}

		/// <summary>
		/// Removes/excludes fields from documents.
		/// https://docs.mongodb.com/manual/reference/operator/aggregation/unset/
		/// </summary>
		/// <param name="aggregate">List of steps for the aggregation.</param>
		/// <param name="propertyName">Name of the field to remove.</param>
		/// <returns></returns>
		public static List<BsonDocument> Unset(this List<BsonDocument> aggregate, string propertyName) {
			aggregate.Add(new BsonDocument(unset, propertyName));
			return aggregate;
		}

		/// <summary>
		/// Add a pipeline from a Json file to the current pipeline.
		/// </summary>
		/// <param name="aggregate">List of steps for the aggregation.</param>
		/// <param name="index">Index of the file name.</param>
		/// <returns></returns>
		public static List<BsonDocument> AddJson(this List<BsonDocument> aggregate, int index) {
			aggregate.AddRange(MongoManager.aggregationPipelinesLoader.Aggregates[index]);
			return aggregate;
		}

		/// <summary>
		/// Get the result of the aggregation.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aggregate">List of steps for the aggregation.</param>
		/// <param name="pageNb">Number of pages to skip.</param>
		/// <param name="pageSize">Number of items that we will retrieve.</param>
		/// <param name="databaseEnvironment">Which database environment.</param>
		/// <returns></returns>
		public static IAsyncCursor<T> GetResult<T>(this List<BsonDocument> aggregate, int? pageNb = null, int? pageSize = null, DatabaseEnvironment? databaseEnvironment = null) where T : BaseType {
			PipelineDefinition<T, T> pipelineDefinition = PipelineDefinition<T, T>.Create(aggregate);
			return MongoManager.GetAggregate(pipelineDefinition, pageNb, pageSize, databaseEnvironment);
		}
	};
}
