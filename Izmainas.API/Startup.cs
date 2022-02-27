using System.Reflection;
using Izmainas.API.Data;
using Izmainas.API.Domain.Configuration;
using Izmainas.API.Domain.Constants;
using Izmainas.API.Domain.Services;
using Izmainas.API.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

namespace Izmainas.API
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<AppDbContext>(options => 
                options.UseSqlServer(Configuration.GetConnectionString(DBConstants.DBName)));

            // Data operation services
            services.AddScoped<INotesRepository, NotesRepository>();
            services.AddScoped<IScheduleImportRepository, ScheduleImportRepository>();

            // Data presentation services
            services.AddScoped<IStudentScheduleService, StudentScheduleService>();

            // TODO: Add teahcer schedule service !!!

            services.AddAutoMapper(Assembly.GetExecutingAssembly());

            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                var swaggerOptions = Configuration.GetSection(nameof(SwaggerOptions)).Get<SwaggerOptions>();
                c.SwaggerDoc(
                    swaggerOptions.Version, 
                    new OpenApiInfo 
                    { 
                        Title = swaggerOptions.Title, 
                        Version = swaggerOptions.Version 
                    });
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    var swaggerOptions = Configuration.GetSection(nameof(SwaggerOptions)).Get<SwaggerOptions>();
                    var title = swaggerOptions.Title + ' ' + swaggerOptions.Version;
                    c.SwaggerEndpoint(swaggerOptions.Endpoint, title);
                });
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
