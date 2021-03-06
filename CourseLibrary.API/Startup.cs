using AutoMapper;
using CourseLibrary.API.DbContexts;
using CourseLibrary.API.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Serialization;
using System;
using System.Linq;

namespace CourseLibrary.API
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // ETag support, using a middleware Kevin Dockx made (nuget package -> Marvin.Cache.Headers)
            services.AddHttpCacheHeaders((expirationModel) =>
            {
                expirationModel.MaxAge = 60;
                expirationModel.CacheLocation = Marvin.Cache.Headers.CacheLocation.Private;
            },
            (validationModelOptions)=>
            {
                // if response becomes stale make sure revalidation happens
                validationModelOptions.MustRevalidate = true;
            });

            // adding caching store middleware
            services.AddResponseCaching();

            // by default api returns json as output as that's the first item in outputformatters
            // we want to also support xml, but not as default, so add it to the by appending it
            // after AddNewtonsoftJson, otherwise the default Json parser will work
            // but in this exercise we used newtonsoft for patch updates portion but make it before
            // xml else we will need to set Accept header in Postman to get json results, otherwise
            // will default to Xml if that is before newtonsoft
            services.AddControllers(setupAction =>
           {
               // this will force api to return 406 status code for output it does not support
               setupAction.ReturnHttpNotAcceptable = true;
               // setup caching profiles, so the same number (240) for example can be shared among different
               // controllers and actions
               setupAction.CacheProfiles.Add("240SecondsCacheProfile", 
                   new CacheProfile { 
                       Duration = 240 
                   });
           })
           .AddNewtonsoftJson(setupAction =>
           {
               // using newtonsoft mainly for patch section and to make sure we get camelcase
               setupAction.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
           })
           .AddXmlDataContractSerializerFormatters()
           .ConfigureApiBehaviorOptions(setupAction =>
           {
               // author wanted to create a different validation error response, instead of 400 wanted 422 status
               // and a few other things to change the default .net response for validation errors
               // wants it to be like RFC format
               setupAction.InvalidModelStateResponseFactory = context =>
               {
                   // create a problem details objects
                   var problemDetailsFactory = context.HttpContext.RequestServices
                    .GetRequiredService<ProblemDetailsFactory>();
                   var problemDetails = problemDetailsFactory.CreateValidationProblemDetails(
                       context.HttpContext,
                       context.ModelState
                   );

                   // add additional info not added by default
                   problemDetails.Detail = "See the errors field for details";
                   problemDetails.Instance = context.HttpContext.Request.Path;

                   // find out which status code to use
                   var actionExecutingContext = context as Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext;

                   // if there are modelstate errors & all arguments were correctly
                   // found/parsed we're dealing with validation errors
                   if ((context.ModelState.ErrorCount > 0) &&
                       (actionExecutingContext?.ActionArguments.Count ==
                       context.ActionDescriptor.Parameters.Count))
                   {
                       problemDetails.Type = "https://courselibrary.com/modelvalidationproblems";
                       problemDetails.Status = StatusCodes.Status422UnprocessableEntity;
                       problemDetails.Title = "One or more validation errors occurred.";

                       return new UnprocessableEntityObjectResult(problemDetails)
                       {
                           ContentTypes = { "application/problem+json" }
                       };
                   };

                   // if one of the arguments wasn't correctly found / couldn't be parsed
                   // we're dealing with null/unparseable input
                   problemDetails.Status = StatusCodes.Status400BadRequest;
                   problemDetails.Title = "One or more validation errors occurred.";

                   return new UnprocessableEntityObjectResult(problemDetails)
                   {
                       ContentTypes = { "application/problem+json" }
                   };
               };
           });

            // add support for application/vnd.marvin.hateoas+json accept header, else will
            // get 406 error code if requesting with that type
            services.Configure<MvcOptions>(config =>
            {
                var newtonsoftJsonOutputFormatter = config.OutputFormatters
                    .OfType<NewtonsoftJsonOutputFormatter>()?.FirstOrDefault();

                if (newtonsoftJsonOutputFormatter != null)
                {
                    newtonsoftJsonOutputFormatter.SupportedMediaTypes.Add("application/vnd.marvin.hateoas+json");
                }
            });

            // register PropertyMappingService (for ordering collections between dto and entity mappings)
            // make sure the fields between them map, exist on each
            services.AddTransient<IPropertyMappingService, PropertyMappingService>();

            // register to check if all fields are there when filtering 
            // by specific fields to return (?Fields=...)
            services.AddTransient<IPropertyCheckerService, PropertyCheckerService>();

            // register automapper (added AutoMapper.Extensions.Microsoft.DependencyInjection package, that includes AutoMapper)
            // will automatically scan the assemblies and check if we have profiles for them for mapping configurations
            services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

            services.AddScoped<ICourseLibraryRepository, CourseLibraryRepository>();

            services.AddDbContext<CourseLibraryContext>(options =>
            {
                options.UseSqlServer(
                    @"Server=(localdb)\mssqllocaldb;Database=CourseLibraryDB;Trusted_Connection=True;");
            }); 
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                // global error handler, make sure project settings are Production to see this
                app.UseExceptionHandler(appBuilder =>
                {
                    appBuilder.Run(async context =>
                    {
                        context.Response.StatusCode = 500;
                        await context.Response.WriteAsync("An unexpected fault happened.  Try again later");
                    });
                });
            }

            // add caching to pipeline, do before routing so it kicks in before MVC routing does
            // he commented this out saying for simple cases use it, otherwise avoid this as it
            // does not work with ETags out of the box, it does not take ETags into account to 
            // avoid caching if ETag has changed -> See Demo - ETags and the Validation Model
            // in Supporting HTTP Cache for ASP.NET Core APIs
            //app.UseResponseCaching();
            
            // run the ETag check after the cache check, if that doesn't stop the request then check ETag
            app.UseHttpCacheHeaders();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
