using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Izmainas.FileUploadService.Data;
using Izmainas.FileUploadService.Domain.Constants;
using Izmainas.FileUploadService.Domain.Dtos;
using Izmainas.FileUploadService.Domain.Entities;
using Izmainas.FileUploadService.Domain.Services;
using Izmainas.FileUploadService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace Izmainas.FileUploadService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var app = CreateHostBuilder(args).Build();

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.File(LogMessages.BuildLogFileLocation())
                .CreateLogger();
            
            // Extract from Excel
            IExcelRepository excelRepository = new ExcelRepository();
            var studentItems = Task.Run(() => excelRepository.GetAllSAsync(@"ScheduleRaw\student_schedule.xlsx", 0)).Result;

            var teacherItems = Task.Run(() => excelRepository.GetAllTAsync(@"ScheduleRaw\teacher_schedule.xlsx", 0)).Result;

            // Map from Excel to DB entities
            var studentDbItems = new List<StudentScheduleItem>();
            foreach (var item in studentItems)
            {
                var dbItem = new StudentScheduleItem()
                {                    
                    Lesson = item.Lesson,
                    Day = item.Day,
                    Subject = item.Subject,
                    Class = item.Class
                };
                studentDbItems.Add(dbItem);
            }

            var teacherDbItems = new List<TeacherScheduleItem>();
            foreach (var item in teacherItems)
            {
                var dbItem = new TeacherScheduleItem()
                {
                    Lesson = item.Lesson,
                    Day = item.Day,
                    TeacherName = item.TeacherName,
                    Class = item.Class
                };
                teacherDbItems.Add(dbItem);
            }

            // Write data to SQLite database
            var scope = app.Services.CreateScope();
            
            var services = scope.ServiceProvider;
            ISqliteRepository sqliteRepository = new SqliteRepository(services.GetService<AppDbContext>());
            Task.Run(() => sqliteRepository.EnsureDatabaseCreated());

            // Retrieve from DB
            var studentExportItems = Task.Run(() => sqliteRepository.GetAllSAsync()).Result;
            var teacherExportItems = Task.Run(() => sqliteRepository.GetAllTAsync()).Result;

            // Map from DB to dto
            var studentDtos = new List<StudentScheduleDto>();
            foreach (var item in studentExportItems)
            {
                var dto = new StudentScheduleDto(item.Lesson, item.Day, item.Class, item.Subject);
                studentDtos.Add(dto);
            }

            var teacherDtos = new List<TeacherScheduleDto>();
            foreach (var item in teacherExportItems)
            {
                var dto = new TeacherScheduleDto(item.Lesson, item.Day, item.Class, item.TeacherName);
                teacherDtos.Add(dto);
            }

            // Export data to external server
            INetworkService networkService = new NetworkService(new HttpClient());
            networkService.SendAllSAsync(studentDtos);
            networkService.SendAllTAsync(teacherDtos);

            try
            {
                Log.Information(LogMessages.InfoMessages.StartingService);
                app.Run();
                return;
            }
            catch (Exception ex)
            {             
                Log.Fatal(ex, LogMessages.ErrorMessages.FailedToStartService);
                return;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService(options => 
                {
                    options.ServiceName = ServiceInfo.ServiceName;
                })
                .ConfigureServices((hostContext, services) =>
                {                 
                    services.AddDbContext<AppDbContext>(
                        options => options.UseSqlite(SqliteConstants.BuildConnectionString()));
                    
                    services.AddScoped<ISqliteRepository, SqliteRepository>();
                    services.AddScoped<IExcelRepository, ExcelRepository>();
                    
                    services.AddHttpClient<INetworkService, NetworkService>(c =>
                    {
                        c.BaseAddress = new Uri(ServerOptions.BaseAddress);
                    });
                })
                .UseSerilog();
    }
}
