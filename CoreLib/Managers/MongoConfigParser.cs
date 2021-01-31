using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using CoreLib.Utils;
using MongoDB.Driver;
using Serilog;

namespace CoreLib.Managers {
	/// <summary>
	/// Retrieve the configs from the XML for MongoDB.
	/// </summary>
	public sealed class MongoConfigParser {
        static readonly ILogger log = Log.ForContext(MethodBase.GetCurrentMethod().DeclaringType);

        DatabaseEnvironment _databaseType;
        XDocument dbConfigFile;
        XElement databaseConfig;

        XDocument dbCredentialsFile;
        XElement databaseCredentials;

        public MongoConfigParser(string configFilePath, string credentialFilePath) {
            try {
                dbConfigFile = XDocument.Load(configFilePath);
            }
            catch (Exception e) {
                log.Error($"Could not load MongoDbManager Config Xml! Error = [{e.Message}], Stack = [{e.StackTrace}]");
            }

            databaseConfig = dbConfigFile.Element("DatabaseConfig");

            try {
                dbCredentialsFile = XDocument.Load(credentialFilePath);
            }
            catch (Exception e) {
                log.Error($"Could not load MongoDbManager Logins Xml! Error = [{e.Message}], Stack = [{e.StackTrace}]");
            }

            databaseCredentials = dbCredentialsFile.Element("DatabaseCredentials");
        }

        /// <summary>
        /// Property: host of MongoDB.
        /// </summary>
        public List<string> Hosts {
            get {
                List<string> hosts = new List<string>();

                foreach (XElement host in databaseCredentials.Elements("Credentials").Where(x => x.Attribute("name").Value == DatabaseType.ToString()).First().Element("Hosts").Elements("Host"))
                    hosts.Add(host.Value);

                return hosts;
            }
        }

        /// <summary>
        /// Property: port of MongoDB.
        /// </summary>
        public int Port => int.Parse(databaseConfig.Element("Port").Value);

        /// <summary>
        /// Property: if we use or not ssl in MongoDB.
        /// </summary>
        public bool UseSsl => bool.Parse(databaseConfig.Element("UseSsl").Value);

        /// <summary>
        /// Property: admin database name in MongoDB.
        /// </summary>
        public string AdminDatabaseName => databaseConfig.Element("AdminDatabaseName").Value;

        /// <summary>
        /// Property: current database name in MongoDB.
        /// </summary>
        public DatabaseEnvironment DatabaseType {
            get {
                if (_databaseType == DatabaseEnvironment.Unittest)
                    _databaseType = Tools.EnumParse<DatabaseEnvironment>(databaseConfig.Element("DatabaseType").Value);

                return _databaseType;
            }
            set { _databaseType = value == DatabaseEnvironment.Unittest ? Tools.EnumParse<DatabaseEnvironment>(databaseConfig.Element("DatabaseType").Value) : value; }
        }

        /// <summary>
        /// Property: login of MongoDB.
        /// </summary>
        public string Login => databaseCredentials.Elements("Credentials").Where(x => x.Attribute("name").Value == DatabaseType.ToString()).First().Element("Login").Value;

        /// <summary>
        /// Property: password of MongoDB.
        /// </summary>
        public string Password => databaseCredentials.Elements("Credentials").Where(x => x.Attribute("name").Value == DatabaseType.ToString()).First().Element("Password").Value;

        /// <summary>
        /// Property: current database name in MongoDB.
        /// </summary>
        public string DatabaseName => databaseCredentials.Elements("Credentials").Where(x => x.Attribute("name").Value == DatabaseType.ToString()).First().Element("DatabaseName").Value;

        /// <summary>
        /// Property: reading preferences: Primary or PrimaryPrefered (allow to check on secondaries if primary unavailable).
        /// </summary>
        public ReadPreference ReadPreference => databaseConfig.Element("ReadPreference").Value.Equals("Primary") ? ReadPreference.Primary : ReadPreference.PrimaryPreferred;
    };
}
