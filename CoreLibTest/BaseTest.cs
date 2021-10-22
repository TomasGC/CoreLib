using CoreLib.Aggregations;
using CoreLib.Base;
using CoreLib.Managers;
using CoreLib.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace CoreLibTest {
	/// <summary>
	/// Base test class.
	/// </summary>
	[TestClass]
	public abstract class BaseTest {
		static readonly string dataPath = "Data";
		
		protected virtual bool IsMongoNeeded { get; set; } = true;

		[TestInitialize]
		public virtual void Initialize() {
			if (!IsMongoNeeded)
				return;

			string aggregationPipelinesPath = Path.Combine(dataPath, "AggregationPipelines");
			string configFilePath = Path.Combine(dataPath, "MongoDB", "MongoConfig.xml");
			string credentialFilePath = Path.Combine(dataPath, "MongoDB", "MongoCredentials.xml");
			if (MongoManager.aggregationPipelinesLoader.Aggregates == null || MongoManager.aggregationPipelinesLoader.Aggregates.Count == 0)
				MongoManager.Configure(aggregationPipelinesPath, configFilePath, credentialFilePath);
		}
	};

	/// <summary>
	/// Base test class for settings.
	/// </summary>
	[TestClass]
	public abstract class BaseTest<T> : BaseTest where T : BaseSettings {
		/// <summary>
		/// Appsettings file name.
		/// </summary>
		protected virtual string JsonFile { get; set; }

		/// <summary>
		/// Initialization method.
		/// </summary>
		[TestInitialize]
		public override void Initialize() {
			// Initialize configuration.
			IConfiguration configuration = new ConfigurationBuilder().SetBasePath(Tools.GetExecutableRootPath()).AddJsonFile(JsonFile, false, true).AddEnvironmentVariables().Build();

			// Load json config.
			ServiceCollection services = new ServiceCollection();
			services.AddSingleton(configuration.GetSection("AppConfiguration:AggregationTypes").Get<AggregationTypes>());
			services.AddSingleton(configuration.GetSection("AppConfiguration:Settings").Get<T>());

			base.Initialize();
		}
	};
}
