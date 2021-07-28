using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using Serilog;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using CoreLib.Types;
using CoreLib.Aggregations;
using MongoDB.Driver.Core.Configuration;

namespace CoreLib.Managers {
    /// <summary>
    /// Database environment.
    /// </summary>
    public enum DatabaseEnvironment {
        Production,
        Unittest
    };

    /// <summary>
    /// Manage all the communication with the Mongo Db.
    /// </summary>
    public sealed class MongoManager {
        static readonly ILogger log = Log.ForContext(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Event used to watch a collection.
        /// IMPORTANT: Use it only ONCE per collection per binary project, else it will be called multiple times and that's not what you want.
        /// </summary>
        public static event Action<ChangeStreamOperationType, Type, BaseType> OnDbModified;

        static IMongoDatabase database;
        public static IBsonSerializerRegistry SerializerRegistry { get; private set; }
        static MongoClientSettings settings;
        static MongoConfigParser mongoXDocReader;

        #region Original Database
        public static string databaseName { get; private set; } = string.Empty;
        public static MongoClient client { get; private set; }
        #endregion

        #region Temporary Database
        static string temporaryDatabaseName = string.Empty;
        static MongoClient temporaryClient;
        static DatabaseEnvironment temporaryDatabaseType;
        #endregion

        static readonly UpdateOptions updateOptions = new UpdateOptions { IsUpsert = false };
        static readonly List<ChangeStreamOperationType> defaultOperationTypes = new List<ChangeStreamOperationType> { ChangeStreamOperationType.Delete, ChangeStreamOperationType.Insert, ChangeStreamOperationType.Replace, ChangeStreamOperationType.Update };
        static readonly ChangeStreamOptions watchOptions = new ChangeStreamOptions() { FullDocument = ChangeStreamFullDocumentOption.UpdateLookup, BatchSize = 1, MaxAwaitTime = new TimeSpan(0, 0, 0, 0, 10) };
        static readonly AggregateOptions aggregateOptions = new AggregateOptions() { AllowDiskUse = true, BatchSize = 10000, BypassDocumentValidation = true };
        public static AggregationPipelinesLoader aggregationPipelinesLoader { get; } = new AggregationPipelinesLoader();

        /// <summary>
        /// Get aggregate options.
        /// </summary>
        /// <returns></returns>
        public static AggregateOptions GetAggregateOptions() => aggregateOptions;

        /// <summary>
        /// Retrieve the current database environment.
        /// </summary>
        /// <returns></returns>
        public static DatabaseEnvironment GetPlatformEnvironment() => mongoXDocReader.DatabaseType;

        /// <summary>
        /// Configure the aggregationPipelinesLoader to read the json files.
        /// </summary>
        /// <param name="aggregationPipelineBasePath"></param>
        /// <param name="configFilePath"></param>
        /// <param name="credentialFilePath"></param>
        public static void Configure(string aggregationPipelineBasePath, string configFilePath, string credentialFilePath) {
            mongoXDocReader = new MongoConfigParser(configFilePath, credentialFilePath);

            // For some reasons the configuration of the client doesn't work without a connection string.
            #region MongoDB Atlas Cloud
            string connectionString = $"mongodb+srv://{mongoXDocReader.Login}:{mongoXDocReader.Password}@{mongoXDocReader.Hosts[0]}/{mongoXDocReader.DatabaseName}?retryWrites=true&w=majority&readPreference=primaryPreferred";
            client = new MongoClient(connectionString);
            temporaryClient = new MongoClient(connectionString);
            #endregion

            #region MongoDB Server
            //List<MongoServerAddress> servers = new List<MongoServerAddress>();
            //foreach (string host in mongoXDocReader.Hosts) {
            //    servers.Add(new MongoServerAddress(host, mongoXDocReader.Port));

            //    log.Information($"[db-driver] Mongos servers are: {host}:{mongoXDocReader.Port}");
            //}

            //settings = new MongoClientSettings {
            //    Scheme = ConnectionStringScheme.MongoDBPlusSrv,
            //    Servers = servers,
            //    UseTls = mongoXDocReader.UseSsl,
            //    ReadPreference = mongoXDocReader.ReadPreference,
            //    Credential = MongoCredential.CreateCredential(mongoXDocReader.AdminDatabaseName, mongoXDocReader.Login, mongoXDocReader.Password),
            //    ServerSelectionTimeout = new TimeSpan(0, 0, serverSelectionTimeoutInSeconds),
            //    ConnectTimeout = new TimeSpan(0, 0, connectTimeoutInSeconds)
            //};
            //client = new MongoClient(settings);
            //temporaryClient = new MongoClient(settings);
            #endregion

            databaseName = mongoXDocReader.DatabaseName;
            database = client.GetDatabase(databaseName);
            SerializerRegistry = database.Settings.SerializerRegistry;

            temporaryDatabaseType = mongoXDocReader.DatabaseType;
            temporaryDatabaseName = mongoXDocReader.DatabaseName;

            ConventionsRegister();
            ClassSerialization();

            log.Information($"Database is: {databaseName}.");

            aggregationPipelinesLoader.Configure(aggregationPipelineBasePath);
        }

        /// <summary>
        /// Setup the serialization conventions for MongoDB.
        /// </summary>
        static void ConventionsRegister() {
            BsonSerializer.RegisterSerializer(new DateTimeSerializer(DateTimeKind.Utc));

            // Set Conventions as Ignore Null Values into db store.
            ConventionPack pack = new ConventionPack { new IgnoreIfNullConvention(true), new IgnoreExtraElementsConvention(true) };
            ConventionRegistry.Register("conventions", pack, t => true);
        }

        /// <summary>
        /// Setup the custom class serializations for MongoDB.
        /// </summary>
        static void ClassSerialization() {
            // This will loop over all the classes that inherit from SerializationBaseType.
            // x.IsClass && !x.IsAbstract are mandatory to avoid to have an exception, as abstract class aren't instanciable.
            foreach (Type type in Assembly.GetAssembly(typeof(SerializationBaseType)).GetTypes().Where(x => x.IsClass && !x.IsAbstract && x.IsSubclassOf(typeof(SerializationBaseType)))) {
                SerializationBaseType a = (SerializationBaseType)Activator.CreateInstance(type);
                a.Serialization();
            }
        }

        #region Original
        /// <summary>
        /// Retrieve the collection.
        /// </summary>
        /// <param name="databaseType">The database where the collection is.</param>
        public static IMongoCollection<T> GetCollection<T>(DatabaseEnvironment? databaseType = null) where T : BaseType {
            if (!CheckDatabase(databaseType))
                return null;

            return database.GetCollection<T>(typeof(T).Name);
        }

        /// <summary>
        /// Returns the queryable with the count condition already assigned.
        /// </summary>
        /// <param name="predicate">Condition for the existance in the collection.</param>
        /// <param name="databaseType">The database where the collection is.</param>
        public static long Count<T>(Expression<Func<T, bool>> predicate = null, DatabaseEnvironment? databaseType = null) where T : BaseType {
            if (predicate != null)
                return GetCollection<T>(databaseType).CountDocuments(predicate);
            else
                return GetCollection<T>(databaseType).CountDocuments(x => true);
        }

        /// <summary>
        /// Clear a collection.
        /// </summary>
        /// <param name="databaseType">The database where the collection is.</param>
        public static void ClearCollection<T>(DatabaseEnvironment? databaseType = null) where T : BaseType {
            if (!CheckDatabase(databaseType))
                return;

            if (Count<T>() > 0)
                DeleteItems<T>(x => true);
        }

        #region ToList
        /// <summary>
        /// Retrieve the list directly instead of the collection.
        /// </summary>
        /// <param name="predicateOrder">Reordering condition.</param>
        /// <param name="predicateOrderSkip">Skip reordering totally?</param>
        /// <param name="databaseType">The database where the collection is.</param>
        public static List<T> ToList<T>(Expression<Func<T, object>> predicateOrder = null, bool predicateOrderSkip = false, DatabaseEnvironment? databaseType = null) where T : BaseType => Find(null, predicateOrder, predicateOrderSkip, databaseType)?.ToList();

		/// <summary>
		/// Retrieve the list directly instead of the collection.
		/// </summary>
		/// <param name="predicateOrder">Reordering condition.</param>
		/// <param name="databaseType">The database where the collection is.</param>
		public static List<T> ToListDescending<T>(Expression<Func<T, object>> predicateOrder = null, DatabaseEnvironment? databaseType = null) where T : BaseType => FindDescending(null, predicateOrder, databaseType)?.ToList();
		#endregion ToList

		#region First
		/// <summary>
		/// Returns the queryable with the first condition already assigned.
		/// </summary>
		/// <param name="predicate">Condition for the existance in the collection.</param>
		/// <param name="databaseType">The database where the collection is.</param>
		public static T First<T>(Expression<Func<T, bool>> predicate = null, DatabaseEnvironment? databaseType = null) where T : BaseType => Find(predicate, null, true, databaseType)?.First();

		/// <summary>
		/// Returns the queryable with the first or default condition already assigned.
		/// </summary>
		/// <param name="predicate">Condition for the existance in the collection.</param>
		/// <param name="databaseType">The database where the collection is.</param>
		public static T FirstOrDefault<T>(Expression<Func<T, bool>> predicate = null, DatabaseEnvironment? databaseType = null) where T : BaseType => Find(predicate, null, true, databaseType)?.FirstOrDefault();
		#endregion First

		#region Last
		/// <summary>
		/// Returns the queryable with the first condition already assigned.
		/// </summary>
		/// <param name="predicate">Condition for the existance in the collection.</param>
		/// <param name="predicateOrder">Reordering condition.</param>
		/// <param name="databaseType">The database where the collection is.</param>
		public static T Last<T>(Expression<Func<T, bool>> predicate = null, Expression<Func<T, object>> predicateOrder = null, DatabaseEnvironment? databaseType = null) where T : BaseType => FindDescending(predicate, predicateOrder, databaseType)?.First();

		/// <summary>
		/// Returns the queryable with the first condition already assigned.
		/// </summary>
		/// <param name="predicate">Condition for the existance in the collection.</param>
		/// <param name="predicateOrder">Reordering condition.</param>
		/// <param name="databaseType">The database where the collection is.</param>
		public static T LastOrDefault<T>(Expression<Func<T, bool>> predicate = null, Expression<Func<T, object>> predicateOrder = null, DatabaseEnvironment? databaseType = null) where T : BaseType => FindDescending(predicate, predicateOrder, databaseType)?.FirstOrDefault();
		#endregion Last

		#region Single
		/// <summary>
		/// Returns the queryable with the single condition already assigned.
		/// </summary>
		/// <param name="predicate">Condition for the existance in the collection.</param>
		/// <param name="databaseType">The database where the collection is.</param>
		public static T Single<T>(Expression<Func<T, bool>> predicate = null, DatabaseEnvironment? databaseType = null) where T : BaseType => Find(predicate, null, true, databaseType)?.Single();

		/// <summary>
		/// Returns the queryable with the single or default condition already assigned.
		/// </summary>
		/// <param name="predicate">Condition for the existance in the collection.</param>
		/// <param name="databaseType">The database where the collection is.</param>
		public static T SingleOrDefault<T>(Expression<Func<T, bool>> predicate = null, DatabaseEnvironment? databaseType = null) where T : BaseType => Find(predicate, null, true, databaseType)?.SingleOrDefault();
		#endregion Single

		#region Where
		/// <summary>
		/// Returns the queryable with the where condition already assigned.
		/// </summary>
		/// <param name="predicate">Condition for the existance in the collection.</param>
		/// <param name="predicateOrder">Reordering condition.</param>
		/// <param name="databaseType">The database where the collection is.</param>
		public static List<T> Where<T>(Expression<Func<T, bool>> predicate, Expression<Func<T, object>> predicateOrder = null, DatabaseEnvironment? databaseType = null) where T : BaseType => Find(predicate, predicateOrder, false, databaseType)?.ToList();

		/// <summary>
		/// Returns the queryable with the where condition already assigned (using Filter Definition from MongoDB).
		/// </summary>
		/// <param name="predicate">Condition for the existance in the collection.</param>
		/// <param name="predicateOrder">Reordering condition.</param>
		/// <param name="databaseType">The database where the collection is.</param>
		public static List<T> Where<T>(FilterDefinition<T> predicate, Expression<Func<T, object>> predicateOrder = null, DatabaseEnvironment? databaseType = null) where T : BaseType => Find(predicate, predicateOrder, false, databaseType)?.ToList();

		/// <summary>
		/// Returns the queryable with the where condition already assigned.
		/// </summary>
		/// <param name="predicate">Condition for the existance in the collection.</param>
		/// <param name="predicateOrder">Reordering condition.</param>
		/// <param name="databaseType">The database where the collection is.</param>
		public static List<T> WhereDescending<T>(Expression<Func<T, bool>> predicate, Expression<Func<T, object>> predicateOrder = null, DatabaseEnvironment? databaseType = null) where T : BaseType => FindDescending(predicate, predicateOrder, databaseType)?.ToList();

		/// <summary>
		/// Returns the queryable with the where condition already assigned.
		/// </summary>
		/// <param name="predicate">Condition for the existance in the collection.</param>
		/// <param name="pageNumber">Number of the page to start.</param>
		/// <param name="pageSize">Number of item we want.</param>
		/// <param name="predicateOrder">Reordering condition.</param>
		/// <param name="databaseType">The database where the collection is.</param>
		public static List<T> Where<T>(Expression<Func<T, bool>> predicate, int pageNumber, int pageSize, Expression<Func<T, object>> predicateOrder = null, DatabaseEnvironment? databaseType = null) where T : BaseType => Find(predicate, predicateOrder, false, databaseType)?.Skip(pageNumber * pageSize)?.Limit(pageSize)?.ToList();

		/// <summary>
		/// Returns the queryable with the where condition already assigned.
		/// </summary>
		/// <param name="predicate">Condition for the existance in the collection.</param>
		/// <param name="databaseType">The database where the collection is.</param>
		public static List<T> WhereDescending<T>(Expression<Func<T, bool>> predicate, int pageNumber, int pageSize, Expression<Func<T, object>> predicateOrder = null, DatabaseEnvironment? databaseType = null) where T : BaseType => FindDescending(predicate, predicateOrder, databaseType)?.Skip(pageNumber * pageSize)?.Limit(pageSize)?.ToList();

		/// <summary>
		/// Returns the non-sorted queryable with the where condition already assigned.
		/// </summary>
		/// <param name="predicate">Condition for the existance in the collection.</param>
		/// <param name="databaseType">The database where the collection is.</param>
		public static List<T> WhereNoSort<T>(Expression<Func<T, bool>> predicate, DatabaseEnvironment? databaseType = null) where T : BaseType => Find(predicate, null, true, databaseType)?.ToList();
		#endregion Where

		#endregion Original

		#region Queryable
		/// <summary>
		/// Retrieve the queryable directly instead of the collection. So far too slow...
		/// </summary>
		/// <param name="databaseType">The database where the collection is.</param>
		public static IMongoQueryable<T> GetQueryable<T>(Expression<Func<T, object>> predicateOrder = null, DatabaseEnvironment? databaseType = null) where T : BaseType {
            if (predicateOrder != null)
                return GetCollection<T>(databaseType).AsQueryable().OrderBy(predicateOrder);
            else
                return GetCollection<T>(databaseType).AsQueryable().OrderBy(x => x._id);
        }

        /// <summary>
        /// Retrieve the queryable directly instead of the collection. So far too slow...
        /// </summary>
        /// <param name="databaseType">The database where the collection is.</param>
        public static IMongoQueryable<T> GetQueryableDescending<T>(Expression<Func<T, object>> predicateOrder = null, DatabaseEnvironment? databaseType = null) where T : BaseType {
            if (predicateOrder != null)
                return GetCollection<T>(databaseType).AsQueryable().OrderByDescending(predicateOrder);
            else
                return GetCollection<T>(databaseType).AsQueryable().OrderByDescending(x => x._id);
        }

        /// <summary>
        /// Returns the queryable with the count condition already assigned.
        /// </summary>
        /// <param name="predicate">Condition for the existance in the collection.</param>
        /// <param name="databaseType">The database where the collection is.</param>
        public static long CountQueryable<T>(Expression<Func<T, bool>> predicate = null, DatabaseEnvironment? databaseType = null) where T : BaseType {
            if (predicate != null)
                return GetQueryable<T>(null, databaseType).Count(predicate);
            else
                return GetQueryable<T>(null, databaseType).Count(x => true);
        }

        /// <summary>
        /// Return the query of the aggregation.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="aggregationType"></param>
        /// <param name="filter">Condition for the existance in the collection.</param>
        /// <param name="pageNb">Number of the page to start.</param>
        /// <param name="pageSize">Number of item we want.</param>
        /// <param name="databaseType">The database where the collection is.</param>
        /// <returns></returns>
        public static IAsyncCursor<T> GetAggregate<T>(int aggregationType, Expression<Func<T, bool>> filter = null, int? pageNb = null, int? pageSize = null, DatabaseEnvironment? databaseType = null) where T : BaseType {
            List<BsonDocument> aggregate = new List<BsonDocument>();
            aggregate.Match(filter).AddJson(aggregationType);

            return aggregate.GetResult<T>(pageNb, pageSize, databaseType);
        }

        /// <summary>
        /// Return the query of the aggregation.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="aggregationType"></param>
        /// <param name="match">Condition for the existance in the collection.</param>
        /// <param name="pageNb">Number of the page to start.</param>
        /// <param name="pageSize">Number of item we want.</param>
        /// <param name="databaseType">The database where the collection is.</param>
        /// <returns></returns>
        public static IAsyncCursor<T> GetAggregate<T>(int aggregationType, BsonDocument match = null, int? pageNb = null, int? pageSize = null, DatabaseEnvironment? databaseType = null) where T : BaseType {
            List<BsonDocument> aggregate;
            if (match != null) {
                aggregate = new List<BsonDocument>() { match };
                aggregate.AddRange(aggregationPipelinesLoader.Aggregates[aggregationType]);
            }
            else
                aggregate = aggregationPipelinesLoader.Aggregates[aggregationType];

            PipelineDefinition<T, T> pipeline = PipelineDefinition<T, T>.Create(aggregate);

            if (pageNb.HasValue && pageNb.Value != -1 && pageSize.HasValue && pageSize.Value != -1)
                pipeline = pipeline.Skip(pageNb.Value * pageSize.Value).Limit(pageSize.Value);

            return GetCollection<T>(databaseType).Aggregate(pipeline, aggregateOptions);
        }

        /// <summary>
        /// Return the query of the aggregation.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="pipeline">Condition for the existance in the collection.</param>
        /// <param name="pageNb">Number of the page to start.</param>
        /// <param name="pageSize">Number of item we want.</param>
        /// <param name="databaseType">The database where the collection is.</param>
        /// <returns></returns>
        public static IAsyncCursor<T> GetAggregate<T>(PipelineDefinition<T, T> pipeline, int? pageNb = null, int? pageSize = null, DatabaseEnvironment? databaseType = null) where T : BaseType {
            if (pageNb.HasValue && pageNb.Value != -1 && pageSize.HasValue && pageSize.Value != -1)
                pipeline = pipeline.Skip(pageNb.Value * pageSize.Value).Limit(pageSize.Value);

            return GetCollection<T>(databaseType).Aggregate(pipeline, aggregateOptions);
        }
        #endregion Queryable

        #region First
        /// <summary>
        /// Returns the queryable with the first condition already assigned.
        /// </summary>
        /// <param name="predicate">Condition for the existance in the collection.</param>
        /// <param name="databaseType">The database where the collection is.</param>
        public static T FirstQueryable<T>(Func<T, bool> predicate = null, DatabaseEnvironment? databaseType = null) where T : BaseType {
            if (predicate != null)
                return GetQueryable<T>(null, databaseType)?.First(predicate);
            else
                return GetQueryable<T>(null, databaseType)?.First();
        }

        /// <summary>
        /// Returns the queryable with the first condition already assigned.
        /// </summary>
        /// <param name="predicate">Condition for the existance in the collection.</param>
        /// <param name="databaseType">The database where the collection is.</param>
        public static T FirstOrDefaultQueryable<T>(Func<T, bool> predicate = null, DatabaseEnvironment? databaseType = null) where T : BaseType {
            if (predicate != null)
                return GetQueryable<T>()?.FirstOrDefault(predicate);
            else
                return GetQueryable<T>()?.FirstOrDefault();
        }
        #endregion First

        #region Single
        /// <summary>
        /// Returns the queryable with the single or default condition already assigned.
        /// </summary>
        /// <param name="predicate">Condition for the existance in the collection.</param>
        /// <param name="databaseType">The database where the collection is.</param>
        public static T SingleOrDefaultQueryable<T>(Func<T, bool> predicate = null, DatabaseEnvironment? databaseType = null) where T : BaseType {
            if (predicate != null)
                return GetQueryable<T>(null, databaseType)?.SingleOrDefault(predicate);
            else
                return GetQueryable<T>(null, databaseType)?.SingleOrDefault();
        }
		#endregion Single

		#region Where
		/// <summary>
		/// Returns the queryable with the where condition already assigned.
		/// </summary>
		/// <param name="predicate">Condition for the existance in the collection.</param>
		/// <param name="predicateOrder">Reordering condition.</param>
		/// <param name="databaseType">The database where the collection is.</param>
		public static IQueryable<T> WhereQueryable<T>(Expression<Func<T, bool>> predicate, Expression<Func<T, object>> predicateOrder = null, DatabaseEnvironment? databaseType = null) where T : BaseType => GetQueryable(predicateOrder, databaseType)?.Where(predicate)?.AsQueryable();

		/// <summary>
		/// Returns the queryable with the where condition already assigned.
		/// </summary>
		/// <param name="predicate">Condition for the existance in the collection.</param>
		/// <param name="pageNumber">Number of the page to start.</param>
		/// <param name="pageSize">Number of item we want.</param>
		/// <param name="predicateOrder">Reordering condition.</param>
		/// <param name="databaseType">The database where the collection is.</param>
		public static IQueryable<T> WhereQueryable<T>(Expression<Func<T, bool>> predicate, int pageNumber, int pageSize, Expression<Func<T, object>> predicateOrder = null, DatabaseEnvironment? databaseType = null) where T : BaseType => GetQueryable(predicateOrder, databaseType)?.Where(predicate)?.Skip(pageNumber * pageSize)?.Take(pageSize)?.AsQueryable();

		/// <summary>
		/// Returns the queryable with the where condition already assigned.
		/// </summary>
		/// <param name="predicate">Condition for the existance in the collection.</param>
		/// <param name="predicateOrder">Reordering condition.</param>
		/// <param name="databaseType">The database where the collection is.</param>
		public static IQueryable<T> WhereQueryableDescending<T>(Expression<Func<T, bool>> predicate, Expression<Func<T, object>> predicateOrder = null, DatabaseEnvironment? databaseType = null) where T : BaseType => GetQueryableDescending(predicateOrder, databaseType)?.Where(predicate)?.AsQueryable();

		/// <summary>
		/// Returns the queryable with the where condition already assigned.
		/// </summary>
		/// <param name="predicate">Condition for the existance in the collection.</param>
		/// <param name="pageNumber">Number of the page to start.</param>
		/// <param name="pageSize">Number of item we want.</param>
		/// <param name="predicateOrder">Reordering condition.</param>
		/// <param name="databaseType">The database where the collection is.</param>
		public static IQueryable<T> WhereQueryableDescending<T>(Expression<Func<T, bool>> predicate, int pageNumber, int pageSize, Expression<Func<T, object>> predicateOrder = null, DatabaseEnvironment? databaseType = null) where T : BaseType => GetQueryableDescending(predicateOrder, databaseType)?.Where(predicate)?.Skip(pageNumber * pageSize)?.Take(pageSize)?.AsQueryable();
		#endregion Where

		#region Modifiers
		/// <summary>
		/// Create or update the item, depending on the id.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="item"></param>
		/// <param name="isNoIncrement"></param>
		/// <param name="databaseType"></param>
		public static void Save<T>(T item, bool isNoIncrement = false, DatabaseEnvironment? databaseType = null) where T : BaseType {
            if (isNoIncrement && !ItemExists<T>(x => x._id == item._id))
                AddItemInCollectionNoIncrement(item, databaseType);
            if (item.IsNew)
                AddItemInCollection(item, databaseType);
            else
                UpdateItem(item, databaseType);
        }

		/// <summary>
		/// Add an item in the collection.
		/// </summary>
		/// <param name="item">The item to add.</param>
		/// <param name="databaseType">The database where the collection is.</param>
		public static void AddItemInCollectionNoIncrement<T>(T item, DatabaseEnvironment? databaseType = null) where T : BaseType => GetCollection<T>(databaseType)?.InsertOne(item);

        /// <summary>
        /// Add an item in the collection.
        /// </summary>
        /// <param name="items">The items to add.</param>
        /// <param name="databaseType">The database where the collection is.</param>
        public static void AddItemsInCollectionNoIncrement<T>(List<T> items, DatabaseEnvironment? databaseType = null) where T : BaseType => GetCollection<T>(databaseType)?.InsertMany(items);

		/// <summary>
		/// Add an item in the collection.
		/// </summary>
		/// <param name="item">The item to add.</param>
		/// <param name="databaseType">The database where the collection is.</param>
		public static void AddItemInCollection<T>(T item, DatabaseEnvironment? databaseType = null) where T : BaseType {
            IMongoCollection<T> collection = GetCollection<T>(databaseType);
            if (collection == null)
                return;

            item._id = ObjectId.GenerateNewId();

            try {
                collection.InsertOne(item);
            }
            catch (MongoWriteException) {
                log.Warning($"Warning: Duplicated key ({item._id}) for item type: {typeof(T).Name}.");
                AddItemInCollection(item, databaseType);
            }
        }

        /// <summary>
        /// Update an item in the collection.
        /// </summary>
        /// <param name="item">The item to update.</param>
        /// <param name="databaseType">The database where the collection is.</param>
        public static void  UpdateItem<T>(T item, DatabaseEnvironment? databaseType = null) where T : BaseType {
            if (GetCollection<T>(databaseType)?.ReplaceOne(o => o._id == item._id, item).MatchedCount == 0)
                throw new Exception($"The item with id {item._id} in collection {typeof(T).Name} wasn't present in the database {databaseName}.");
        }

        /// <summary>
        /// Update items in the collection by a list of ids.
        /// </summary>
        /// <param name="itemIds">The ids' list of item to update.</param>
        /// <param name="updates">The fields to update.</param>
        /// <param name="databaseType">The database where the collection is.</param>
        public static void UpdateItems<T>(List<ObjectId> itemIds, UpdateDefinition<T> updates, DatabaseEnvironment? databaseType = null) where T : BaseType {
            if (GetCollection<T>(databaseType)?.UpdateMany(o => itemIds.Contains(o._id), updates, updateOptions).MatchedCount == 0)
                throw new Exception($"Some of items in collection {typeof(T).Name} weren't present in the database {databaseName}.");
        }

        /// <summary>
        /// Update items in the collection by a list of ids.
        /// </summary>
        /// <param name="filter">The ids' list of item to update.</param>
        /// <param name="updates">The fields to update.</param>
        /// <param name="databaseType">The database where the collection is.</param>
        public static void UpdateItems<T>(Expression<Func<T, bool>> filter, UpdateDefinition<T> updates, DatabaseEnvironment? databaseType = null) where T : BaseType {
            if (GetCollection<T>(databaseType)?.UpdateMany(filter, updates, updateOptions).MatchedCount == 0)
                throw new Exception($"Some of items in collection {typeof(T).Name} weren't present in the database {databaseName}.");
        }

        /// <summary>
        /// Delete an item from the collection.
        /// </summary>
        /// <param name="id">The id of the item.</param>
        /// <param name="databaseType">The database where the collection is.</param>
        public static void DeleteItem<T>(ObjectId id, DatabaseEnvironment? databaseType = null) where T : BaseType {
            if (GetCollection<T>(databaseType)?.DeleteOne(d => d._id == id).DeletedCount == 0)
                throw new Exception($"The item with id {id} in collection {typeof(T).Name} wasn't present in the database {databaseName}.");
        }

        /// <summary>
        /// Delete an item from the collection through a predicate.
        /// </summary>
        /// <param name="predicate">Condition to delete the items.</param>
        /// <param name="databaseType">The database where the collection is.</param>
        public static void DeleteItem<T>(Expression<Func<T, bool>> predicate, DatabaseEnvironment? databaseType = null) where T : BaseType {
            if (GetCollection<T>(databaseType)?.DeleteOne(predicate).DeletedCount == 0)
                throw new Exception($"The item with predicate {predicate} in collection {typeof(T).Name} wasn't present in the database {databaseName}.");
        }

		/// <summary>
		/// Delete items from the collection through a list of Ids.
		/// </summary>
		/// <param name="ids">The list of ids.</param>
		/// <param name="databaseType">The database where the collection is.</param>
		public static void DeleteItems<T>(List<ObjectId> ids, DatabaseEnvironment? databaseType = null) where T : BaseType => ids.ForEach(i => DeleteItem<T>(i, databaseType));

		/// <summary>
		/// Delete items from the collection through a list of items.
		/// </summary>
		/// <param name="items">The id of the item.</param>
		/// <param name="databaseType">The database where the collection is.</param>
		public static void DeleteItems<T>(List<T> items, DatabaseEnvironment? databaseType = null) where T : BaseType => items.ForEach(i => DeleteItem<T>(i._id, databaseType));

		/// <summary>
		/// Delete items from the collection through a predicate.
		/// </summary>
		/// <param name="predicate">The id of the item.</param>
		/// <param name="databaseType">The database where the collection is.</param>
		public static void DeleteItems<T>(Expression<Func<T, bool>> predicate, DatabaseEnvironment? databaseType = null) where T : BaseType {
            if (GetCollection<T>(databaseType)?.DeleteMany(predicate).DeletedCount == 0)
                throw new Exception($"The item with predicate {predicate} in collection {typeof(T).Name} wasn't present in the database {databaseName}.");
        }
        #endregion Modifiers

        #region Checkers
        /// <summary>
        /// Returns the queryable with the any condition already assigned.
        /// </summary>
        /// <param name="predicate">Condition for the existance in the collection.</param>
        /// <param name="databaseType">The database where the collection is.</param>
        public static bool Any<T>(Expression<Func<T, bool>> predicate, DatabaseEnvironment? databaseType = null) where T : BaseType => Find(predicate, null, true, databaseType).Any();

		/// <summary>
		/// Check if the item exists in the collection.
		/// </summary>
		/// <param name="predicate">Condition for the existance in the collection.</param>
		/// <param name="databaseType">The database where the collection is.</param>
		public static bool ItemExists<T>(Expression<Func<T, bool>> predicate, DatabaseEnvironment? databaseType = null) where T : BaseType => Count(predicate, databaseType) > 0;

		/// <summary>
		/// Check if the item exists as unique in the collection.
		/// </summary>
		/// <param name="predicate">Condition for the existance in the collection.</param>
		/// <param name="databaseType">The database where the collection is.</param>
		public static bool ItemExist<T>(Expression<Func<T, bool>> predicate, DatabaseEnvironment? databaseType = null) where T : BaseType => Count(predicate, databaseType) == 1;
		#endregion Checkers

        #region Internal
        /// <summary>
        /// For Unittests and Admin: Change the temporary database.
        /// </summary>
        /// <param name="databaseType">The list to display as Bson.</param>
        static void ChangeDatabase(DatabaseEnvironment? databaseType = null) {
            if (!databaseType.HasValue) {
                database = client.GetDatabase(databaseName);
                return;
            }

            if (temporaryDatabaseType != databaseType.Value) {
                temporaryDatabaseType = databaseType.Value;

                MongoClientSettings settings = temporaryClient.Settings.Clone();
                mongoXDocReader.DatabaseType = temporaryDatabaseType;
                temporaryDatabaseName = mongoXDocReader.DatabaseName;
                settings.Credential = MongoCredential.CreateCredential(settings.Credential.Source, mongoXDocReader.Login, mongoXDocReader.Password);
                temporaryClient = new MongoClient(settings);
            }

            database = temporaryClient.GetDatabase(temporaryDatabaseName);
        }

        /// <summary>
        /// Find the element in the collection sorted by ascending.
        /// </summary>
        /// <param name="predicate"></param>
        /// <param name="predicateOrder"></param>
        /// <param name="predicateOrderSkip"></param>
        /// <param name="databaseType">The database where the collection is.</param>
        static IFindFluent<T, T> Find<T>(Expression<Func<T, bool>> predicate = null, Expression<Func<T, object>> predicateOrder = null, bool predicateOrderSkip = false, DatabaseEnvironment? databaseType = null) where T : BaseType {
            if (predicate == null)
                predicate = x => true;

            if (predicateOrderSkip)
                return GetCollection<T>(databaseType)?.Find(predicate);

            if (predicateOrder != null)
                return GetCollection<T>(databaseType)?.Find(predicate)?.Sort(Builders<T>.Sort.Ascending(predicateOrder));
            else
                return GetCollection<T>(databaseType)?.Find(predicate)?.Sort(Builders<T>.Sort.Ascending(x => x._id));
        }

        /// <summary>
        /// Find the element in the collection sorted by ascending.
        /// </summary>
        /// <param name="predicate"></param>
        /// <param name="predicateOrder"></param>
        /// <param name="predicateOrderSkip"></param>
        /// <param name="databaseType">The database where the collection is.</param>
        static IFindFluent<T, T> Find<T>(FilterDefinition<T> predicate = null, Expression<Func<T, object>> predicateOrder = null, bool predicateOrderSkip = false, DatabaseEnvironment? databaseType = null) where T : BaseType {
            if (predicateOrderSkip)
                return GetCollection<T>(databaseType)?.Find(predicate);

            if (predicateOrder != null)
                return GetCollection<T>(databaseType)?.Find(predicate)?.Sort(Builders<T>.Sort.Ascending(predicateOrder));
            else
                return GetCollection<T>(databaseType)?.Find(predicate)?.Sort(Builders<T>.Sort.Ascending(x => x._id));
        }

        /// <summary>
        /// Find the element in the collection sorted by descending.
        /// </summary>
        /// <param name="predicate"></param>
        /// <param name="databaseType">The database where the collection is.</param>
        static IFindFluent<T, T> FindDescending<T>(Expression<Func<T, bool>> predicate = null, Expression<Func<T, object>> predicateOrder = null, DatabaseEnvironment? databaseType = null) where T : BaseType {
            if (predicate == null)
                predicate = x => true;

            if (predicateOrder != null)
                return GetCollection<T>(databaseType)?.Find(predicate)?.Sort(Builders<T>.Sort.Descending(predicateOrder));
            else
                return GetCollection<T>(databaseType)?.Find(predicate)?.Sort(Builders<T>.Sort.Descending(x => x._id));
        }

        /// <summary>
        /// Check if the database exists and if not, create it and set the enable sharding.
        /// </summary>
        /// <param name="databaseType">To use another database</param>
        static bool CheckDatabase(DatabaseEnvironment? databaseType = null) {
            ChangeDatabase(databaseType);
            return true;
        }
        #endregion Internal

        /// <summary>
        /// Watch the collection for any change (Replace, Instert, Update or delete).
        /// Auto retry enabled but locking thread (need to be placed on a Task/Thread)
        /// </summary>
        public static void Watch<T>(List<ChangeStreamOperationType> operationTypes = null, DatabaseEnvironment? databaseType = null) where T : BaseType {
            if (operationTypes == null)
                operationTypes = defaultOperationTypes;

            try {
                var pipeline = new EmptyPipelineDefinition<ChangeStreamDocument<T>>().Match(x => operationTypes.Contains(x.OperationType));
                using (var cursor = GetCollection<T>(databaseType).Watch(pipeline, watchOptions)) {
                    foreach (var change in cursor.ToEnumerable())
                        OnDbModified?.Invoke(change.OperationType, change.OperationType != ChangeStreamOperationType.Delete ? change.FullDocument.GetType() : change.GetType(), change.OperationType != ChangeStreamOperationType.Delete ? change.FullDocument : new BaseType() { _id = change.DocumentKey.AsBsonValue["_id"].AsObjectId });
                }
            }
            catch (Exception e) {
                log.Warning($"Watch failed. Exception = [{e.Message}], Stack = [{e.StackTrace}] -> Reconnect it...");

                Thread.Sleep(100);
                Watch<T>(operationTypes);
            }
        }
    };
}