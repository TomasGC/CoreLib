using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using CoreLib.Aggregations;
using CoreLib.Converters;
using CoreLib.Managers;
using CoreLib.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Serilog;

[assembly: ApiController]
namespace CoreLib.Base {
	public abstract class BaseSettings {
		/// <summary>
		/// Name of the API.
		/// </summary>
		public static string ApiName { get; set; }
		/// <summary>
		/// Version of the API.
		/// </summary>
		public static string ApiVersion { get; set; }
		/// <summary>
		/// URL extension to reach Swagger's page.
		/// </summary>
		public static string SwaggerBaseUrl { get; set; }
	};
	
	/// <summary>
	/// Basic configuration infos.
	/// </summary>
	public sealed class BaseConfig { 
		/// <summary>
		/// Path of the xml file.
		/// </summary>
		public string XmlFilePath { get; set; }

		public BaseConfig(string xmlFilePath) {
			XmlFilePath = xmlFilePath;
		}
	};

	/// <summary>
	/// Startup class.
	/// </summary>
	public class BaseStartup {
		/// <summary>
		/// Data folder path.
		/// </summary>
		static readonly string dataPath = Path.Combine(Tools.GetExecutableRootPath(), "Data");

		/// <summary>
		/// Logger.
		/// </summary>
		protected static readonly ILogger log = Log.ForContext(MethodBase.GetCurrentMethod().DeclaringType);

		/// <summary>
		/// Assembly Name info from the child Startup class.
		/// </summary>
		protected static AssemblyName AssemblyName;

		/// <summary>
		/// Configuration of the server.
		/// </summary>
		public static IConfiguration Configuration { get; protected set; }

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="configuration"></param>
		/// <param name="assemblyName"></param>
		public BaseStartup(IConfiguration configuration, AssemblyName assemblyName) { 
			Configuration = configuration;
			AssemblyName = assemblyName;
		}

		/// <summary>
		/// This method gets called by the runtime. Use this method to add services to the container.
		/// </summary>
		/// <param name="services"></param>
		protected void ConfigureServices<T>(IServiceCollection services) where T : BaseSettings {
			services.AddSingleton(Configuration.GetSection("AppConfiguration:AggregationTypes").Get<AggregationTypes>());
			services.AddSingleton(Configuration.GetSection("AppConfiguration:Settings").Get<T>());

			// Register the Swagger generator, defining 1 or more Swagger documents.
			services.AddSwaggerGen(c => {
				c.SwaggerDoc("v1", new OpenApiInfo { Title = BaseSettings.ApiName, Version = BaseSettings.ApiVersion });

				c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { 
					In = ParameterLocation.Header, 
					Description = @"Please put your session key here.",
					Name = "Authorization",
					Type = SecuritySchemeType.ApiKey,
					Scheme = "Bearer"
				});

				c.AddSecurityRequirement(new OpenApiSecurityRequirement { { new OpenApiSecurityScheme {
						Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
						Scheme = "oauth2",
						Name = "Bearer",
						In = ParameterLocation.Header,
					},
					new List<string>()
				} });

				// Set the comments path for the Swagger JSON and UI.
				c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{AssemblyName.Name}.xml"));
			});

			services.AddSwaggerGenNewtonsoftSupport();

			services.AddControllers().AddNewtonsoftJson(o => {
				o.SerializerSettings.Converters.Add(new StringEnumConverter());
				o.SerializerSettings.Converters.Add(new ObjectIdStringConverter());
				o.SerializerSettings.PreserveReferencesHandling = PreserveReferencesHandling.Objects;
				o.SerializerSettings.DefaultValueHandling = DefaultValueHandling.Include;
				o.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
			});

			services.AddHttpContextAccessor();
		}

		/// <summary>
		/// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		/// </summary>
		/// <param name="app"></param>
		/// <param name="env"></param>
		public virtual void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
			string aggregationPipelinesPath = Path.Combine(dataPath, "AggregationPipelines");
			string configFilePath = Path.Combine(dataPath, "MongoDB", "MongoConfig.xml");
			string credentialFilePath = Path.Combine(dataPath, "MongoDB", "MongoCredentials.xml");

			MongoManager.Configure(aggregationPipelinesPath, configFilePath, credentialFilePath);

			log.Information($"{BaseSettings.ApiName} started. Version = [{BaseSettings.ApiVersion}].");

			if (env.IsDevelopment()) {
				app.UseDeveloperExceptionPage();
				log.Information("Development Env enabled.");
			}
			else
				log.Information("Production Env enabled.");


			// Enable middleware to serve generated Swagger as a JSON endpoint.
			app.UseSwagger(c => c.RouteTemplate = $"/{BaseSettings.SwaggerBaseUrl}/swagger/{{documentName}}/swagger.json");
			//app.UseSwagger();
			// Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.),
			// specifying the Swagger JSON endpoint.
			app.UseSwaggerUI(c => {
				c.SwaggerEndpoint("swagger/v1/swagger.json", $"{BaseSettings.ApiName} {BaseSettings.ApiVersion}");
				c.RoutePrefix = BaseSettings.SwaggerBaseUrl;
			});

			app.UseRouting();
			app.UseCors(builder => builder.WithOrigins(Configuration.GetSection("AppConfiguration:corsOrigins").Get<string[]>()).AllowAnyHeader().AllowAnyMethod().AllowCredentials());
			app.UseAuthorization();
		}
	};
}
